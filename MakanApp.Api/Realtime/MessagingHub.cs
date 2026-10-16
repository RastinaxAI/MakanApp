using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

[Authorize]
public sealed class MessagingHub(IMessagingService messagingService, MessagingEphemeralService ephemeral) : Hub<IMessagingHubClient>
{
    private EphemeralConnection Connection => new(Context.ConnectionId,
        AuthenticatedSession.GetUserId(Context.User!), AuthenticatedSession.GetSessionId(Context.User!));

    public override async Task OnConnectedAsync()
    {
        await ephemeral.ConnectAsync(Connection, Context.ConnectionAborted);
        var sessionId = AuthenticatedSession.GetSessionId(Context.User!);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            MessagingRealtimeGroups.Session(sessionId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeConversation(Guid conversationId)
    {
        try
        {
            await messagingService.AuthorizeRealtimeSubscriptionAsync(
                AuthenticatedSession.GetUserId(Context.User!),
                AuthenticatedSession.GetSessionId(Context.User!),
                conversationId,
                Context.ConnectionAborted);
            await ephemeral.SubscribeConversationAsync(Connection, conversationId, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                MessagingRealtimeGroups.Conversation(conversationId),
                Context.ConnectionAborted);
        }
        catch (IdentityException)
        {
            throw new HubException(IdentityErrorCodes.AuthRequired);
        }
        catch (MessagingException)
        {
            throw new HubException(MessagingErrorCodes.RealtimeSubscriptionNotAllowed);
        }
    }

    public async Task UnsubscribeConversation(Guid conversationId)
    {
        ephemeral.Unsubscribe(Context.ConnectionId, conversationId);
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            MessagingRealtimeGroups.Conversation(conversationId),
            Context.ConnectionAborted);
    }

    public Task SetTyping(Guid conversationId, bool isTyping) => GuardAsync(() =>
        ephemeral.SetTypingAsync(Connection, conversationId, isTyping, Context.ConnectionAborted));

    public Task Heartbeat() => GuardAsync(() => ephemeral.HeartbeatAsync(Connection, Context.ConnectionAborted));

    public async Task<PresenceResult> SubscribePresence(Guid userId)
    {
        PresenceResult result = null!;
        await GuardAsync(async () => { result = await ephemeral.WatchPresenceAsync(Connection, userId, Context.ConnectionAborted); });
        return result;
    }

    public Task UnsubscribePresence(Guid userId)
    {
        ephemeral.UnwatchPresence(Context.ConnectionId, userId);
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ephemeral.Disconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private static async Task GuardAsync(Func<Task> action)
    {
        try { await action(); }
        catch (IdentityException) { throw new HubException(IdentityErrorCodes.AuthRequired); }
        catch (MessagingException exception) { throw new HubException(exception.Code); }
    }
}
