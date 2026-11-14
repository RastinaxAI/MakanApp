using MakanApp.Domain.Academic;
using MakanApp.Domain.Notification;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Notification;

public sealed partial class ProductNotificationWriter
{
    // فقط Storeهای مالک کلاس این capture را در transaction موجود و پیش از Save صدا می‌زنند.
    public async Task CapturePersonalClassesAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case PersonalClassRequest request when entry.State == EntityState.Modified && entry.Property(nameof(PersonalClassRequest.Status)).IsModified &&
                    request.Status is PersonalClassRequestStatus.UnderReview or PersonalClassRequestStatus.NeedsChanges or PersonalClassRequestStatus.Approved or PersonalClassRequestStatus.Rejected:
                    Emit("personal-class-request:" + request.Status, request.Id, request.RowVersion, request.RequesterUserId,
                        NotificationType.PersonalClassRequestUpdated, NotificationResourceType.PersonalClassRequest, request.Id,
                        "وضعیت درخواست کلاس", "وضعیت درخواست تشکیل کلاس شما به‌روز شد.");
                    break;
                case PersonalClassInvitation invitation when entry.State == EntityState.Added:
                    Emit("personal-class-invitation", invitation.Id, invitation.RowVersion, invitation.DestinationUserId,
                        NotificationType.PersonalClassInvitation, NotificationResourceType.PersonalClassInvitation, invitation.Id,
                        "دعوت کلاس مستقل", "دعوت جدیدی برای عضویت در کلاس دارید.");
                    break;
                case PersonalClassJoinRequest join when entry.State == EntityState.Added:
                    var classroom = await ClassAsync(join.PersonalClassId, ct);
                    if (classroom.ManagerUserId is Guid manager)
                        Emit("personal-class-join:Pending", join.Id, join.RowVersion, manager,
                            NotificationType.PersonalClassJoinRequestUpdated, NotificationResourceType.PersonalClassJoinRequest, join.Id,
                            "درخواست عضویت کلاس", "درخواست عضویت جدیدی برای بررسی دارید.");
                    break;
                case PersonalClassJoinRequest join when entry.Property(nameof(PersonalClassJoinRequest.Status)).IsModified &&
                    join.Status is PersonalClassJoinRequestStatus.Approved or PersonalClassJoinRequestStatus.Rejected:
                    Emit("personal-class-join:" + join.Status, join.Id, join.RowVersion, join.UserId,
                        NotificationType.PersonalClassJoinRequestUpdated, NotificationResourceType.PersonalClassJoinRequest, join.Id,
                        "نتیجه درخواست عضویت", "وضعیت درخواست عضویت شما به‌روز شد.");
                    break;
                case PersonalClassManagerTransfer transfer when entry.State == EntityState.Added || entry.Property(nameof(PersonalClassManagerTransfer.Status)).IsModified:
                    var recipients = transfer.Status == PersonalClassManagerTransferStatus.Pending
                        ? new[] { transfer.TargetUserId }
                        : new[] { transfer.RequestedByUserId, transfer.TargetUserId };
                    foreach (var recipient in recipients.Distinct())
                        Emit("personal-class-transfer:" + transfer.Status, transfer.Id, transfer.RowVersion, recipient,
                            NotificationType.PersonalClassManagerTransferUpdated, NotificationResourceType.PersonalClassManagerTransfer, transfer.Id,
                            "انتقال مسئولیت کلاس", "وضعیت انتقال مسئولیت کلاس به‌روز شد.");
                    break;
                case PersonalClassParticipant member when entry.State == EntityState.Added || entry.Property(nameof(PersonalClassParticipant.EndedAtUtc)).IsModified ||
                    entry.Property(nameof(PersonalClassParticipant.LocalRoles)).IsModified || entry.Property(nameof(PersonalClassParticipant.DelegatedCapabilities)).IsModified:
                    Emit($"personal-class-member:{member.LocalRoles}:{member.DelegatedCapabilities}:{member.EndedAtUtc.HasValue}", member.Id,
                        member.RowVersion, member.StudentUserId, NotificationType.PersonalClassMembershipChanged,
                        NotificationResourceType.PersonalClassMembership, member.Id, "تغییر دسترسی کلاس", "وضعیت عضویت یا نقش محلی شما تغییر کرده است.");
                    break;
                case PersonalClass state when entry.State == EntityState.Modified && entry.Property(nameof(PersonalClass.State)).IsModified:
                    var members = await db.PersonalClassParticipants.Where(m => m.PersonalClassId == state.Id && m.EndedAtUtc == null)
                        .Select(m => m.StudentUserId).ToArrayAsync(ct);
                    foreach (var recipient in members.Distinct())
                        Emit("personal-class-state:" + state.State, state.Id, state.RowVersion, recipient,
                            NotificationType.PersonalClassStateChanged, NotificationResourceType.PersonalClass, state.Id,
                            "وضعیت کلاس مستقل", "وضعیت کلاس مستقل شما تغییر کرده است.");
                    break;
            }
        }
    }

    private async Task<PersonalClass> ClassAsync(Guid id, CancellationToken ct) => db.PersonalClasses.Local.SingleOrDefault(c => c.Id == id) ??
        await db.PersonalClasses.AsNoTracking().SingleAsync(c => c.Id == id, ct);
}
