using MakanApp.Application.Messaging;

namespace MakanApp.Application.Realtime;

public interface IAccountInboxRealtimeService
{
    Task<AccountChangePageResult> ReadAsync(Guid userId, Guid sessionId, string? cursor, int limit, CancellationToken ct);
    IAsyncEnumerable<(Guid SessionId, RealtimeInvalidation Notification)> ResolveAsync(
        MessagingRealtimeNotification notification, Guid? audienceUserId, CancellationToken ct);
}
