using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Realtime;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Realtime;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task Policy20261101AccountReplayUsesCurrentOrganizationGrantAcrossSelectedContextsAndRevocation()
    {
        using var ownerClient = factory.CreateClient(); using var receiverClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var receiver = await CreateMessagingUserAsync(receiverClient);
        var orgA = await factory.CreateOrganizationAsync(NewOrganizationName()); var orgB = await factory.CreateOrganizationAsync(NewOrganizationName());
        var manager = await factory.CreateMembershipAsync(owner.UserId, orgA, OrganizationRole.Manager);
        var memberA = await factory.CreateMembershipAsync(receiver.UserId, orgA, OrganizationRole.Teacher);
        var memberB = await factory.CreateMembershipAsync(receiver.UserId, orgB, OrganizationRole.Teacher);
        await SelectOrganizationAsync(ownerClient, manager.MembershipId, OrganizationRole.Manager);
        await SelectOrganizationAsync(receiverClient, memberB.MembershipId, OrganizationRole.Teacher);
        var boundary = (await receiverClient.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/account-inbox/changes", JsonOptions))!;
        var liveEvents = Channel.CreateUnbounded<RealtimeInvalidation>();
        await using var hub = CreateHubConnection(receiver.AccessToken);
        hub.On<RealtimeInvalidation>("ClientInvalidated", item => { if (item.Key == AccountInboxRealtimeService.InvalidationKey) liveEvents.Writer.TryWrite(item); });
        await hub.StartAsync();
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", new CreateManagedConversationCommand(Guid.NewGuid(),
            ConversationScope.Organization, "صندوق حساب بین بافت‌های مجاز", null, [receiver.UserId]));
        var sent = await SendMessageAndReadAsync(ownerClient, group.ConversationId, Guid.NewGuid(), "محتوای آزمایشی سازمان مجاز");
        Guid eventId;
        await using (var scope = factory.Services.CreateAsyncScope())
            eventId = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().MessagingChangeEvents.Where(e => e.ResourceId == sent.MessageId && e.ChangeType == MessagingChangeType.MessageCreated).Select(e => e.Id).SingleAsync();
        await WaitPolicyInvalidationAsync(liveEvents.Reader, eventId);
        Assert.Contains(eventId, (await DrainPolicyAccountChangesAsync(receiverClient, boundary.NextCursor)).Keys);
        var inbox = (await receiverClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.Contains(inbox.Items, item => item.ConversationId == group.ConversationId && item.OrganizationId == orgA);
        Assert.DoesNotContain((await receiverClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!, item => item.ConversationId == group.ConversationId);
        await factory.EndMembershipAsync(memberA.MembershipId);
        Assert.DoesNotContain(eventId, (await DrainPolicyAccountChangesAsync(receiverClient, boundary.NextCursor)).Keys);
        Assert.DoesNotContain((await receiverClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!.Items, item => item.ConversationId == group.ConversationId);
        Assert.Equal(orgB, (await receiverClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!.OrganizationId);
    }

    [Fact]
    public async Task Policy20261101AccountInvalidationReachesIndependentSessionsWithoutOpeningChatAndReplaysOfflineChanges()
    {
        using var senderClient = factory.CreateClient(); using var receiverClient = factory.CreateClient(); using var secondClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient); var receiver = await CreateMessagingUserAsync(receiverClient);
        await factory.SetCommunicationAgeCategoryAsync(sender.UserId, CommunicationAgeCategory.Unknown);
        await factory.SetCommunicationAgeCategoryAsync(receiver.UserId, CommunicationAgeCategory.Unknown);
        var boundary = (await receiverClient.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/account-inbox/changes", JsonOptions))!;
        using var challenge = await secondClient.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(receiver.PhoneNumber), JsonOptions);
        challenge.EnsureSuccessStatusCode(); var challengeDto = (await challenge.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var verify = await secondClient.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challengeDto.ChallengeId, receiver.PhoneNumber, factory.GetLatestOtpCode(receiver.PhoneNumber)), JsonOptions);
        verify.EnsureSuccessStatusCode(); var secondSession = (await verify.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        UseBearerToken(secondClient, secondSession.AccessToken);
        var firstEvents = Channel.CreateUnbounded<RealtimeInvalidation>(); var secondEvents = Channel.CreateUnbounded<RealtimeInvalidation>();
        await using var firstHub = CreateHubConnection(receiver.AccessToken); await using var secondHub = CreateHubConnection(secondSession.AccessToken);
        firstHub.On<RealtimeInvalidation>("ClientInvalidated", item => { if (item.Key == AccountInboxRealtimeService.InvalidationKey) firstEvents.Writer.TryWrite(item); });
        secondHub.On<RealtimeInvalidation>("ClientInvalidated", item => { if (item.Key == AccountInboxRealtimeService.InvalidationKey) secondEvents.Writer.TryWrite(item); });
        await firstHub.StartAsync(); await secondHub.StartAsync();
        var conversation = await StartDirectAndReadAsync(senderClient, receiver.UserId, ConversationScope.Personal);
        var operation = Guid.NewGuid();
        var message = await SendMessageAndReadAsync(senderClient, conversation.ConversationId, operation, "اعلان حساب بدون بازکردن گفتگو");
        Guid eventId;
        await using (var scope = factory.Services.CreateAsyncScope())
            eventId = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().MessagingChangeEvents
                .Where(e => e.ResourceId == message.MessageId && e.ChangeType == MessagingChangeType.MessageCreated).Select(e => e.Id).SingleAsync();
        await WaitPolicyInvalidationAsync(firstEvents.Reader, eventId); await WaitPolicyInvalidationAsync(secondEvents.Reader, eventId);
        var inbox = (await secondClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        var summary = Assert.Single(inbox.Items, item => item.ConversationId == conversation.ConversationId);
        Assert.Equal(1, summary.UnreadCount); Assert.Equal(1, summary.LastMessageSequence);
        await firstHub.StopAsync();
        var offline = await SendMessageAndReadAsync(senderClient, conversation.ConversationId, Guid.NewGuid(), "پیام ساختگی در زمان قطع اتصال");
        await firstHub.StartAsync();
        var changes = await DrainPolicyAccountChangesAsync(receiverClient, boundary.NextCursor);
        Assert.Contains(changes.Values, item => item.EventId == eventId);
        Assert.All(changes.Values, item => { Assert.Null(item.Conversation); Assert.Null(item.Invalidation!.ResourceId); Assert.Equal(AccountInboxRealtimeService.InvalidationKey, item.Invalidation.Key); });
        var repeated = await SendMessageAndReadAsync(senderClient, conversation.ConversationId, operation, "اعلان حساب بدون بازکردن گفتگو");
        Assert.Equal(message.MessageId, repeated.MessageId);
        var current = (await receiverClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.Equal(2, Assert.Single(current.Items, item => item.ConversationId == conversation.ConversationId).UnreadCount);
        await using var finalScope = factory.Services.CreateAsyncScope(); var db = finalScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(2, await db.Messages.CountAsync(m => m.ConversationId == conversation.ConversationId));
        var offlineEvent = await db.MessagingChangeEvents.Where(e => e.ResourceId == offline.MessageId && e.ChangeType == MessagingChangeType.MessageCreated).Select(e => e.Id).SingleAsync();
        Assert.Contains(offlineEvent, changes.Keys);
    }

    [Fact]
    public async Task Policy20261101AccountReplayCursorCannotBeReusedAcrossUsersOrScopedViews()
    {
        using var first = factory.CreateClient(); using var second = factory.CreateClient(); await CreateMessagingUserAsync(first); await CreateMessagingUserAsync(second);
        var account = (await first.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/account-inbox/changes", JsonOptions))!;
        var scoped = (await first.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/changes?scope=Personal", JsonOptions))!;
        foreach (var (client, route, cursor) in new[]
        {
            (second, "/api/v1/realtime/account-inbox/changes?cursor=", account.NextCursor),
            (first, "/api/v1/realtime/account-inbox/changes?cursor=", scoped.NextCursor),
            (first, "/api/v1/realtime/changes?scope=Personal&cursor=", account.NextCursor)
        })
        {
            using var rejected = await client.GetAsync(route + Uri.EscapeDataString(cursor));
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode); Assert.Equal(RealtimeErrorCodes.ResyncRequired, await ReadProblemCodeAsync(rejected));
        }
        var sameView = await first.GetAsync("/api/v1/realtime/account-inbox/changes?cursor=" + Uri.EscapeDataString(account.NextCursor)); sameView.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(CommunicationAgeCategory.Unknown)]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Adult)]
    public async Task Policy20261101UnavailableSafetyFailsClosedForEveryAge(CommunicationAgeCategory age)
    {
        using var first = factory.CreateClient(); using var second = factory.CreateClient(); var owner = await CreateMessagingUserAsync(first); var target = await CreateMessagingUserAsync(second);
        await factory.SetCommunicationAgeCategoryAsync(owner.UserId, age); await factory.SetCommunicationAgeCategoryAsync(target.UserId, age);
        var conversation = await StartDirectAndReadAsync(first, target.UserId, ConversationScope.Personal);
        factory.ContentSafetyProvider.ConfigureUnavailable();
        try
        {
            using var rejected = await SendMessageAsync(first, conversation.ConversationId, Guid.NewGuid(), "پیام آزمایشی در نبود Safety");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode); Assert.Equal(MessagingErrorCodes.MessageSafetyUnavailable, await ReadProblemCodeAsync(rejected));
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Messages.AnyAsync(m => m.ConversationId == conversation.ConversationId));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    private static async Task WaitPolicyInvalidationAsync(ChannelReader<RealtimeInvalidation> reader, Guid eventId)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        RealtimeInvalidation item;
        do { item = await reader.ReadAsync(deadline.Token); } while (item.EventId != eventId);
        Assert.Null(item.ResourceId); Assert.NotEmpty(item.ContextRevision); Assert.NotEmpty(item.StreamVersion);
    }

    private static async Task<Dictionary<Guid, AccountChangeResult>> DrainPolicyAccountChangesAsync(HttpClient client, string cursor)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var unique = new Dictionary<Guid, AccountChangeResult>();
        while (true)
        {
            var page = (await client.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/account-inbox/changes?limit=2&cursor=" + Uri.EscapeDataString(cursor), JsonOptions, deadline.Token))!;
            foreach (var item in page.Changes) unique[item.EventId] = item;
            cursor = page.NextCursor;
            if (!page.HasMore) return unique;
            if (page.RetryAfterMilliseconds.HasValue) await Task.Delay(page.RetryAfterMilliseconds.Value, deadline.Token);
        }
    }
}
