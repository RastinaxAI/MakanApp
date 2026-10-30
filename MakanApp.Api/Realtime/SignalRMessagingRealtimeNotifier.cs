using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

public sealed class SignalRMessagingRealtimeNotifier(
    IHubContext<MessagingHub, IMessagingHubClient> hubContext,
    IMessagingRealtimeAudienceResolver audienceResolver,
    TimeProvider timeProvider,
    MakanApp.Application.Realtime.IRealtimeSessionScope sessionScope) : IMessagingRealtimeNotifier
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
        string? groupRevision = null;
        await foreach (var sessionId in sessions.WithCancellation(cancellationToken))
        {
            var revision = await sessionScope.AuthorizeConversationAsync(sessionId, notification, cancellationToken);
            if (revision is null) continue;
            if (groups.Count > 0 && groupRevision != revision)
            {
                await hubContext.Clients.Groups(groups.ToArray()).ConversationChanged(notification with { ContextRevision = groupRevision }).WaitAsync(cancellationToken);
                groups.Clear();
            }
            groupRevision = revision;
            groups.Add(MessagingRealtimeGroups.Session(sessionId));
            if (groups.Count < MaximumGroupBatchSize) continue;
            await hubContext.Clients.Groups(groups.ToArray()).ConversationChanged(notification with { ContextRevision = groupRevision }).WaitAsync(cancellationToken);
            groups.Clear();
        }
        if (groups.Count > 0)
            await hubContext.Clients.Groups(groups.ToArray()).ConversationChanged(notification with { ContextRevision = groupRevision }).WaitAsync(cancellationToken);
    }
}
