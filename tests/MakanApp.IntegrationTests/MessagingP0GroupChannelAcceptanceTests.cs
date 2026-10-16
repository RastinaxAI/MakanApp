using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task RemovedGroupOrChannelMemberLosesFilesMutationsDeltaSearchAndRealtime(string resource)
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var conversation = await CreateManagedAndReadAsync(ownerClient, resource, PersonalCommand("پذیرش دسترسی جاری", member.UserId));
        var id = conversation.ConversationId;
        var file = await UploadFileAsync(ownerClient, "image/png", "p0-private.png");
        var message = await SendAdvancedMessageAsync(ownerClient, id, new SendMessageCommand
        {
            ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, Text = "p0-private-attachment", AttachmentIds = [file.Id]
        });
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync($"/api/v1/files/{file.Id}/content")).StatusCode);
        var baseline = await GetChangesAsync(memberClient, id);
        await using var connection = CreateHubConnection(member.AccessToken);
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeConversation", id);
        using var remove = await ownerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{member.UserId}");
        remove.EnsureSuccessStatusCode();
        foreach (var route in new[]
        {
            $"/api/v1/conversations/{id}/messages",
            $"/api/v1/conversations/{id}/media",
            $"/api/v1/conversations/{id}/changes?afterCursor={Uri.EscapeDataString(baseline.NextCursor)}",
            $"/api/v1/files/{file.Id}/content"
        }) Assert.Equal(HttpStatusCode.NotFound, (await memberClient.GetAsync(route)).StatusCode);
        using var deniedSend = await SendMessageAsync(memberClient, id, Guid.NewGuid(), "شناسه قدیمی");
        using var deniedEdit = await memberClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/messages/{message.MessageId}", new EditMessageCommand("غیرمجاز", message.Version), JsonOptions);
        using var deniedDelete = await memberClient.DeleteAsync($"/api/v1/conversations/{id}/messages/{message.MessageId}?expectedVersion={Uri.EscapeDataString(message.Version)}");
        Assert.Equal(HttpStatusCode.NotFound, deniedSend.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedEdit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedDelete.StatusCode);
        Assert.Empty((await SearchAsync(memberClient, "p0-private-attachment")).Items);
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SubscribeConversation", id));
        await using var scope = factory.Services.CreateAsyncScope();
        var notification = await ReadP0ConversationNotificationAsync(ownerClient, id);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>()
            .GetActiveSessionIdsAsync(notification, member.UserId, factory.Clock.GetUtcNow().UtcDateTime, default));
        var future = await SendMessageAndReadAsync(ownerClient, id, Guid.NewGuid(), "ارسال مالک پس از حذف عضو");
        Assert.Equal(message.Sequence + 1, future.Sequence);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/api/v1/files/{file.Id}/content")).StatusCode);
    }

    [Fact]
    public async Task ConversationPaginationIsStableAcrossThirtyOneTiedRowsAndActivityRefresh()
    {
        using var client = factory.CreateClient();
        _ = await CreateMessagingUserAsync(client);
        var frozen = DateTime.UtcNow.AddMinutes(2);
        factory.Clock.SetUtcNow(frozen);
        try
        {
            var ids = new List<Guid>();
            for (var index = 0; index < 31; index++)
                ids.Add((await CreateManagedAndReadAsync(client, "groups", PersonalCommand($"صفحه‌بندی {index}"))).ConversationId);
            var firstRead = await ReadP0ConversationPagesAsync(client);
            var repeated = await ReadP0ConversationPagesAsync(client);
            Assert.Equal(firstRead, repeated);
            Assert.Equal(31, firstRead.Distinct().Count());
            Assert.True(ids.ToHashSet().SetEquals(firstRead));
            factory.Clock.SetUtcNow(frozen.AddSeconds(1));
            await SendMessageAndReadAsync(client, firstRead[^1], Guid.NewGuid(), "فعالیت تازه");
            var refreshed = await ReadP0ConversationPagesAsync(client);
            Assert.Equal(firstRead[^1], refreshed[0]);
            Assert.True(ids.ToHashSet().SetEquals(refreshed));
            Assert.Equal(31, refreshed.Distinct().Count());
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task FromJoinBoundaryAppliesToSearchFilesDeltaAndReplayedRealtimeMessage()
    {
        using var ownerClient = factory.CreateClient();
        using var joinerClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(ownerClient);
        var joiner = await CreateMessagingUserAsync(joinerClient);
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", new CreateManagedConversationCommand(
            Guid.NewGuid(), ConversationScope.Personal, "تاریخچه محدود P0", null, [], ConversationVisibility.Public, GroupHistoryPolicy.FromJoin));
        var file = await UploadFileAsync(ownerClient, "image/png", "p0-before-join.png");
        var oldMessage = await SendAdvancedMessageAsync(ownerClient, group.ConversationId, new SendMessageCommand
        {
            ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, Text = "p0-before-join", AttachmentIds = [file.Id]
        });
        var oldChange = Assert.Single((await GetChangesAsync(ownerClient, group.ConversationId)).Changes,
            c => c.Type == MessagingChangeType.MessageCreated && c.ResourceId == oldMessage.MessageId);
        using var join = await joinerClient.PostAsync($"/api/v1/conversations/{group.ConversationId}/join", null);
        join.EnsureSuccessStatusCode();
        var current = await SendMessageAndReadAsync(ownerClient, group.ConversationId, Guid.NewGuid(), "p0-after-join");
        var history = (await joinerClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{group.ConversationId}/messages", JsonOptions))!;
        Assert.Equal(current.MessageId, Assert.Single(history.Messages).MessageId);
        Assert.Empty((await SearchAsync(joinerClient, "p0-before-join")).Items);
        Assert.Equal(current.MessageId, Assert.Single((await SearchAsync(joinerClient, "p0-after-join")).Items).MessageId);
        Assert.Empty((await joinerClient.GetFromJsonAsync<ConversationMediaPageResult>($"/api/v1/conversations/{group.ConversationId}/media", JsonOptions))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await joinerClient.GetAsync($"/api/v1/files/{file.Id}/content")).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(joinerClient, group.ConversationId)).Changes, c => c.ResourceId == oldMessage.MessageId);

        await using var connection = CreateHubConnection(joiner.AccessToken);
        var forbiddenEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n =>
        {
            if (n.ResourceId == oldMessage.MessageId) forbiddenEvent.TrySetResult();
        });
        await connection.StartAsync();
        var recipients = await ReplayP0NotificationAsync(group.ConversationId, oldChange);
        Assert.Empty(recipients.Intersect(await ReadP0UserSessionIdsAsync(joiner.UserId)));
        // کنار بررسی قطعی گیرنده‌ها در SQL، مسیر واقعی Hub نیز بازپخش می‌شود.
        await connection.InvokeAsync("SubscribeConversation", group.ConversationId);
        Assert.False(forbiddenEvent.Task.IsCompleted, "رویداد تاریخچهٔ غیرمجاز نباید به عضو جدید برسد.");
    }

    [Fact]
    public async Task PrivateReadCursorCannotLeakWhenNotificationAudienceHintIsMissing()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var message = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "private cursor P0");
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", message.Sequence);
        var privateChange = Assert.Single((await GetChangesAsync(setup.SecondClient, setup.ConversationId)).Changes,
            c => c.Type == MessagingChangeType.ReadCursorAdvanced);
        await using var connection = CreateHubConnection(setup.First.AccessToken);
        var forbiddenEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n =>
        {
            if (n.Type == MessagingChangeType.ReadCursorAdvanced) forbiddenEvent.TrySetResult();
        });
        await connection.StartAsync();
        var recipients = await ReplayP0NotificationAsync(setup.ConversationId, privateChange);
        Assert.True((await ReadP0UserSessionIdsAsync(setup.Second.UserId)).ToHashSet().SetEquals(recipients));
        await connection.InvokeAsync("SubscribeConversation", setup.ConversationId);
        Assert.False(forbiddenEvent.Task.IsCompleted, "اشارهٔ audience جای کنترل رویداد خصوصی را نمی‌گیرد.");
    }

    private async Task<IReadOnlyCollection<Guid>> ReplayP0NotificationAsync(Guid conversationId, ConversationChangeResult change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var notification = new MessagingRealtimeNotification(conversationId, change.Type, change.ResourceId,
            change.ResourceVersion, change.Cursor, change.OccurredAtUtc, change.PayloadVersion);
        var recipients = await scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>()
            .GetActiveSessionIdsAsync(notification, null, factory.Clock.GetUtcNow().UtcDateTime, default);
        await scope.ServiceProvider.GetRequiredService<IMessagingRealtimeNotifier>().NotifyConversationChangedAsync(
            notification, null, default);
        return recipients;
    }

    private async Task<Guid[]> ReadP0UserSessionIdsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UserSessions
            .Where(session => session.UserId == userId).Select(session => session.Id).ToArrayAsync();
    }

    private static async Task<MessagingRealtimeNotification> ReadP0ConversationNotificationAsync(HttpClient client, Guid conversationId)
    {
        var change = (await GetChangesAsync(client, conversationId)).Changes.First();
        return new MessagingRealtimeNotification(conversationId, change.Type, change.ResourceId,
            change.ResourceVersion, change.Cursor, change.OccurredAtUtc, change.PayloadVersion);
    }

    private static async Task<Guid[]> ReadP0ConversationPagesAsync(HttpClient client)
    {
        var ids = new List<Guid>();
        for (var page = 1; page <= 6; page++)
        {
            var rows = (await client.GetFromJsonAsync<ConversationSummaryResult[]>($"/api/v1/conversations?page={page}&pageSize=7", JsonOptions))!;
            if (page == 6) Assert.Empty(rows);
            ids.AddRange(rows.Select(r => r.ConversationId));
        }
        return ids.ToArray();
    }
}
