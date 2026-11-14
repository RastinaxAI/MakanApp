using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed partial class ProductNotificationAccess
{
    private IQueryable<DomainNotification> FilterWorkflowResources(IQueryable<DomainNotification> source, Guid user, DateTime now)
    {
        var invitations = ConversationInvitations(user, now); var joins = ConversationJoins(user);
        var personalInvitations = PersonalInvitations(user, now); var personalJoins = PersonalJoins(user); var transfers = PersonalTransfers(user);
        return source.Where(n =>
        (n.ResourceType != NotificationResourceType.ConversationInvitation || invitations.Any(i => i.Id == n.ResourceId)) &&
        (n.ResourceType != NotificationResourceType.ConversationJoinRequest || joins.Any(i => i.Id == n.ResourceId)) &&
        (n.ResourceType != NotificationResourceType.PersonalClassRequest || db.PersonalClassRequests.Any(r => r.Id == n.ResourceId && r.RequesterUserId == user)) &&
        (n.ResourceType != NotificationResourceType.PersonalClassInvitation || personalInvitations.Any(i => i.Id == n.ResourceId)) &&
        (n.ResourceType != NotificationResourceType.PersonalClassJoinRequest || personalJoins.Any(i => i.Id == n.ResourceId)) &&
        (n.ResourceType != NotificationResourceType.PersonalClassManagerTransfer || transfers.Any(i => i.Id == n.ResourceId)) &&
        (n.ResourceType != NotificationResourceType.PersonalClassMembership || db.PersonalClassParticipants.Any(m => m.Id == n.ResourceId && m.StudentUserId == user)) &&
        (n.ResourceType != NotificationResourceType.PersonalClass || db.PersonalClasses.Any(c => c.Id == n.ResourceId &&
            db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id && m.StudentUserId == user && m.EndedAtUtc == null))));
    }

    private async Task<bool> AllowsWorkflowAsync(Guid user, NotificationResourceType type, Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return type switch
        {
            NotificationResourceType.ConversationInvitation => await ConversationInvitations(user, now).AnyAsync(i => i.Id == id, ct),
            NotificationResourceType.ConversationJoinRequest => await ConversationJoins(user).AnyAsync(i => i.Id == id, ct),
            NotificationResourceType.PersonalClassRequest => await db.PersonalClassRequests.AnyAsync(r => r.Id == id && r.RequesterUserId == user, ct),
            NotificationResourceType.PersonalClassInvitation => await PersonalInvitations(user, now).AnyAsync(i => i.Id == id, ct),
            NotificationResourceType.PersonalClassJoinRequest => await PersonalJoins(user).AnyAsync(i => i.Id == id, ct),
            NotificationResourceType.PersonalClassManagerTransfer => await PersonalTransfers(user).AnyAsync(i => i.Id == id, ct),
            NotificationResourceType.PersonalClassMembership => await db.PersonalClassParticipants.AnyAsync(m => m.Id == id && m.StudentUserId == user, ct),
            NotificationResourceType.PersonalClass => await db.PersonalClasses.AnyAsync(c => c.Id == id &&
                db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id && m.StudentUserId == user && m.EndedAtUtc == null), ct),
            // مجوز منابع آموزشی و پیام در resolver/worker تخصصی خودشان بررسی می‌شود؛ نوع ناشناخته fail-closed است.
            NotificationResourceType.Assignment or NotificationResourceType.SubmissionAttempt or NotificationResourceType.Exam or
                NotificationResourceType.ExamAttempt or NotificationResourceType.Conversation => true,
            _ => false
        };
    }

    private IQueryable<ConversationInvitation> ConversationInvitations(Guid user, DateTime now) =>
        from invitation in db.ConversationInvitations
        join conversation in db.Conversations on invitation.ConversationId equals conversation.Id
        where invitation.TargetUserId == user && invitation.Status == ConversationInvitationStatus.Pending && invitation.ExpiresAtUtc > now &&
            conversation.Status == ConversationStatus.Active && conversation.Scope == ConversationScope.Personal &&
            !db.ConversationParticipants.Any(p => p.ConversationId == conversation.Id && p.UserId == user && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null) &&
            !db.ConversationMemberRestrictions.Any(r => r.ConversationId == conversation.Id && (r.UntilUtc == null || r.UntilUtc > now) &&
                (r.UserId == user && r.IsBanned || r.UserId == invitation.InviterUserId && (r.IsBanned || (r.DeniedCapabilities & GroupMemberCapability.InviteMembers) != 0))) &&
            (db.MessagingPrivacySettings.Any(s => s.UserId == user && (s.GroupInvitesAudience == PrivacyAudience.Everyone ||
                s.GroupInvitesAudience == PrivacyAudience.Contacts && db.UserContacts.Any(c => c.OwnerUserId == user && c.ContactUserId == invitation.InviterUserId))) ||
             !db.MessagingPrivacySettings.Any(s => s.UserId == user) &&
                (MessagingPrivacySettings.DefaultGroupInvitesAudience == PrivacyAudience.Everyone ||
                 MessagingPrivacySettings.DefaultGroupInvitesAudience == PrivacyAudience.Contacts && db.UserContacts.Any(c => c.OwnerUserId == user && c.ContactUserId == invitation.InviterUserId))) &&
            db.Users.Any(u => u.Id == invitation.InviterUserId && u.AccountStatus == MakanApp.Domain.Identity.UserAccountStatus.Active) &&
            !db.UserBlocks.Any(b => b.Status == UserBlockStatus.Active && b.EndedAtUtc == null &&
                (b.BlockerUserId == user && b.BlockedUserId == invitation.InviterUserId || b.BlockerUserId == invitation.InviterUserId && b.BlockedUserId == user)) &&
            db.ConversationParticipants.Any(p => p.ConversationId == conversation.Id && p.UserId == invitation.InviterUserId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null &&
                (p.Role == ConversationParticipantRole.Owner || p.Role == ConversationParticipantRole.Admin &&
                    (p.AdminPermissions & ConversationAdminPermission.ManageMembers) != 0 ||
                 p.Role == ConversationParticipantRole.Member && conversation.Type == ConversationType.Group &&
                    (conversation.MemberCapabilities & GroupMemberCapability.InviteMembers) != 0))
        select invitation;

    private IQueryable<ConversationJoinRequest> ConversationJoins(Guid user)
    {
        var conversations = AccountInboxAccessQuery.Conversations(db, user);
        return from request in db.ConversationJoinRequests
        join conversation in db.Conversations on request.ConversationId equals conversation.Id
        where conversation.Status == ConversationStatus.Active && (request.UserId == user ||
            conversations.Any(c => c.Id == conversation.Id) && db.ConversationParticipants.Any(p =>
                p.ConversationId == conversation.Id && p.UserId == user && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null &&
                (p.Role == ConversationParticipantRole.Owner || p.Role == ConversationParticipantRole.Admin &&
                    (p.AdminPermissions & ConversationAdminPermission.ManageInvites) != 0)))
        select request;
    }

    private IQueryable<PersonalClassInvitation> PersonalInvitations(Guid user, DateTime now) => db.PersonalClassInvitations.Where(i =>
        i.DestinationUserId == user && i.AcceptedAtUtc == null && i.DeclinedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now &&
        db.PersonalClasses.Any(c => c.Id == i.PersonalClassId && c.State == PersonalClassState.Active &&
            !db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id && m.StudentUserId == user && m.EndedAtUtc == null)));

    private IQueryable<PersonalClassJoinRequest> PersonalJoins(Guid user) => db.PersonalClassJoinRequests.Where(j =>
        db.PersonalClasses.Any(c => c.Id == j.PersonalClassId && c.State != PersonalClassState.Archived &&
            (j.UserId == user || c.ManagerUserId == user && db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id &&
                m.StudentUserId == user && m.EndedAtUtc == null && (m.LocalRoles & PersonalClassLocalRoles.Manager) != 0))));

    private IQueryable<PersonalClassManagerTransfer> PersonalTransfers(Guid user) => db.PersonalClassManagerTransfers.Where(t =>
        (t.TargetUserId == user || t.RequestedByUserId == user) && db.PersonalClasses.Any(c => c.Id == t.PersonalClassId && c.State != PersonalClassState.Archived));
}
