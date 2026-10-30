using MakanApp.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

public sealed class SignalRInvalidationNotifier(IHubContext<MessagingHub, IMessagingHubClient> hub,
    IRealtimeInvalidationAudience audience, MakanApp.Infrastructure.Observability.RealtimeTelemetry telemetry) : IRealtimeInvalidationNotifier
{
    public async Task NotifyAsync(Guid eventId, string payload, CancellationToken ct)
    {
        await foreach (var recipient in audience.ResolveAsync(eventId, payload, ct).WithCancellation(ct))
        {
            await hub.Clients.Group(MessagingRealtimeGroups.Session(recipient.SessionId))
                .ClientInvalidated(recipient.Notification).WaitAsync(ct);
            telemetry.Delivered();
        }
    }
}
