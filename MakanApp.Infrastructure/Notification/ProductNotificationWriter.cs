using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Notification;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Notification;

// فقط Storeهای مالک، پیش از Save موجود این writer را فراخوانی می‌کنند؛ hook عمومی DbContext ساخته نمی‌شود.
public sealed class ProductNotificationWriter(MakanDbContext db, IOutbox outbox, TimeProvider clock)
{
    public async Task CaptureOrganizationAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        var changedMemberships = entries.Where(e => e.Entity is Membership && (e.State == EntityState.Added || e.Property(nameof(Membership.Status)).IsModified))
            .Select(e => ((Membership)e.Entity).Id).ToHashSet();
        foreach (var entry in entries)
        {
            if (entry.Entity is Invitation invitation && entry.State == EntityState.Added && invitation.Status == InvitationStatus.Pending)
                Emit("organization-invitation", invitation.Id, invitation.RowVersion, invitation.DestinationUserId,
                    NotificationType.OrganizationInvitation, NotificationResourceType.OrganizationInvitation, invitation.Id, "دعوت جدید", "دعوت جدیدی برای شما ثبت شده است.");
            else if (entry.Entity is Membership member && changedMemberships.Contains(member.Id))
                Emit("membership:" + member.Status, member.Id, member.RowVersion, member.UserId, NotificationType.MembershipChanged,
                    NotificationResourceType.Membership, member.Id, "تغییر دسترسی", "وضعیت دسترسی سازمانی شما تغییر کرده است.");
            else if (entry.Entity is RoleAssignment role && !changedMemberships.Contains(role.MembershipId) &&
                (entry.State == EntityState.Added || entry.Property(nameof(RoleAssignment.Status)).IsModified))
            {
                var membership = db.Memberships.Local.SingleOrDefault(m => m.Id == role.MembershipId) ??
                    await db.Memberships.AsNoTracking().SingleAsync(m => m.Id == role.MembershipId, ct);
                Emit("role:" + role.Status, role.Id, role.RowVersion, membership.UserId, NotificationType.MembershipChanged,
                    NotificationResourceType.Membership, membership.Id, "تغییر دسترسی", "وضعیت نقش سازمانی شما تغییر کرده است.");
            }
        }
    }
    public void CapturePaidOrders()
    {
        db.ChangeTracker.DetectChanges();
        foreach (var entry in db.ChangeTracker.Entries<Order>().Where(e => e.Entity.Status == OrderStatus.Paid &&
            (e.State == EntityState.Added || e.State == EntityState.Modified && e.Property(o => o.Status).IsModified)).ToArray())
            Emit("order-paid", entry.Entity.Id, entry.Entity.RowVersion, entry.Entity.PurchaserUserId, NotificationType.OrderPaid,
                NotificationResourceType.Order, entry.Entity.Id, "وضعیت سفارش", "پرداخت سفارش شما در سرور تأیید شد.");
    }
    public void SupportUpdated(Guid operation, Guid ticket, Guid recipient) => Emit("support-updated", operation, [], recipient,
        NotificationType.SupportUpdated, NotificationResourceType.SupportTicket, ticket, "به‌روزرسانی پشتیبانی", "پروندهٔ پشتیبانی شما به‌روز شد.");
    public void SchoolUpdated(Guid operation, Guid application, Guid recipient) => Emit("school-updated", operation, [], recipient,
        NotificationType.SchoolApplicationUpdated, NotificationResourceType.SchoolApplication, application, "وضعیت درخواست تأسیس", "وضعیت درخواست تأسیس آموزشگاه شما به‌روز شد.");
    private void Emit(string kind, Guid source, byte[] version, Guid recipient, NotificationType type, NotificationResourceType resource, Guid id, string title, string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"product-notification:v1:{kind}:{source:N}:{resource}:{id:N}:{recipient:N}:{Convert.ToHexString(version)}"));
        var eventId = new Guid(bytes.AsSpan(0, 16));
        outbox.EnqueueRange(NotificationOutboxContract.Create(new(eventId, null, NotificationCategory.AccountSystem, type, resource, id,
            clock.GetUtcNow().UtcDateTime, [new(recipient, title, body)])));
    }
}
