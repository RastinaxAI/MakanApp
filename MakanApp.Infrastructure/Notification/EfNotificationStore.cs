using MakanApp.Application.Notification;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class EfNotificationStore(MakanDbContext dbContext, PersonalClassAcademicFeatures features, ProductNotificationAccess resourceAccess) : INotificationStore
{
    private IQueryable<DomainNotification> Visible(Guid userId) => dbContext.Notifications.Where(n =>
        n.RecipientUserId == userId && (n.OrganizationId != null ||
            (features.AssignmentsEnabled || n.ResourceType != NotificationResourceType.Assignment && n.ResourceType != NotificationResourceType.SubmissionAttempt) &&
            (features.ExamsEnabled || n.ResourceType != NotificationResourceType.Exam && n.ResourceType != NotificationResourceType.ExamAttempt)));

    public async Task<(IReadOnlyCollection<NotificationItem> Items, int TotalCount)> GetPageAsync(
        Guid userId,
        NotificationQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = (await resourceAccess.FilterAsync(Visible(userId), userId, cancellationToken)).AsNoTracking();
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

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var query = (await resourceAccess.FilterAsync(Visible(userId), userId, cancellationToken))
            .AsNoTracking()
            .Where(notification =>
                notification.RecipientUserId == userId && notification.ReadAtUtc == null);
        if (organizationId.HasValue)
        {
            query = query.Where(notification => notification.OrganizationId == organizationId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<DomainNotification?> GetForUpdateAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (await resourceAccess.FilterAsync(Visible(userId), userId, cancellationToken)).SingleOrDefaultAsync(
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
