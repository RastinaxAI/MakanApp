using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

public sealed class SignalRMessagingEphemeralNotifier(IHubContext<MessagingHub, IMessagingHubClient> hub) : IMessagingEphemeralNotifier
{
    public Task TypingChangedAsync(IReadOnlyCollection<string> connectionIds, TypingNotification notification, CancellationToken cancellationToken) =>
        hub.Clients.Clients(connectionIds).TypingChanged(notification).WaitAsync(cancellationToken);

    public Task PresenceChangedAsync(string connectionId, PresenceResult notification, CancellationToken cancellationToken) =>
        hub.Clients.Client(connectionId).PresenceChanged(notification).WaitAsync(cancellationToken);
}
