using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Outbox;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountReplayCommittedTailBeyondCapturedWatermarkNeverClaimsCaughtUpAndEventuallyReplaysBothEvents()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        var before = await AccountChangesAsync(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var late = AddOwnAccessEvent(scope.ServiceProvider, owner.UserId);
        await db.SaveChangesAsync();
        Guid following;
        await using (var other = factory.Services.CreateAsyncScope())
        {
            following = AddOwnAccessEvent(other.ServiceProvider, owner.UserId);
            await other.ServiceProvider.GetRequiredService<MakanDbContext>().SaveChangesAsync();
        }
        var gate = factory.Services.GetRequiredService<RealtimeBoundaryGate>().Arm();
        var request = AccountChangesAsync(client, before.NextCursor);
        try
        {
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            await transaction.CommitAsync();
            gate.Release();
            var heldCut = await request;
            Assert.True(heldCut.HasMore);
            Assert.Equal(100, heldCut.RetryAfterMilliseconds);
            var recovered = await DrainAccountAsync(client, heldCut.NextCursor, 1);
            Assert.True(recovered.ContainsKey(late)); Assert.True(recovered.ContainsKey(following));
        }
        finally { gate.Release(); }
    }

    [Fact]
    public async Task AccountReplaySqlProjectionSupportsEmptyPersonalFeed()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var session = await db.UserSessions.AsNoTracking().SingleAsync(s => s.UserId == owner.UserId);
        var service = scope.ServiceProvider.GetRequiredService<IRealtimeSyncService>();
        var first = await service.ReadAsync(owner.UserId, session.Id, null, 10, CancellationToken.None);
        var next = await service.ReadAsync(owner.UserId, session.Id, first.NextCursor, 10, CancellationToken.None);
        Assert.Empty(next.Changes);
        Assert.False(next.HasMore);
    }

    [Fact]
    public async Task AccountReplayRecoversUnknownOfflineConversationThreeMessagesEditAndRetryWithoutDuplicateEffect()
    {
        using var sender = factory.CreateClient();
        using var receiver = factory.CreateClient();
        _ = await CreateMessagingUserAsync(sender);
        var b = await CreateMessagingUserAsync(receiver);
        var before = await AccountChangesAsync(receiver);
        Assert.Empty(before.Changes);
        await using var connection = CreateHubConnection(b.AccessToken);
        await connection.StartAsync(); await connection.StopAsync();
        var conversation = await StartDirectAndReadAsync(sender, b.UserId, ConversationScope.Personal);
        var operation = Guid.NewGuid();
        var first = await SendMessageAndReadAsync(sender, conversation.ConversationId, operation, "پیام اول قطع اتصال");
        var retry = await SendMessageAndReadAsync(sender, conversation.ConversationId, operation, "پیام اول قطع اتصال");
        Assert.Equal(first.MessageId, retry.MessageId);
        await SendMessageAndReadAsync(sender, conversation.ConversationId, Guid.NewGuid(), "پیام دوم قطع اتصال");
        await SendMessageAndReadAsync(sender, conversation.ConversationId, Guid.NewGuid(), "پیام سوم قطع اتصال");
        using var edit = await sender.PatchAsJsonAsync($"/api/v1/conversations/{conversation.ConversationId}/messages/{first.MessageId}",
            new EditMessageCommand("ویرایش هنگام قطع اتصال", first.Version), JsonOptions);
        edit.EnsureSuccessStatusCode();
        await connection.StartAsync();
        var replay = await DrainAccountAsync(receiver, before.NextCursor, 1);
        var events = replay.Values.Where(e => e.Conversation?.ConversationId == conversation.ConversationId).ToArray();
        Assert.Equal(3, events.Count(e => e.Conversation!.Type == MessagingChangeType.MessageCreated));
        Assert.Single(events, e => e.Conversation!.Type == MessagingChangeType.MessageEdited);
        var repeated = await DrainAccountAsync(receiver, before.NextCursor, 2);
        Assert.All(events, e => Assert.True(repeated.ContainsKey(e.EventId)));
        var summary = await receiver.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{conversation.ConversationId}/summary", JsonOptions);
        Assert.Equal(3, summary!.UnreadCount);
        var history = await receiver.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{conversation.ConversationId}/messages", JsonOptions);
        Assert.Equal(3, history!.Messages.Select(m => m.MessageId).Distinct().Count());
    }

    [Fact]
    public async Task AccountReplayBootstrapBeforeSnapshotClosesFirstMessageRace()
    {
        using var aClient = factory.CreateClient(); using var bClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(aClient); var b = await CreateMessagingUserAsync(bClient);
        await using var live = CreateHubConnection(b.AccessToken);
        await live.StartAsync();
        var boundary = await AccountChangesAsync(bClient);
        var conversation = await StartDirectAndReadAsync(aClient, b.UserId, ConversationScope.Personal);
        var receipt = await SendMessageAndReadAsync(aClient, conversation.ConversationId, Guid.NewGuid(), "میان مرز و Snapshot");
        var snapshot = await bClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions);
        Assert.Equal(conversation.ConversationId, Assert.Single(snapshot!).ConversationId);
        var replay = await DrainAccountAsync(bClient, boundary.NextCursor, 1);
        Assert.Single(replay.Values, e => e.Conversation?.ResourceId == receipt.MessageId && e.Conversation.Type == MessagingChangeType.MessageCreated);
        Assert.Equal(1, Assert.Single((await bClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!).UnreadCount);
    }

    [Fact]
    public async Task AccountReplayCursorRejectsOtherUserOtherSessionAndContextRoundTrip()
    {
        using var first = factory.CreateClient(); using var other = factory.CreateClient(); using var secondDevice = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(first); _ = await CreateMessagingUserAsync(other);
        var cursor = (await AccountChangesAsync(first)).NextCursor;
        using var differentUser = await other.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.Conflict, differentUser.StatusCode);
        using var challenge = await secondDevice.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(owner.PhoneNumber), JsonOptions);
        var requested = (await challenge.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var login = await secondDevice.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(requested.ChallengeId, owner.PhoneNumber, factory.GetLatestOtpCode(owner.PhoneNumber)), JsonOptions);
        var auth = (await login.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        UseBearerToken(secondDevice, auth.AccessToken);
        using var differentSession = await secondDevice.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.Conflict, differentSession.StatusCode);
        var org = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(owner.UserId, org, OrganizationRole.Manager);
        await SelectOrganizationAsync(first, membership.MembershipId, OrganizationRole.Manager);
        await SelectPersonalAsync(first);
        using var roundTrip = await first.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.Conflict, roundTrip.StatusCode);
        Assert.Equal(RealtimeErrorCodes.ResyncRequired, await ReadProblemCodeAsync(roundTrip));
        Assert.Empty((await AccountChangesAsync(first)).Changes);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("expired")]
    [InlineData("epoch")]
    public async Task AccountReplayInvalidCursorRequiresExplicitResync(string failure)
    {
        using var client = factory.CreateClient(); _ = await CreateMessagingUserAsync(client);
        var token = (await AccountChangesAsync(client)).NextCursor;
        if (failure == "corrupt") token = "not-a-protected-cursor";
        else
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var protection = scope.ServiceProvider.GetRequiredService<IRealtimeCursorProtector>();
            var value = JsonNode.Parse(protection.Unprotect(token))!;
            if (failure == "epoch") value["Epoch"] = "unknown-epoch";
            else value["IssuedAtUtc"] = DateTime.UtcNow.AddDays(-2);
            token = protection.Protect(value.ToJsonString());
        }
        using var response = await client.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(token));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RealtimeErrorCodes.ResyncRequired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AccountReplaySafeWatermarkRetainsTransactionThatCommitsAfterLaterEventAndBootstrap()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        await using var pendingScope = factory.Services.CreateAsyncScope();
        var pendingDb = pendingScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await using var transaction = await pendingDb.Database.BeginTransactionAsync();
        var lateId = AddOwnAccessEvent(pendingScope.ServiceProvider, owner.UserId);
        await pendingDb.SaveChangesAsync();
        Guid laterId;
        await using (var committedScope = factory.Services.CreateAsyncScope())
        {
            laterId = AddOwnAccessEvent(committedScope.ServiceProvider, owner.UserId);
            await committedScope.ServiceProvider.GetRequiredService<MakanDbContext>().SaveChangesAsync();
        }
        var boundaryWhileUncommitted = await AccountChangesAsync(client);
        await transaction.CommitAsync();
        var replay = await DrainAccountAsync(client, boundaryWhileUncommitted.NextCursor, 1);
        Assert.True(replay.ContainsKey(lateId));
        Assert.True(replay.ContainsKey(laterId));
    }

    [Fact]
    public async Task AccountReplayRolledBackOutboxIsNeverVisibleAndInvalidLimitIsRejected()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        using var bootstrap = await client.GetAsync("/api/v1/realtime/changes");
        bootstrap.EnsureSuccessStatusCode();
        Assert.True(bootstrap.Headers.CacheControl?.NoStore);
        var boundary = (await bootstrap.Content.ReadFromJsonAsync<AccountChangePageResult>(JsonOptions))!;
        Guid rolledBack;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            rolledBack = AddOwnAccessEvent(scope.ServiceProvider, owner.UserId);
            await db.SaveChangesAsync(); await tx.RollbackAsync();
        }
        Assert.False((await DrainAccountAsync(client, boundary.NextCursor)).ContainsKey(rolledBack));
        using var invalid = await client.GetAsync("/api/v1/realtime/changes?limit=201");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static Guid AddOwnAccessEvent(IServiceProvider services, Guid userId)
    {
        var id = Guid.NewGuid();
        services.GetRequiredService<IOutbox>().Enqueue(new OutboxEnvelope(id, RealtimeInvalidationWriter.MessageType,
            JsonSerializer.Serialize(new { Key = "access", TargetUserId = userId }), DateTime.UtcNow));
        return id;
    }
    private static async Task<AccountChangePageResult> AccountChangesAsync(HttpClient client, string? cursor = null, int limit = 100)
        => await AccountReplayTestClient.ReadAsync(client, JsonOptions, cursor, limit);
    private static async Task<Dictionary<Guid, AccountChangeResult>> DrainAccountAsync(HttpClient client, string cursor, int limit = 100)
        => await AccountReplayTestClient.DrainAsync(client, JsonOptions, cursor, limit);
}
