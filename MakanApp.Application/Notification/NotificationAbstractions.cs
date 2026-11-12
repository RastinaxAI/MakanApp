using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public interface INotificationStore
{
    Task<(IReadOnlyCollection<NotificationItem> Items, int TotalCount)> GetPageAsync(
        Guid userId,
        NotificationQuery query,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken);

    Task<Domain.Notification.Notification?> GetForUpdateAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task<NotificationTargetResult> ResolveTargetAsync(Guid userId, Guid sessionId, Guid notificationId, CancellationToken ct);
    Task<NotificationPage> GetPageAsync(
        Guid userId,
        NotificationQuery query,
        CancellationToken cancellationToken);

    Task<NotificationUnreadCount> GetUnreadCountAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken);

    Task<NotificationItem> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken);
}

public sealed record NotificationTargetResult(string Kind, Guid ResourceId, string ApiRoute, Guid? RequiredOrganizationId);
public interface INotificationTargetResolver
{
    Task<NotificationTargetResult?> ResolveAsync(Guid user, Guid session, Domain.Notification.Notification notification, CancellationToken ct);
}
