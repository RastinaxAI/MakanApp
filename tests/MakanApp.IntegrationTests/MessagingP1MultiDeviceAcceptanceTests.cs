using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task TwoAuthenticatedSessionsShareReadManualUnreadAndPrivateConversationState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var otherDevice = factory.CreateClient();
        var login = await AuthenticateP1SessionAsync(otherDevice, setup.Second.PhoneNumber, factory.Services);
        Assert.NotEqual(setup.Second.AccessToken, login.AccessToken);
        var sessions = (await otherDevice.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions", JsonOptions))!;
        Assert.Equal(2, sessions.Length); Assert.Equal(2, sessions.Select(s => s.Id).Distinct().Count());
        var baseline = await GetChangesAsync(otherDevice, setup.ConversationId);
        await using var ownHub = CreateHubConnection(login.AccessToken);
        await using var peerHub = CreateHubConnection(setup.First.AccessToken);
        var ownState = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var leakedPrivate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ownHub.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, value =>
        { if (value.ConversationId == setup.ConversationId && value.Type == MessagingChangeType.ConversationUserStateChanged) ownState.TrySetResult(); });
        peerHub.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, value =>
        { if (value.ConversationId == setup.ConversationId && IsP1PrivateChange(value.Type)) leakedPrivate.TrySetResult(); });
        await Task.WhenAll(ownHub.StartAsync(), peerHub.StartAsync());
        await Task.WhenAll(ownHub.InvokeAsync("SubscribeConversation", setup.ConversationId), peerHub.InvokeAsync("SubscribeConversation", setup.ConversationId));
        var first = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-device-first");
        var last = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-device-last");
        var advances = await Task.WhenAll(
            setup.SecondClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/read", new AdvanceConversationCursorCommand(first.Sequence), JsonOptions),
            otherDevice.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/read", new AdvanceConversationCursorCommand(last.Sequence), JsonOptions));
        foreach (var response in advances) { response.EnsureSuccessStatusCode(); response.Dispose(); }
        Assert.Equal(last.Sequence, (await P1ConversationSummaryAsync(setup.SecondClient, setup.ConversationId)).LastReadMessageSequence);
        Assert.Equal(0, (await P1ConversationSummaryAsync(otherDevice, setup.ConversationId)).UnreadCount);
        (await setup.SecondClient.PostAsync($"/api/v1/conversations/{setup.ConversationId}/mark-unread", null)).EnsureSuccessStatusCode();
        var manual = await P1ConversationSummaryAsync(otherDevice, setup.ConversationId);
        Assert.True(manual.IsManuallyUnread); Assert.Equal(last.Sequence, manual.LastReadMessageSequence);
        var unread = (await otherDevice.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations?filter=Unread", JsonOptions))!;
        Assert.Contains(unread, row => row.ConversationId == setup.ConversationId);
        await AdvanceCursorAsync(otherDevice, setup.ConversationId, "read", last.Sequence);
        Assert.False((await P1ConversationSummaryAsync(setup.SecondClient, setup.ConversationId)).IsManuallyUnread);
        (await setup.SecondClient.PostAsync($"/api/v1/conversations/{setup.ConversationId}/archive", null)).EnsureSuccessStatusCode();
        (await otherDevice.PostAsync($"/api/v1/conversations/{setup.ConversationId}/state/pin", null)).EnsureSuccessStatusCode();
        (await setup.SecondClient.PutAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/mute",
            new SetConversationMuteCommand(ConversationMuteMode.Until, DateTime.UtcNow.AddMinutes(30)), JsonOptions)).EnsureSuccessStatusCode();
        await ownState.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-device-while-archived-muted");
        foreach (var client in new[] { setup.SecondClient, otherDevice })
        {
            var state = await P1ConversationSummaryAsync(client, setup.ConversationId, archived: true);
            Assert.True(state.IsArchived); Assert.True(state.IsPinned); Assert.True(state.IsMuted); Assert.Equal(1, state.UnreadCount);
        }
        var peerState = await P1ConversationSummaryAsync(setup.FirstClient, setup.ConversationId);
        Assert.False(peerState.IsArchived); Assert.False(peerState.IsPinned); Assert.False(peerState.IsMuted);
        var ownChanges = await GetChangesAsync(otherDevice, setup.ConversationId, baseline.NextCursor);
        var privateChange = ownChanges.Changes.Last(c => c.Type == MessagingChangeType.ConversationUserStateChanged);
        Assert.True(sessions.Select(s => s.Id).ToHashSet().SetEquals(await ReplayP0NotificationAsync(setup.ConversationId, privateChange)));
        Assert.DoesNotContain((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes, c => IsP1PrivateChange(c.Type));
        Assert.False(leakedPrivate.Task.IsCompleted);
    }

    [Fact]
    public async Task TwoSessionsShareReactionsForMeAndRegistrationsWithoutLeakingToPeer()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var otherDevice = factory.CreateClient();
        await AuthenticateP1SessionAsync(otherDevice, setup.Second.PhoneNumber, factory.Services);
        var firstDevice = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
        var secondDevice = await RegisterP1NotificationDeviceAsync(otherDevice);
        Assert.NotEqual(firstDevice.Id, secondDevice.Id);
        Assert.Equal(2, (await otherDevice.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.TotalCount);
        Assert.Empty((await setup.FirstClient.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.Items);
        var file = await UploadFileAsync(setup.FirstClient, "image/png", "p1-two-device-private.png");
        var message = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [file.Id] });
        var baseline = await GetChangesAsync(otherDevice, setup.ConversationId);
        (await otherDevice.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Like), JsonOptions)).EnsureSuccessStatusCode();
        (await setup.SecondClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Like), JsonOptions)).EnsureSuccessStatusCode();
        var own = (await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!;
        Assert.True(Assert.Single(Assert.Single(own.Messages).Reactions).ReactedByCurrentUser);
        var peer = (await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!;
        Assert.False(Assert.Single(Assert.Single(peer.Messages).Reactions).ReactedByCurrentUser);
        Assert.Equal(1, await factory.CountActiveMessageReactionsAsync(message.MessageId, setup.Second.UserId));
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/reactions/Like")).EnsureSuccessStatusCode();
        (await otherDevice.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/for-me")).EnsureSuccessStatusCode();
        foreach (var client in new[] { setup.SecondClient, otherDevice })
        {
            Assert.Empty((await client.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/files/{file.Id}/content")).StatusCode);
        }
        Assert.Single((await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages);
        var privateChange = Assert.Single((await GetChangesAsync(otherDevice, setup.ConversationId, baseline.NextCursor)).Changes,
            c => c.Type == MessagingChangeType.MessageHiddenForUser);
        var sessions = (await otherDevice.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions", JsonOptions))!;
        Assert.True(sessions.Select(s => s.Id).ToHashSet().SetEquals(await ReplayP0NotificationAsync(setup.ConversationId, privateChange)));
        Assert.DoesNotContain((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes,
            c => c.Type == MessagingChangeType.MessageHiddenForUser);
        (await otherDevice.DeleteAsync($"/api/v1/notifications/devices/{firstDevice.Id}")).EnsureSuccessStatusCode();
        var remaining = Assert.Single((await setup.SecondClient.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.Items);
        Assert.Equal(secondDevice.Id, remaining.Id); Assert.True(remaining.IsActive);
        (await otherDevice.DeleteAsync($"/api/v1/notifications/devices/{secondDevice.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RevokingOneSessionStopsRestHubAndFuturePrivateEventsButKeepsOtherSessionWorking()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var otherDevice = factory.CreateClient();
        var login = await AuthenticateP1SessionAsync(otherDevice, setup.Second.PhoneNumber, factory.Services);
        var sessions = (await setup.SecondClient.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions", JsonOptions))!;
        var revokedId = sessions.Single(s => s.IsCurrent).Id;
        var activeId = sessions.Single(s => !s.IsCurrent).Id;
        await using var oldHub = CreateHubConnection(setup.Second.AccessToken); await using var activeHub = CreateHubConnection(login.AccessToken);
        var received = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        activeHub.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, value =>
        { if (value.ConversationId == setup.ConversationId && value.Type == MessagingChangeType.MessageCreated) received.TrySetResult(value); });
        await Task.WhenAll(oldHub.StartAsync(), activeHub.StartAsync());
        await Task.WhenAll(oldHub.InvokeAsync("SubscribeConversation", setup.ConversationId), activeHub.InvokeAsync("SubscribeConversation", setup.ConversationId));
        (await otherDevice.DeleteAsync($"/api/v1/auth/sessions/{revokedId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await setup.SecondClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/messages")).StatusCode);
        var denied = await Record.ExceptionAsync(() => oldHub.InvokeAsync("SubscribeConversation", setup.ConversationId));
        Assert.NotNull(denied); Assert.True(denied is HubException or HttpRequestException or InvalidOperationException);
        if (denied is InvalidOperationException) Assert.NotEqual(HubConnectionState.Connected, oldHub.State);
        await activeHub.InvokeAsync("SubscribeConversation", setup.ConversationId);
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-valid-second-session");
        Assert.Equal(sent.MessageId, (await received.Task.WaitAsync(TimeSpan.FromSeconds(10))).ResourceId);
        (await otherDevice.PostAsync($"/api/v1/conversations/{setup.ConversationId}/archive", null)).EnsureSuccessStatusCode();
        var privateChange = (await GetChangesAsync(otherDevice, setup.ConversationId)).Changes.Last(c => c.Type == MessagingChangeType.ConversationUserStateChanged);
        var audience = await ReplayP0NotificationAsync(setup.ConversationId, privateChange);
        Assert.Equal(activeId, Assert.Single(audience)); Assert.DoesNotContain(revokedId, audience);
        Assert.Equal(activeId, Assert.Single((await otherDevice.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions", JsonOptions))!).Id);
    }

    private static bool IsP1PrivateChange(MessagingChangeType type) => type is MessagingChangeType.ReadCursorAdvanced or
        MessagingChangeType.DeliveryCursorAdvanced or MessagingChangeType.ConversationUserStateChanged or MessagingChangeType.MessageHiddenForUser;

    private static async Task<ConversationSummaryResult> P1ConversationSummaryAsync(HttpClient client, Guid conversationId, bool archived = false)
    {
        var rows = (await client.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations" + (archived ? "?filter=Archived" : ""), JsonOptions))!;
        return Assert.Single(rows, row => row.ConversationId == conversationId);
    }
}
