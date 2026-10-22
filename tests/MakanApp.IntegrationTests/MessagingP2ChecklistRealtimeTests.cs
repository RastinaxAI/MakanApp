using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    private static MessagingRealtimeNotification P2ChecklistNotification(Guid conversationId, ConversationChangeResult change) =>
        new(conversationId, change.Type, change.ResourceId, change.ResourceVersion, change.Cursor, change.OccurredAtUtc, change.PayloadVersion);

    [Fact]
    public async Task P2ChecklistPersonalRealtimeReachesSameUsersSecondDeviceButNotPeerEvenWithBroadAudienceHint()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var device = factory.CreateClient();
        var session = await AuthenticateP1SessionAsync(device, setup.First.PhoneNumber, factory.Services);
        var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId); var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        var baselineA = await GetChangesAsync(device, setup.ConversationId); var baselineB = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        await using var connectionA = CreateHubConnection(session.AccessToken); await using var connectionB = CreateHubConnection(setup.Second.AccessToken);
        var arrived = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = new ConcurrentQueue<MessagingRealtimeNotification>();
        connectionA.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n => { if (n.Type == MessagingChangeType.ChecklistChanged && n.ResourceId == receipt.MessageId) arrived.TrySetResult(n); });
        connectionB.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n => { if (n.Type == MessagingChangeType.ChecklistChanged && n.ResourceId == receipt.MessageId) peer.Enqueue(n); });
        await StartP2HubAsync(connectionA); await StartP2HubAsync(connectionB);
        await connectionA.InvokeAsync("SubscribeConversation", setup.ConversationId); await connectionB.InvokeAsync("SubscribeConversation", setup.ConversationId);
        await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        var notification = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var aChanges = await GetChangesAsync(device, setup.ConversationId, baselineA.NextCursor);
        var own = Assert.Single(aChanges.Changes, c => c.Type == MessagingChangeType.ChecklistChanged); Assert.Null(own.ActorUserId);
        Assert.DoesNotContain((await GetChangesAsync(setup.SecondClient, setup.ConversationId, baselineB.NextCursor)).Changes, c => c.Type == MessagingChangeType.ChecklistChanged);
        Assert.True((await ReadP2ChecklistAsync(device, setup.ConversationId, receipt.MessageId)).Items.First().IsChecked);
        Assert.All((await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId)).Items, i => Assert.False(i.IsChecked));
        await using var scope = factory.Services.CreateAsyncScope(); var resolver = scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>();
        var sessions = await resolver.GetActiveSessionIdsAsync(notification, null, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync();
        Assert.Equal(2, sessions.Count); Assert.Empty(await resolver.GetActiveSessionIdsAsync(notification, setup.Second.UserId, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync());
        Assert.Empty(peer);
        var wire = JsonSerializer.Serialize(notification, JsonOptions);
        Assert.DoesNotContain("isChecked", wire, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(checklist.Title, wire);
        Assert.DoesNotContain(setup.First.UserId.ToString(), wire, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P2ChecklistCollaborativeRealtimeAndReplayRecheckRevocationRemovalAndVisibility()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id, P2Checklist(ConversationChecklistMode.Collaborative));
        var checklist = await ReadP2ChecklistAsync(setup.OwnerClient, id, receipt.MessageId);
        await using var hub = CreateHubConnection(setup.Target.AccessToken); var arrived = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n => { if (n.Type == MessagingChangeType.ChecklistChanged && n.ResourceId == receipt.MessageId) arrived.TrySetResult(n); });
        await StartP2HubAsync(hub); await hub.InvokeAsync("SubscribeConversation", id);
        await SetP2ChecklistAsync(setup.OwnerClient, checklist, checklist.Items.First().ItemId, true);
        var notification = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var scope = factory.Services.CreateAsyncScope(); var resolver = scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>();
        Assert.Single(await resolver.GetActiveSessionIdsAsync(notification, setup.Target.UserId, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync());
        (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
        Assert.Empty(await resolver.GetActiveSessionIdsAsync(notification, setup.Target.UserId, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync());
        Assert.NotEmpty(await resolver.GetActiveSessionIdsAsync(notification, setup.Owner.UserId, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync());
        await factory.RevokeSessionByTokenAsync(setup.Owner.AccessToken);
        Assert.Empty(await resolver.GetActiveSessionIdsAsync(notification, null, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await setup.OwnerClient.GetAsync(P2ChecklistRoute(id, receipt.MessageId))).StatusCode);
    }

    [Fact]
    public async Task P2ChecklistDefinitionEventIsSharedButNeverIncludesPersonalInteractionState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        checklist = await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        var baseline = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        await UpdateP2ChecklistAsync(setup.FirstClient, checklist, P2ChecklistUpdate(checklist) with { Title = "عنوان مشترک تازه" });
        var changed = Assert.Single((await GetChangesAsync(setup.SecondClient, setup.ConversationId, baseline.NextCursor)).Changes, c => c.Type == MessagingChangeType.ChecklistChanged);
        var notification = P2ChecklistNotification(setup.ConversationId, changed);
        await using var scope = factory.Services.CreateAsyncScope(); var resolver = scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>();
        Assert.Equal(2, (await resolver.GetActiveSessionIdsAsync(notification, null, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync()).Count);
        Assert.DoesNotContain("isChecked", JsonSerializer.Serialize(changed, JsonOptions), StringComparison.OrdinalIgnoreCase);
        var peer = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId); Assert.All(peer.Items, i => Assert.Null(i.StateVersion));
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/for-me")).EnsureSuccessStatusCode();
        Assert.Empty(await resolver.GetActiveSessionIdsAsync(notification, setup.Second.UserId, factory.Clock.GetUtcNow().UtcDateTime, default).ToTestArrayAsync());
    }
}
