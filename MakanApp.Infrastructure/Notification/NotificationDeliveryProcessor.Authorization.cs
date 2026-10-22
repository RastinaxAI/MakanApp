using MakanApp.Application.Messaging;
using System.Data;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed partial class NotificationDeliveryProcessor
{
    private async Task<DeliveryWork?> EvaluateCurrentDeliveryAsync(
        NotificationDeliveryAttempt attempt, DomainNotification notification, DateTime nowUtc, CancellationToken ct)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == notification.RecipientUserId && u.AccountStatus == UserAccountStatus.Active, ct))
            return Suppress(NotificationDeliveryFailureReason.ResourceAccessRevoked);
        var preference = await preferenceResolver.ResolveAsync(notification.RecipientUserId, notification.OrganizationId,
            NotificationPreferencePolicy.Map(notification.Type), ct);
        if (!preference.ScopeAvailable) return Suppress(NotificationDeliveryFailureReason.OrganizationAccessRevoked);
        if (attempt.Channel == NotificationDeliveryChannel.Push && !preference.PushEnabled ||
            attempt.Channel == NotificationDeliveryChannel.Sms && !preference.SmsEnabled)
            return Suppress(NotificationDeliveryFailureReason.SuppressedByPreference);
        if (notification.ResourceType == NotificationResourceType.Conversation)
        {
            var state = await dbContext.ConversationUserStates.AsNoTracking()
                .SingleOrDefaultAsync(s => s.ConversationId == notification.ResourceId && s.UserId == notification.RecipientUserId, ct);
            if (state?.IsMutedAt(nowUtc) == true) return Suppress(NotificationDeliveryFailureReason.SuppressedByPreference);
            if (!await CanDeliverMessageNotificationAsync(notification, nowUtc, ct))
                return Suppress(NotificationDeliveryFailureReason.ResourceAccessRevoked);
        }
        var allowedAtUtc = NotificationQuietHoursPolicy.GetNextAllowedUtc(preference, nowUtc);
        return allowedAtUtc > nowUtc ? new DeliveryWork(null, null, null, allowedAtUtc) : null;
    }

    private async Task<bool> CanDeliverMessageNotificationAsync(DomainNotification notification, DateTime nowUtc, CancellationToken ct)
    {
        var recipientId = notification.RecipientUserId;
        var members = MessengerRelationshipQuery.ClassMembers(dbContext);
        var source = await (from message in dbContext.Messages.AsNoTracking()
            join conversation in dbContext.Conversations.AsNoTracking() on message.ConversationId equals conversation.Id
            join participant in dbContext.ConversationParticipants.AsNoTracking() on conversation.Id equals participant.ConversationId
            where message.Id == notification.SourceEventId && conversation.Id == notification.ResourceId &&
                message.DeletedAtUtc == null && !message.IsSilent && participant.UserId == recipientId &&
                participant.Status == ConversationParticipantStatus.Active && participant.EndedAtUtc == null &&
                message.Sequence >= participant.HistoryVisibleFromSequence &&
                !dbContext.MessageUserVisibilities.Any(v => v.MessageId == message.Id && v.UserId == recipientId) &&
                !dbContext.ConversationUserStates.Any(s => s.ConversationId == conversation.Id && s.UserId == recipientId &&
                    s.HistoryHiddenThroughSequence >= message.Sequence) &&
                (conversation.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
                    conversation.AcademicClassId != null && members.Any(m => m.UserId == recipientId &&
                        m.ClassId == conversation.AcademicClassId && m.OrganizationId == conversation.OrganizationId)) &&
                (conversation.Scope == ConversationScope.Personal || dbContext.Memberships.Any(m =>
                    m.UserId == recipientId && m.OrganizationId == conversation.OrganizationId && m.Status == MembershipStatus.Active &&
                    m.EndedAtUtc == null && dbContext.RoleAssignments.Any(r => r.MembershipId == m.Id &&
                        r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null)))
            select new { message.SenderUserId, conversation.Type, conversation.Scope, conversation.OrganizationId }).SingleOrDefaultAsync(ct);
        if (source is null) return false;
        var users = await dbContext.Users.AsNoTracking().Where(u => u.Id == recipientId || u.Id == source.SenderUserId)
            .Select(u => new { u.Id, u.AccountStatus, u.CommunicationAgeCategory, u.CommunicationAgeVerificationStatus }).ToArrayAsync(ct);
        if (users.Length != 2 || users.Any(u => u.AccountStatus != UserAccountStatus.Active)) return false;
        if (users.Any(u => u.CommunicationAgeCategory != CommunicationAgeCategory.Adult ||
                u.CommunicationAgeVerificationStatus != CommunicationAgeVerificationStatus.Verified) &&
            !await MessengerRelationshipQuery.RelatedUsers(dbContext, recipientId, nowUtc,
                source.Scope == ConversationScope.Organization ? source.OrganizationId : null).ContainsAsync(source.SenderUserId, ct)) return false;
        if (source.Type != ConversationType.Direct) return true;
        if (await dbContext.UserBlocks.AnyAsync(b => b.Status == UserBlockStatus.Active && b.EndedAtUtc == null &&
                (b.BlockerUserId == recipientId && b.BlockedUserId == source.SenderUserId ||
                 b.BlockerUserId == source.SenderUserId && b.BlockedUserId == recipientId), ct)) return false;
        if (source.Scope != ConversationScope.Personal) return true;
        var privacy = await privacyResolver.ResolveAsync(source.SenderUserId, recipientId, ct);
        return MessagingPrivacyPolicy.Allows(privacy.Settings.DirectMessagesAudience, privacy.TargetHasSavedRequester);
    }

    private async Task DeferBeforeDeliveryAsync(Guid attemptId, Guid claimToken, DateTime deliverAtUtc, CancellationToken ct)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var attempt = await LockAttemptAsync(attemptId, ct);
        if (!IsLiveOwner(attempt, claimToken, timeProvider.GetUtcNow().UtcDateTime))
        {
            await transaction.CommitAsync(ct);
            telemetry.RecordOutcome(MakanApp.Infrastructure.Observability.WorkerTelemetry.NotificationDelivery, "LeaseLost");
            return;
        }
        attempt!.DeferBeforeDelivery(claimToken, deliverAtUtc);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        telemetry.RecordOutcome(MakanApp.Infrastructure.Observability.WorkerTelemetry.NotificationDelivery, "Deferred");
    }

    private static DeliveryWork Suppress(NotificationDeliveryFailureReason reason) => new(null, null, reason);
}
