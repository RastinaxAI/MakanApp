using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

public sealed class SignalRMessagingRealtimeNotifier(
    IHubContext<MessagingHub, IMessagingHubClient> hubContext,
    IMessagingRealtimeAudienceResolver audienceResolver,
    TimeProvider timeProvider) : IMessagingRealtimeNotifier
{
    public const int MaximumGroupBatchSize = 128;

    public async Task NotifyConversationChangedAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        CancellationToken cancellationToken)
    {
        var sessions = audienceResolver.GetActiveSessionIdsAsync(
            notification,
            audienceUserId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        var groups = new List<string>(MaximumGroupBatchSize);
        await foreach (var sessionId in sessions.WithCancellation(cancellationToken))
        {
            groups.Add(MessagingRealtimeGroups.Session(sessionId));
            if (groups.Count < MaximumGroupBatchSize) continue;
            await hubContext.Clients.Groups(groups.ToArray()).ConversationChanged(notification).WaitAsync(cancellationToken);
            groups.Clear();
        }
        if (groups.Count > 0)
            await hubContext.Clients.Groups(groups.ToArray()).ConversationChanged(notification).WaitAsync(cancellationToken);
    }
}
