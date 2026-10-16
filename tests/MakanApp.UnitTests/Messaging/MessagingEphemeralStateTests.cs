using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingEphemeralStateTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static EphemeralConnection Connection(string id, Guid? userId = null) => new(id, userId ?? Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void TypingExpiresOnServerClockWithoutDurableState()
    {
        var store = new InMemoryMessagingEphemeralState(new()); var connection = Connection("one"); var conversationId = Guid.NewGuid();
        store.Connect(connection, Start); store.SubscribeConversation(connection.ConnectionId, conversationId);
        store.SetTyping(connection.ConnectionId, conversationId, true, Start);
        Assert.True(Assert.Single(store.Drain(Start, new HashSet<Guid> { connection.SessionId }, 10), c => c.ConversationId == conversationId).IsTyping);
        var expired = Assert.Single(store.Drain(Start.AddSeconds(6), new HashSet<Guid> { connection.SessionId }, 10));
        Assert.False(expired.IsTyping); Assert.Equal(Start.AddSeconds(6), expired.ExpiresAtUtc);
        Assert.Empty(new InMemoryMessagingEphemeralState(new()).Connections(Start));
    }

    [Fact]
    public void TypingIsAggregatedAcrossConnectionsAndFalseOnlyStopsItsOwnerConnection()
    {
        var store = new InMemoryMessagingEphemeralState(new()); var userId = Guid.NewGuid();
        var one = Connection("one", userId); var two = Connection("two", userId); var conversationId = Guid.NewGuid();
        foreach (var c in new[] { one, two }) { store.Connect(c, Start); store.SubscribeConversation(c.ConnectionId, conversationId); store.SetTyping(c.ConnectionId, conversationId, true, Start); }
        store.SetTyping(one.ConnectionId, conversationId, false, Start.AddSeconds(1));
        var active = Assert.Single(store.Drain(Start.AddSeconds(1), new HashSet<Guid> { one.SessionId, two.SessionId }, 10), c => c.ConversationId == conversationId);
        Assert.True(active.IsTyping); Assert.Equal(two, Assert.Single(active.TypingSources));
        store.Disconnect(two.ConnectionId, Start.AddSeconds(2));
        Assert.False(Assert.Single(store.Drain(Start.AddSeconds(2), new HashSet<Guid> { one.SessionId }, 10), c => c.ConversationId == conversationId).IsTyping);
    }

    [Fact]
    public void OneDisconnectDoesNotMakeAnotherSessionOfflineAndLeaseEventuallyExpires()
    {
        var store = new InMemoryMessagingEphemeralState(new()); var userId = Guid.NewGuid(); var one = Connection("one", userId); var two = Connection("two", userId);
        store.Connect(one, Start); store.Connect(two, Start); store.Disconnect(one.ConnectionId, Start.AddSeconds(1));
        Assert.True(store.Presence(userId, new HashSet<Guid> { two.SessionId }, Start.AddSeconds(2)).IsOnline);
        Assert.False(store.Presence(userId, new HashSet<Guid> { two.SessionId }, Start.AddSeconds(45)).IsOnline);
        Assert.False(store.Presence(userId, new HashSet<Guid>(), Start.AddSeconds(2)).IsOnline);
    }

    [Fact]
    public void RefreshIsThrottledAndTypingDoesNotRenewPresenceLease()
    {
        var store = new InMemoryMessagingEphemeralState(new()); var connection = Connection("one"); var conversationId = Guid.NewGuid();
        var valid = new HashSet<Guid> { connection.SessionId };
        store.Connect(connection, Start); store.SubscribeConversation(connection.ConnectionId, conversationId);
        store.SetTyping(connection.ConnectionId, conversationId, true, Start); store.Drain(Start, valid, 10);
        store.SetTyping(connection.ConnectionId, conversationId, true, Start.AddSeconds(1));
        Assert.Empty(store.Drain(Start.AddSeconds(1), valid, 10));
        store.SetTyping(connection.ConnectionId, conversationId, true, Start.AddSeconds(4));
        Assert.Equal(Start.AddSeconds(10), Assert.Single(store.Drain(Start.AddSeconds(4), valid, 10)).ExpiresAtUtc);
        Assert.Throws<MessagingException>(() => store.Consume(connection, Start.AddSeconds(45)));
    }

    [Fact]
    public void CommandBudgetConnectionOwnerAndSubscriptionCapsAreEnforced()
    {
        var store = new InMemoryMessagingEphemeralState(new() { CommandsPerMinute = 1, MaximumSubscriptionsPerConnection = 1 });
        var connection = Connection("one"); store.Connect(connection, Start);
        Assert.Equal(MessagingErrorCodes.RealtimeSubscriptionNotAllowed,
            Assert.Throws<MessagingException>(() => store.Consume(connection with { UserId = Guid.NewGuid() }, Start)).Code);
        store.Consume(connection, Start);
        Assert.Equal("MESSAGING_ACTIVITY_RATE_LIMITED", Assert.Throws<MessagingException>(() => store.Consume(connection, Start)).Code);
        store.SubscribeConversation(connection.ConnectionId, Guid.NewGuid());
        Assert.Throws<MessagingException>(() => store.SubscribeConversation(connection.ConnectionId, Guid.NewGuid()));
    }

    [Fact]
    public void PendingHintsAreBoundedAndDispatcherOwnershipIsExclusive()
    {
        var store = new InMemoryMessagingEphemeralState(new() { MaximumConnections = 2 });
        for (var index = 0; index < 4; index++) { var c = Connection(index.ToString()); store.Connect(c, Start); store.Disconnect(c.ConnectionId, Start); }
        Assert.InRange(store.Drain(Start, new HashSet<Guid>(), 10).Count, 0, 2);
        Assert.True(store.TryBeginDispatch()); Assert.False(store.TryBeginDispatch()); store.EndDispatch(); Assert.True(store.TryBeginDispatch()); store.EndDispatch();
    }

    [Fact]
    public void PrivacyDefaultsAreApprovedContactsAndLegacyMutationPreservesNewSettings()
    {
        var privacy = MessagingPrivacySettings.Create(Guid.NewGuid(), PrivacyAudience.Everyone, PrivacyAudience.Nobody,
            PrivacyAudience.Nobody, PrivacyAudience.Contacts, Start);
        Assert.Equal(PrivacyAudience.Contacts, privacy.OnlineAudience); Assert.Equal(PrivacyAudience.Contacts, privacy.LastSeenAudience);
        privacy.UpdatePresenceAudiences(PrivacyAudience.Nobody, PrivacyAudience.Nobody);
        privacy.UpdatePresenceAudiences(null, null);
        Assert.Equal(PrivacyAudience.Nobody, privacy.OnlineAudience); Assert.Equal(PrivacyAudience.Nobody, privacy.LastSeenAudience);
        Assert.Throws<ArgumentOutOfRangeException>(() => privacy.UpdatePresenceAudiences((PrivacyAudience)500, null));
    }

    [Fact]
    public void InvalidLeaseAndRefreshConfigurationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessagingEphemeralOptions { TypingTtl = TimeSpan.FromSeconds(3) }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessagingEphemeralOptions { ConnectionLease = TimeSpan.FromSeconds(5) }.Validate());
        new MessagingEphemeralOptions().Validate();
    }
}
