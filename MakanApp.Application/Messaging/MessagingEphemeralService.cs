using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;

namespace MakanApp.Application.Messaging;

public sealed class MessagingEphemeralService(
    IAccessContextResolver contexts, IMessagingEphemeralAccessStore access,
    IMessagingEphemeralState state, IMessagingEphemeralNotifier notifier,
    MessagingEphemeralOptions options, TimeProvider clock, MessagingOperationContextResolver? operationContexts = null)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task ConnectAsync(EphemeralConnection connection, CancellationToken cancellationToken)
    {
        await ResolveAccountAsync(connection.UserId, connection.SessionId, cancellationToken);
        state.Connect(connection, Now);
    }

    public async Task SubscribeConversationAsync(EphemeralConnection connection, Guid conversationId, CancellationToken cancellationToken)
    {
        var context = await AuthorizeConnectionAsync(connection, cancellationToken, conversationId);
        await access.AuthorizeTypingRecipientAsync(connection.UserId, connection.UserId, conversationId, context, Now, cancellationToken);
        state.SubscribeConversation(connection.ConnectionId, conversationId);
    }

    public async Task SetTypingAsync(EphemeralConnection connection, Guid conversationId, bool isTyping, CancellationToken cancellationToken)
    {
        var context = await AuthorizeConnectionAsync(connection, cancellationToken, conversationId);
        await access.AuthorizeTypingAsync(connection.UserId, conversationId, context, Now, cancellationToken);
        state.SetTyping(connection.ConnectionId, conversationId, isTyping, Now);
        await FlushAsync(cancellationToken);
    }

    public async Task HeartbeatAsync(EphemeralConnection connection, CancellationToken cancellationToken)
    {
        await AuthorizeConnectionAsync(connection, cancellationToken);
        state.Heartbeat(connection.ConnectionId, Now);
        await FlushAsync(cancellationToken);
    }

    public async Task<PresenceResult> WatchPresenceAsync(EphemeralConnection connection, Guid targetUserId, CancellationToken cancellationToken)
    {
        var context = await AuthorizeConnectionAsync(connection, cancellationToken);
        var result = await PresenceCoreAsync(connection.UserId, targetUserId, context, cancellationToken);
        if (result.State == MessagingPresenceState.Hidden)
            throw new MessagingException(MessagingErrorCodes.RealtimeSubscriptionNotAllowed, "مشاهدهٔ این وضعیت مجاز نیست.");
        state.WatchPresence(connection.ConnectionId, targetUserId);
        return result;
    }

    public async Task<PresenceResult> GetPresenceAsync(Guid userId, Guid sessionId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var context = await ResolveAccountAsync(userId, sessionId, cancellationToken);
        return await PresenceCoreAsync(userId, targetUserId, context, cancellationToken);
    }

    public void Unsubscribe(string connectionId, Guid conversationId) => state.UnsubscribeConversation(connectionId, conversationId, Now);
    public void Disconnect(string connectionId) => state.Disconnect(connectionId, Now);
    public void UnwatchPresence(string connectionId, Guid targetUserId) => state.UnwatchPresence(connectionId, targetUserId);

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        // ارسال hintها سریال است؛ state و فرمان‌های ورودی هنگام I/O قفل نمی‌مانند.
        if (!state.TryBeginDispatch()) return;
        try
        {
            var connections = state.Connections(Now);
            var active = await access.GetActiveSessionsAsync(connections.Select(c => c.SessionId).Distinct().ToArray(), Now, cancellationToken);
            foreach (var change in state.Drain(Now, active, 128))
            {
                if (change.ConversationId is Guid conversationId)
                    await PublishTypingAsync(change, conversationId, cancellationToken);
                else
                    foreach (var observer in state.PresenceObservers(change.UserId, Now))
                    {
                        try
                        {
                            var context = await ResolveAccountAsync(observer.UserId, observer.SessionId, cancellationToken);
                            var result = await PresenceCoreAsync(observer.UserId, change.UserId, context, cancellationToken);
                            // Hidden را بر اثر فعالیت مقصد broadcast نمی‌کنیم؛ زمان رخداد هم می‌تواند نشت باشد.
                            // وقتی Online مخفی است، حتی تکرار Recently با ریتم heartbeat نباید فعالیت را لو بدهد.
                            if (result.State != MessagingPresenceState.Hidden &&
                                !(result.State == MessagingPresenceState.Recently && result.LastSeenAtUtc is null))
                                await notifier.PresenceChangedAsync(observer.ConnectionId, result, cancellationToken);
                        }
                        catch (Exception exception) when (IsDenied(exception)) { }
                    }
            }
        }
        finally { state.EndDispatch(); }
    }

    private async Task PublishTypingAsync(EphemeralChange change, Guid conversationId, CancellationToken cancellationToken)
    {
        var sourceAllowed = false;
        foreach (var source in change.TypingSources)
        {
            try
            {
                var context = await ResolveConversationAsync(source.UserId, source.SessionId, conversationId, cancellationToken);
                await access.AuthorizeTypingAsync(source.UserId, conversationId, context, Now, cancellationToken);
                sourceAllowed = true;
                break;
            }
            catch (Exception exception) when (IsDenied(exception)) { }
        }
        if (!sourceAllowed || change.IsTyping && change.ExpiresAtUtc <= Now) return;
        var recipients = new List<string>();
        foreach (var observer in state.ConversationObservers(conversationId, Now).Where(c => c.UserId != change.UserId))
        {
            try
            {
                var context = await ResolveConversationAsync(observer.UserId, observer.SessionId, conversationId, cancellationToken);
                await access.AuthorizeTypingRecipientAsync(observer.UserId, change.UserId, conversationId, context, Now, cancellationToken);
                recipients.Add(observer.ConnectionId);
            }
            catch (Exception exception) when (IsDenied(exception)) { }
        }
        if (recipients.Count > 0 && (!change.IsTyping || change.ExpiresAtUtc > Now))
            await notifier.TypingChangedAsync(recipients,
                new(conversationId, change.UserId, change.IsTyping, change.ExpiresAtUtc), cancellationToken);
    }

    private async Task<PresenceResult> PresenceCoreAsync(Guid userId, Guid targetUserId, AccessContext context, CancellationToken cancellationToken)
    {
        var visibility = await access.GetPresenceVisibilityAsync(userId, targetUserId, context, Now, cancellationToken);
        if (!visibility.Online && !visibility.LastSeen)
            return new(targetUserId, MessagingPresenceState.Hidden, null, Now.Add(options.ConnectionLease));
        var sessions = state.Connections(Now).Where(c => c.UserId == targetUserId).Select(c => c.SessionId).Distinct().ToArray();
        var active = await access.GetActiveSessionsAsync(sessions, Now, cancellationToken);
        var snapshot = state.Presence(targetUserId, active, Now);
        var status = snapshot.IsOnline && visibility.Online ? MessagingPresenceState.Online :
            visibility.LastSeen && (snapshot.IsOnline || snapshot.LastSeenAtUtc.HasValue && snapshot.LastSeenAtUtc.Value.Add(options.RecentlyWindow) > Now)
                ? MessagingPresenceState.Recently : MessagingPresenceState.Offline;
        // زمان heartbeat در حالت Online/مخفی‌بودن Online نباید راه فرعی تشخیص فعالیت دقیق باشد.
        var exact = !snapshot.IsOnline && visibility.LastSeen ? snapshot.LastSeenAtUtc : null;
        return new(targetUserId, status, exact, Now.Add(options.ConnectionLease));
    }

    private async Task<AccessContext> AuthorizeConnectionAsync(EphemeralConnection connection, CancellationToken cancellationToken, Guid? conversationId = null)
    {
        state.Consume(connection, Now);
        return conversationId.HasValue
            ? await ResolveConversationAsync(connection.UserId, connection.SessionId, conversationId.Value, cancellationToken)
            : await ResolveAccountAsync(connection.UserId, connection.SessionId, cancellationToken);
    }

    private static bool IsDenied(Exception exception) => exception is IdentityException or OrganizationException or GuardianException or MessagingException;
    private Task<AccessContext> ResolveAccountAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        operationContexts is null ? contexts.ResolveAsync(userId, sessionId, ct) : operationContexts.PersonalAsync(userId, sessionId, ct);
    private Task<AccessContext> ResolveConversationAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct) =>
        operationContexts is null ? contexts.ResolveAsync(userId, sessionId, ct) : operationContexts.ConversationAsync(userId, sessionId, conversationId, ct);
}
