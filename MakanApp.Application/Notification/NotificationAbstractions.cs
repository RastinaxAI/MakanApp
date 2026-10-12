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
