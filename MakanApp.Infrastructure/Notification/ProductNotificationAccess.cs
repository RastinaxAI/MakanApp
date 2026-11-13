using MakanApp.Application.Platform;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class ProductNotificationAccess(MakanDbContext db, IPlatformPermissionResolver permissions, TimeProvider clock)
{
    public async Task<IQueryable<DomainNotification>> FilterAsync(IQueryable<DomainNotification> source, Guid user, CancellationToken ct)
    {
        var support = await permissions.HasPermissionAsync(user, PlatformPermissionKeys.SupportRead, ct);
        var schools = await permissions.HasPermissionAsync(user, PlatformPermissionKeys.SchoolApplicationsRead, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var orders = ReadableOrders(user);
        return source.Where(n =>
            (n.ResourceType != NotificationResourceType.SupportTicket || db.SupportTickets.Any(t => t.Id == n.ResourceId && (t.OwnerUserId == user || support))) &&
            (n.ResourceType != NotificationResourceType.SchoolApplication || db.SchoolApplications.Any(a => a.Id == n.ResourceId &&
                (a.ApplicantUserId == user || schools && a.Status != SchoolApplicationStatus.Draft && a.SubmittedPolicyVersion != null))) &&
            (n.ResourceType != NotificationResourceType.Order || orders.Any(o => o.Id == n.ResourceId)) &&
            (n.ResourceType != NotificationResourceType.Membership || db.Memberships.Any(m => m.Id == n.ResourceId && m.UserId == user)) &&
            (n.ResourceType != NotificationResourceType.OrganizationInvitation || db.Invitations.Any(i => i.Id == n.ResourceId && i.DestinationUserId == user &&
                i.Status == InvitationStatus.Pending && i.ExpiresAtUtc > now && db.Organizations.Any(o => o.Id == i.OrganizationId && o.Status == OrganizationStatus.Active))));
    }
    public async Task<bool> AllowsAsync(Guid user, NotificationResourceType type, Guid id, CancellationToken ct)
    {
        switch (type)
        {
            case NotificationResourceType.SupportTicket:
                return await db.SupportTickets.AnyAsync(t => t.Id == id && t.OwnerUserId == user, ct) ||
                    await db.SupportTickets.AnyAsync(t => t.Id == id, ct) && await permissions.HasPermissionAsync(user, PlatformPermissionKeys.SupportRead, ct);
            case NotificationResourceType.SchoolApplication:
                return await db.SchoolApplications.AnyAsync(a => a.Id == id && a.ApplicantUserId == user, ct) ||
                    await db.SchoolApplications.AnyAsync(a => a.Id == id && a.Status != SchoolApplicationStatus.Draft && a.SubmittedPolicyVersion != null, ct) &&
                    await permissions.HasPermissionAsync(user, PlatformPermissionKeys.SchoolApplicationsRead, ct);
            case NotificationResourceType.Order:
                return await ReadableOrders(user).AnyAsync(o => o.Id == id, ct);
            case NotificationResourceType.Membership:
                return await db.Memberships.AnyAsync(m => m.Id == id && m.UserId == user, ct);
            case NotificationResourceType.OrganizationInvitation:
                var now = clock.GetUtcNow().UtcDateTime;
                return await db.Invitations.AnyAsync(i => i.Id == id && i.DestinationUserId == user && i.Status == InvitationStatus.Pending &&
                    i.ExpiresAtUtc > now && db.Organizations.Any(o => o.Id == i.OrganizationId && o.Status == OrganizationStatus.Active), ct);
            default: return true;
        }
    }
    private IQueryable<MakanApp.Domain.Commerce.Order> ReadableOrders(Guid user) => db.Orders.Where(o => o.PurchaserUserId == user &&
        (o.OrganizationId == null || db.Organizations.Any(org => org.Id == o.OrganizationId && org.Status == OrganizationStatus.Active) &&
            db.Memberships.Any(m => m.UserId == user && m.OrganizationId == o.OrganizationId && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
                db.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Role == OrganizationRole.Manager && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null))));
}
