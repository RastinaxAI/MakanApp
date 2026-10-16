using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ConversationInvitationResult> InviteAsync(Guid actorId, Guid sessionId, Guid conversationId,
        CreateConversationInvitationCommand command, AccessContext access, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var (conversation, actor) = await LoadManagedForMutationAsync(actorId, conversationId, access, ct);
        EnsureInviterPermission(conversation, actor);
        await EnsureCapabilityNotRestrictedAsync(conversation.Id, actorId, GroupMemberCapability.InviteMembers, nowUtc, ct);
        await EnsureNotBannedAsync(conversation.Id, command.TargetUserId, nowUtc, ct);
        if (conversation.Scope != ConversationScope.Personal) throw ManagementNotAllowed();
        await EnsurePersonalCreationSafetyAsync(actorId, conversation.Scope, conversation.Type, conversation.Visibility, ct);
        await EnsureEligibleTargetAsync(actorId, command.TargetUserId, conversation.Scope, null, access, nowUtc, ct);
        await EnsurePersonalAudienceSafetyAsync(conversation, command.TargetUserId, nowUtc, ct);
        if (await LoadActiveParticipantAsync(conversationId, command.TargetUserId, ct) is not null)
            throw new MessagingException(MessagingErrorCodes.ParticipantAlreadyActive, "عضو از قبل فعال است.");
        var pending = await dbContext.ConversationInvitations.SingleOrDefaultAsync(i => i.ConversationId == conversationId &&
            i.TargetUserId == command.TargetUserId && i.Status == ConversationInvitationStatus.Pending, ct);
        if (pending is not null && pending.ExpiresAtUtc > nowUtc)
        {
            if (pending.InviterUserId != actorId || pending.ExpiresAtUtc != command.ExpiresAtUtc) throw StateConflict();
            await transaction.CommitAsync(ct);
            return MapInvitation(pending);
        }
        if (pending is not null)
        {
            pending.Complete(ConversationInvitationStatus.Expired, nowUtc);
            await SaveManagementAsync(ct);
        }
        var invitation = ConversationInvitation.Create(conversationId, actorId, command.TargetUserId, command.ExpiresAtUtc, nowUtc);
        dbContext.ConversationInvitations.Add(invitation);
        await AuditInvitationAsync(actorId, sessionId, invitation, correlationId, nowUtc, ct);
        await SaveManagementAsync(ct);
        await transaction.CommitAsync(ct);
        return MapInvitation(invitation);
    }

    public async Task<IReadOnlyCollection<ConversationInvitationResult>> ListInvitationsAsync(Guid actorId, DateTime nowUtc, CancellationToken ct) =>
        await dbContext.ConversationInvitations.AsNoTracking().Where(i => i.TargetUserId == actorId &&
            i.Status == ConversationInvitationStatus.Pending && i.ExpiresAtUtc > nowUtc)
            .OrderByDescending(i => i.CreatedAtUtc).ThenBy(i => i.Id).Take(100)
            .Select(i => new ConversationInvitationResult(i.Id, i.ConversationId, i.InviterUserId, i.TargetUserId, i.Status, i.ExpiresAtUtc)).ToArrayAsync(ct);

    public async Task<ConversationInvitationResult> CompleteInvitationAsync(Guid actorId, Guid sessionId, Guid invitationId,
        ConversationInvitationStatus status, AccessContext access, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        var identity = await dbContext.ConversationInvitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == invitationId, ct)
            ?? throw ConversationNotFound();
        if ((status == ConversationInvitationStatus.Revoked ? identity.InviterUserId : identity.TargetUserId) != actorId)
            throw ConversationNotFound();
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(identity.ConversationId, ct) ?? throw ConversationNotFound();
        var invitation = await dbContext.ConversationInvitations.SingleAsync(i => i.Id == invitationId, ct);
        if (invitation.Status == status)
        {
            await transaction.CommitAsync(ct);
            return MapInvitation(invitation);
        }
        if (access.WorkspaceType != WorkspaceType.Personal || invitation.ExpiresAtUtc <= nowUtc ||
            invitation.Status != ConversationInvitationStatus.Pending || conversation.Status != ConversationStatus.Active)
            throw ConversationNotFound();
        var inviter = await LoadActiveParticipantAsync(conversation.Id, invitation.InviterUserId, ct) ?? throw ConversationNotFound();
        EnsureInviterPermission(conversation, inviter);
        if (status == ConversationInvitationStatus.Accepted)
        {
            await EnsureCapabilityNotRestrictedAsync(conversation.Id, inviter.UserId, GroupMemberCapability.InviteMembers, nowUtc, ct);
            await EnsureNotBannedAsync(conversation.Id, actorId, nowUtc, ct);
            await EnsurePersonalCreationSafetyAsync(inviter.UserId, conversation.Scope, conversation.Type, conversation.Visibility, ct);
            await EnsureEligibleTargetAsync(inviter.UserId, actorId, conversation.Scope, null, access, nowUtc, ct);
            await EnsurePersonalAudienceSafetyAsync(conversation, actorId, nowUtc, ct);
            if (await LoadActiveParticipantAsync(conversation.Id, actorId, ct) is null)
            {
                var participant = ConversationParticipant.CreateActive(conversation.Id, actorId, nowUtc, ConversationParticipantRole.Member,
                    conversation.GroupHistoryPolicy == GroupHistoryPolicy.FromJoin ? conversation.NextMessageSequence : 1);
                dbContext.ConversationParticipants.Add(participant);
                await AppendChangeAsync(conversation, MessagingChangeType.ParticipantChanged, participant.Id, null, nowUtc, actorId, null, ct);
            }
        }
        invitation.Complete(status, nowUtc);
        await AuditInvitationAsync(actorId, sessionId, invitation, correlationId, nowUtc, ct);
        await SaveManagementAsync(ct);
        await transaction.CommitAsync(ct);
        outboxWakeSignal.Signal();
        return MapInvitation(invitation);
    }

    private static void EnsureInviterPermission(Conversation conversation, ConversationParticipant actor)
    {
        if (actor.Role != ConversationParticipantRole.Owner &&
            !(actor.Role == ConversationParticipantRole.Admin && actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageMembers)) &&
            !(conversation.Type == ConversationType.Group && actor.Role == ConversationParticipantRole.Member &&
              conversation.MemberCapabilities.HasFlag(GroupMemberCapability.InviteMembers))) throw ManagementNotAllowed();
    }
    private async Task AuditInvitationAsync(Guid actorId, Guid sessionId, ConversationInvitation invitation,
        string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        var personId = await dbContext.Users.Where(u => u.Id == actorId).Select(u => u.PersonId).SingleAsync(ct);
        dbContext.AdminAuditLogs.Add(AdminAuditLog.Create(actorId, personId, sessionId, "messaging.invitation." + invitation.Status.ToString().ToLowerInvariant(),
            "ConversationInvitation", invitation.Id.ToString("D"), null, "گردش رضایت عضویت شخصی", nowUtc, correlationId,
            AdminAuditResult.Succeeded, null, invitation.Status.ToString()));
    }
    private static ConversationInvitationResult MapInvitation(ConversationInvitation i) =>
        new(i.Id, i.ConversationId, i.InviterUserId, i.TargetUserId, i.Status, i.ExpiresAtUtc);
}
