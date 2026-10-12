using MakanApp.Application.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class EfNotificationStore(MakanDbContext dbContext) : INotificationStore
{
    public async Task<(IReadOnlyCollection<NotificationItem> Items, int TotalCount)> GetPageAsync(
        Guid userId,
        NotificationQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId);
        if (query.UnreadOnly)
        {
            filtered = filtered.Where(notification => notification.ReadAtUtc == null);
        }

        if (query.Category.HasValue)
        {
            filtered = filtered.Where(notification => notification.Category == query.Category.Value);
        }

        if (query.OrganizationId.HasValue)
        {
            filtered = filtered.Where(notification => notification.OrganizationId == query.OrganizationId.Value);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(notification => new NotificationItem(
                notification.Id,
                notification.OrganizationId,
                notification.Category,
                notification.Type,
                notification.Title,
                notification.Body,
                notification.ResourceType,
                notification.ResourceId,
                notification.CreatedAtUtc,
                notification.ReadAtUtc))
            .ToArrayAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<int> GetUnreadCountAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.RecipientUserId == userId && notification.ReadAtUtc == null);
        if (organizationId.HasValue)
        {
            query = query.Where(notification => notification.OrganizationId == organizationId.Value);
        }

        return query.CountAsync(cancellationToken);
    }

    public Task<DomainNotification?> GetForUpdateAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Notifications.SingleOrDefaultAsync(
            notification => notification.Id == notificationId &&
                            notification.RecipientUserId == userId,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotificationException(
                NotificationErrorCodes.ConcurrencyConflict,
                "اعلان هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
    }
}
