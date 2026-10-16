using MakanApp.Application.Organization;

namespace MakanApp.Application.Messaging;

public enum MessagingPresenceState { Online = 1, Recently = 2, Offline = 3, Hidden = 4 }

public sealed record TypingNotification(Guid ConversationId, Guid UserId, bool IsTyping, DateTime ExpiresAtUtc);
public sealed record PresenceResult(Guid UserId, MessagingPresenceState State, DateTime? LastSeenAtUtc, DateTime ExpiresAtUtc);
public sealed record EphemeralConnection(string ConnectionId, Guid UserId, Guid SessionId);
public sealed record EphemeralChange(Guid UserId, Guid? ConversationId, bool IsTyping, DateTime ExpiresAtUtc, IReadOnlyCollection<EphemeralConnection> TypingSources);
public sealed record PresenceVisibility(bool Online, bool LastSeen);
public sealed record EphemeralPresenceSnapshot(bool IsOnline, DateTime? LastSeenAtUtc);

public sealed class MessagingEphemeralOptions
{
    public TimeSpan TypingTtl { get; init; } = TimeSpan.FromSeconds(6);
    public TimeSpan TypingRefreshInterval { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan ConnectionLease { get; init; } = TimeSpan.FromSeconds(45);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RecentlyWindow { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan LastSeenRetention { get; init; } = TimeSpan.FromDays(1);
    public int MaximumConnections { get; init; } = 10000;
    public int MaximumConnectionsPerUser { get; init; } = 10;
    public int MaximumSubscriptionsPerConnection { get; init; } = 50;
    public int CommandsPerMinute { get; init; } = 120;

    public void Validate()
    {
        if (TypingRefreshInterval <= TimeSpan.Zero || TypingTtl <= TypingRefreshInterval ||
            TypingTtl > TimeSpan.FromMinutes(1) || HeartbeatInterval <= TimeSpan.Zero ||
            ConnectionLease <= HeartbeatInterval || ConnectionLease > TimeSpan.FromMinutes(5) ||
            RecentlyWindow <= TimeSpan.Zero || LastSeenRetention < RecentlyWindow || LastSeenRetention > TimeSpan.FromDays(7) ||
            MaximumConnections is < 1 or > 100000 || MaximumConnectionsPerUser is < 1 or > 100 ||
            MaximumSubscriptionsPerConnection is < 1 or > 500 || CommandsPerMinute is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(MessagingEphemeralOptions), "تنظیم‌های وضعیت گذرا معتبر نیستند.");
    }
}

public interface IMessagingEphemeralState
{
    bool TryBeginDispatch();
    void EndDispatch();
    void Connect(EphemeralConnection connection, DateTime nowUtc);
    void Consume(EphemeralConnection connection, DateTime nowUtc);
    void Heartbeat(string connectionId, DateTime nowUtc);
    void SubscribeConversation(string connectionId, Guid conversationId);
    void WatchPresence(string connectionId, Guid targetUserId);
    void UnwatchPresence(string connectionId, Guid targetUserId);
    void UnsubscribeConversation(string connectionId, Guid conversationId, DateTime nowUtc);
    void Disconnect(string connectionId, DateTime nowUtc);
    void SetTyping(string connectionId, Guid conversationId, bool isTyping, DateTime nowUtc);
    void StopTyping(Guid userId, Guid sessionId, Guid conversationId, DateTime nowUtc);
    IReadOnlyCollection<EphemeralConnection> Connections(DateTime nowUtc);
    IReadOnlyCollection<EphemeralConnection> ConversationObservers(Guid conversationId, DateTime nowUtc);
    IReadOnlyCollection<EphemeralConnection> PresenceObservers(Guid userId, DateTime nowUtc);
    EphemeralPresenceSnapshot Presence(Guid userId, IReadOnlySet<Guid> validSessions, DateTime nowUtc);
    IReadOnlyCollection<EphemeralChange> Drain(DateTime nowUtc, IReadOnlySet<Guid> validSessions, int limit);
}

public interface IMessagingEphemeralAccessStore
{
    Task AuthorizeTypingAsync(Guid userId, Guid conversationId, AccessContext context, DateTime nowUtc, CancellationToken cancellationToken);
    Task AuthorizeTypingRecipientAsync(Guid userId, Guid sourceUserId, Guid conversationId, AccessContext context, DateTime nowUtc, CancellationToken cancellationToken);
    Task<PresenceVisibility> GetPresenceVisibilityAsync(Guid userId, Guid targetUserId, AccessContext context, DateTime nowUtc, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> GetActiveSessionsAsync(IReadOnlyCollection<Guid> sessionIds, DateTime nowUtc, CancellationToken cancellationToken);
}

public interface IMessagingEphemeralNotifier
{
    Task TypingChangedAsync(IReadOnlyCollection<string> connectionIds, TypingNotification notification, CancellationToken cancellationToken);
    Task PresenceChangedAsync(string connectionId, PresenceResult notification, CancellationToken cancellationToken);
}
