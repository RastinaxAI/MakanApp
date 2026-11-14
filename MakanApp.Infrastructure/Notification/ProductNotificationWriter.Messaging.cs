using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Notification;

public sealed partial class ProductNotificationWriter
{
    public async Task CaptureMessagingInvitationsAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        foreach (var entry in entries)
        {
            if (entry.Entity is ConversationInvitation invitation && entry.State == EntityState.Added)
                Emit("conversation-invitation", invitation.Id, invitation.RowVersion, invitation.TargetUserId,
                    NotificationType.ConversationInvitation, NotificationResourceType.ConversationInvitation, invitation.Id,
                    "دعوت گفتگو", "دعوت جدیدی برای پیوستن به گفتگو دارید.");
            else if (entry.Entity is ConversationJoinRequest request)
            {
                if (entry.State == EntityState.Added)
                {
                    var managers = await db.ConversationParticipants.Where(p => p.ConversationId == request.ConversationId &&
                        p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null &&
                        (p.Role == ConversationParticipantRole.Owner || p.Role == ConversationParticipantRole.Admin &&
                            (p.AdminPermissions & ConversationAdminPermission.ManageInvites) != 0)).Select(p => p.UserId).Distinct().ToArrayAsync(ct);
                    foreach (var recipient in managers)
                        Emit("conversation-join:Pending", request.Id, request.RowVersion, recipient,
                            NotificationType.ConversationJoinRequestUpdated, NotificationResourceType.ConversationJoinRequest, request.Id,
                            "درخواست پیوستن", "درخواست جدیدی برای پیوستن به گفتگو دارید.");
                }
                else if (entry.Property(nameof(ConversationJoinRequest.Status)).IsModified &&
                    request.Status is ConversationJoinRequestStatus.Approved or ConversationJoinRequestStatus.Rejected)
                    Emit("conversation-join:" + request.Status, request.Id, request.RowVersion, request.UserId,
                        NotificationType.ConversationJoinRequestUpdated, NotificationResourceType.ConversationJoinRequest, request.Id,
                        "نتیجه درخواست پیوستن", "وضعیت درخواست پیوستن شما به‌روز شد.");
            }
        }
    }
}
