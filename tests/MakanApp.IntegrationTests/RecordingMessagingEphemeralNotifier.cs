using System.Collections.Concurrent;
using MakanApp.Application.Messaging;
using MakanApp.Api.Realtime;

namespace MakanApp.IntegrationTests;

public sealed class MessagingEphemeralTestControl
{
    public ConcurrentQueue<(string[] Connections, TypingNotification Value)> Typing { get; } = new();
    public ConcurrentQueue<(string Connection, PresenceResult Value)> Presence { get; } = new();
}

public sealed class RecordingMessagingEphemeralNotifier(SignalRMessagingEphemeralNotifier actual, MessagingEphemeralTestControl control)
    : IMessagingEphemeralNotifier
{
    public Task TypingChangedAsync(IReadOnlyCollection<string> connectionIds, TypingNotification notification, CancellationToken cancellationToken)
    {
        control.Typing.Enqueue((connectionIds.ToArray(), notification));
        return actual.TypingChangedAsync(connectionIds, notification, cancellationToken);
    }

    public Task PresenceChangedAsync(string connectionId, PresenceResult notification, CancellationToken cancellationToken)
    {
        control.Presence.Enqueue((connectionId, notification));
        return actual.PresenceChangedAsync(connectionId, notification, cancellationToken);
    }
}
