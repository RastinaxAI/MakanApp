using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public sealed class NotificationService(
    INotificationStore store,
    TimeProvider timeProvider) : INotificationService
{
    public const int MaximumPageSize = 100;

    public async Task<NotificationPage> GetPageAsync(
        Guid userId,
        NotificationQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var (items, totalCount) = await store.GetPageAsync(userId, query, cancellationToken);
        return new NotificationPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<NotificationUnreadCount> GetUnreadCountAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw InvalidQuery();
        }

        return new NotificationUnreadCount(
            await store.GetUnreadCountAsync(userId, organizationId, cancellationToken));
    }

    public async Task<NotificationItem> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await store.GetForUpdateAsync(notificationId, userId, cancellationToken)
            ?? throw new NotificationException(
                NotificationErrorCodes.NotFound,
                "اعلان موردنظر پیدا نشد.");
        notification.MarkRead(timeProvider.GetUtcNow().UtcDateTime);
        await store.SaveChangesAsync(cancellationToken);
        return ToItem(notification);
    }

    private static void ValidateQuery(NotificationQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize ||
            query.OrganizationId == Guid.Empty ||
            (query.Category.HasValue && !Enum.IsDefined(query.Category.Value)) ||
            (long)(query.Page - 1) * query.PageSize > int.MaxValue)
        {
            throw InvalidQuery();
        }
    }

    private static NotificationException InvalidQuery() =>
        new(NotificationErrorCodes.InvalidQuery, "پارامترهای صفحه‌بندی یا فیلتر اعلان معتبر نیستند.");

    private static NotificationItem ToItem(Domain.Notification.Notification notification) =>
        new(
            notification.Id,
            notification.OrganizationId,
            notification.Category,
            notification.Type,
            notification.Title,
            notification.Body,
            notification.ResourceType,
            notification.ResourceId,
            notification.CreatedAtUtc,
            notification.ReadAtUtc);
}
