using MakanApp.Application.Messaging;

namespace MakanApp.Infrastructure.Messaging;

// فقط وضعیت کوتاه‌عمر یک process؛ جایگزین تاریخچه، Outbox یا مخزن مشترک چندسروری نیست.
public sealed class InMemoryMessagingEphemeralState(MessagingEphemeralOptions options) : IMessagingEphemeralState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ConnectionState> _connections = [];
    private readonly Dictionary<Guid, DateTime> _lastSeen = [];
    private readonly Dictionary<(Guid UserId, Guid? ConversationId), EphemeralConnection?> _pending = [];
    private int _dispatching;

    public bool TryBeginDispatch() => Interlocked.CompareExchange(ref _dispatching, 1, 0) == 0;
    public void EndDispatch() => Volatile.Write(ref _dispatching, 0);

    public void Connect(EphemeralConnection connection, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (_connections.Count >= options.MaximumConnections ||
                _connections.Values.Count(c => c.Identity.UserId == connection.UserId) >= options.MaximumConnectionsPerUser)
                throw Limited();
            _connections.Add(connection.ConnectionId, new(connection, nowUtc));
            Remember(connection.UserId, nowUtc);
            Changed(connection.UserId, null);
        }
    }

    public void Consume(EphemeralConnection connection, DateTime nowUtc)
    {
        lock (_gate)
        {
            var state = Get(connection.ConnectionId);
            if (state.Identity != connection || state.LastHeartbeat.Add(options.ConnectionLease) <= nowUtc)
                throw Denied();
            if (nowUtc >= state.WindowStart.AddMinutes(1)) { state.WindowStart = nowUtc; state.Commands = 0; }
            if (++state.Commands > options.CommandsPerMinute) throw Limited();
        }
    }

    public void Heartbeat(string connectionId, DateTime nowUtc)
    {
        lock (_gate)
        {
            var state = Get(connectionId);
            if (nowUtc < state.LastHeartbeat.Add(options.HeartbeatInterval)) return;
            state.LastHeartbeat = nowUtc;
            Remember(state.Identity.UserId, nowUtc);
            Changed(state.Identity.UserId, null);
        }
    }

    public void SubscribeConversation(string connectionId, Guid conversationId)
    {
        lock (_gate) AddSubscription(Get(connectionId).Conversations, conversationId);
    }

    public void WatchPresence(string connectionId, Guid targetUserId)
    {
        lock (_gate) AddSubscription(Get(connectionId).PresenceTargets, targetUserId);
    }

    public void UnwatchPresence(string connectionId, Guid targetUserId)
    {
        lock (_gate)
            if (_connections.TryGetValue(connectionId, out var connection)) connection.PresenceTargets.Remove(targetUserId);
    }

    public void UnsubscribeConversation(string connectionId, Guid conversationId, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out var state)) return;
            state.Conversations.Remove(conversationId);
            if (state.Typing.Remove(conversationId)) Changed(state.Identity.UserId, conversationId, state.Identity);
        }
    }

    public void Disconnect(string connectionId, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (!_connections.Remove(connectionId, out var state)) return;
            foreach (var id in state.Typing.Keys) Changed(state.Identity.UserId, id, state.Identity);
            Remember(state.Identity.UserId, state.LastHeartbeat.Add(options.ConnectionLease) > nowUtc ? nowUtc : state.LastHeartbeat);
            Changed(state.Identity.UserId, null);
        }
    }

    public void SetTyping(string connectionId, Guid conversationId, bool isTyping, DateTime nowUtc)
    {
        lock (_gate)
        {
            var state = Get(connectionId);
            if (!state.Conversations.Contains(conversationId)) throw Denied();
            if (isTyping)
            {
                if (state.Typing.TryGetValue(conversationId, out var prior) && nowUtc < prior.Add(options.TypingRefreshInterval)) return;
                state.Typing[conversationId] = nowUtc;
            }
            else if (!state.Typing.Remove(conversationId)) return;
            Changed(state.Identity.UserId, conversationId, state.Identity);
        }
    }

    public void StopTyping(Guid userId, Guid sessionId, Guid conversationId, DateTime nowUtc)
    {
        lock (_gate)
            foreach (var state in _connections.Values.Where(c => c.Identity.UserId == userId && c.Identity.SessionId == sessionId))
                if (state.Typing.Remove(conversationId)) Changed(userId, conversationId, state.Identity);
    }

    public IReadOnlyCollection<EphemeralConnection> Connections(DateTime nowUtc)
    {
        lock (_gate) return Active(nowUtc).Select(c => c.Identity).ToArray();
    }

    public IReadOnlyCollection<EphemeralConnection> ConversationObservers(Guid conversationId, DateTime nowUtc)
    {
        lock (_gate) return Active(nowUtc).Where(c => c.Conversations.Contains(conversationId)).Select(c => c.Identity).ToArray();
    }

    public IReadOnlyCollection<EphemeralConnection> PresenceObservers(Guid userId, DateTime nowUtc)
    {
        lock (_gate) return Active(nowUtc).Where(c => c.PresenceTargets.Contains(userId)).Select(c => c.Identity).ToArray();
    }

    public EphemeralPresenceSnapshot Presence(Guid userId, IReadOnlySet<Guid> validSessions, DateTime nowUtc)
    {
        lock (_gate)
        {
            var online = Active(nowUtc).Any(c => c.Identity.UserId == userId && validSessions.Contains(c.Identity.SessionId));
            var seen = _lastSeen.TryGetValue(userId, out var value) && value.Add(options.LastSeenRetention) > nowUtc ? (DateTime?)value : null;
            return new(online, seen);
        }
    }

    public IReadOnlyCollection<EphemeralChange> Drain(DateTime nowUtc, IReadOnlySet<Guid> validSessions, int limit)
    {
        lock (_gate)
        {
            foreach (var state in _connections.Values.ToArray())
            {
                if (state.LastHeartbeat.Add(options.ConnectionLease) <= nowUtc || !validSessions.Contains(state.Identity.SessionId))
                {
                    _connections.Remove(state.Identity.ConnectionId);
                    foreach (var id in state.Typing.Keys) Changed(state.Identity.UserId, id, state.Identity);
                    Changed(state.Identity.UserId, null);
                    continue;
                }
                foreach (var item in state.Typing.Where(t => t.Value.Add(options.TypingTtl) <= nowUtc).ToArray())
                { state.Typing.Remove(item.Key); Changed(state.Identity.UserId, item.Key, state.Identity); }
            }
            foreach (var item in _lastSeen.Where(s => s.Value.Add(options.LastSeenRetention) <= nowUtc).ToArray()) _lastSeen.Remove(item.Key);
            var results = _pending.Take(limit).Select(item =>
            {
                var sources = item.Key.ConversationId is Guid id ? Active(nowUtc).Where(c => c.Identity.UserId == item.Key.UserId &&
                    c.Typing.TryGetValue(id, out var at) && at.Add(options.TypingTtl) > nowUtc).ToArray() : [];
                var expires = sources.Length == 0 ? nowUtc : sources.Max(c => c.Typing[item.Key.ConversationId!.Value]).Add(options.TypingTtl);
                return new EphemeralChange(item.Key.UserId, item.Key.ConversationId, sources.Length > 0, expires,
                    sources.Length > 0 ? sources.Select(c => c.Identity).ToArray() : item.Value is not null ? [item.Value] : []);
            }).ToArray();
            foreach (var item in results) _pending.Remove((item.UserId, item.ConversationId));
            return results;
        }
    }

    private IEnumerable<ConnectionState> Active(DateTime nowUtc) => _connections.Values.Where(c => c.LastHeartbeat.Add(options.ConnectionLease) > nowUtc);
    private ConnectionState Get(string id) => _connections.TryGetValue(id, out var value) ? value : throw Denied();
    private void Changed(Guid userId, Guid? conversationId, EphemeralConnection? origin = null)
    {
        var key = (userId, conversationId);
        // hintهای گذرا coalesce و bounded هستند؛ در فشار، TTL کلاینت مرجع پاک‌شدن وضعیت است.
        if (!_pending.ContainsKey(key) && _pending.Count >= options.MaximumConnections) _pending.Remove(_pending.First().Key);
        _pending[key] = origin;
    }
    private void AddSubscription(HashSet<Guid> items, Guid id)
    {
        if (id == Guid.Empty) throw Denied();
        if (!items.Contains(id) && items.Count >= options.MaximumSubscriptionsPerConnection) throw Limited();
        items.Add(id);
    }
    private void Remember(Guid userId, DateTime nowUtc)
    {
        if (!_lastSeen.ContainsKey(userId) && _lastSeen.Count >= options.MaximumConnections) _lastSeen.Remove(_lastSeen.MinBy(s => s.Value).Key);
        if (!_lastSeen.TryGetValue(userId, out var prior) || nowUtc > prior) _lastSeen[userId] = nowUtc;
    }
    private static MessagingException Denied() => new(MessagingErrorCodes.RealtimeSubscriptionNotAllowed, "عملیات وضعیت گذرا مجاز نیست.");
    private static MessagingException Limited() => new(MessagingErrorCodes.ActivityRateLimited, "بودجه یا ظرفیت وضعیت گذرا پر شده است.");

    private sealed class ConnectionState(EphemeralConnection identity, DateTime nowUtc)
    {
        public EphemeralConnection Identity { get; } = identity;
        public DateTime LastHeartbeat { get; set; } = nowUtc;
        public DateTime WindowStart { get; set; } = nowUtc;
        public int Commands { get; set; }
        public HashSet<Guid> Conversations { get; } = [];
        public HashSet<Guid> PresenceTargets { get; } = [];
        public Dictionary<Guid, DateTime> Typing { get; } = [];
    }
}
