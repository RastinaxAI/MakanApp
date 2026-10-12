using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<(IReadOnlyCollection<ConversationParticipantStoreRecord> Items, bool HasMore)> GetMembersAsync(
        Guid actorUserId,
        Guid conversationId,
        int page,
        int pageSize,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        if (conversation.Type is not ConversationType.Group and not ConversationType.Channel ||
            await LoadActiveParticipantAsync(conversationId, actorUserId, cancellationToken) is null ||
            !await CanAccessScopeAsync(conversation, actorUserId, accessContext, cancellationToken))
        {
            throw ConversationNotFound();
        }

        var rows = await (
            from participant in dbContext.ConversationParticipants.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on participant.UserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id
            where participant.ConversationId == conversationId &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  user.Username != null &&
                  user.ProfileCompletedAtUtc != null
            orderby participant.Role, participant.JoinedAtUtc, participant.Id
            select new
            {
                Participant = participant,
                Username = user.Username!,
                DisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .ToArrayAsync(cancellationToken);
        return (
            rows.Take(pageSize)
                .Select(row => new ConversationParticipantStoreRecord(
                    row.Participant,
                    new SafeMessagingIdentityRecord(
                        row.Participant.UserId,
                        row.Username,
                        row.DisplayName)))
                .ToArray(),
            rows.Length > pageSize);
    }

    public async Task<ManagedConversationStoreResult> GetDetailsAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null ||
            conversation.Type is not ConversationType.Group and not ConversationType.Channel)
        {
            throw ConversationNotFound();
        }

        var actor = await LoadActiveParticipantAsync(conversationId, actorUserId, cancellationToken);
        if (actor is null ||
            !await CanAccessScopeAsync(conversation, actorUserId, accessContext, cancellationToken))
        {
            throw ConversationNotFound();
        }

        return await LoadManagedResultAsync(conversation, false, cancellationToken);
    }

    public async Task<ManagedConversationStoreResult> AddMemberAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var canAdd = actor.Role == ConversationParticipantRole.Owner ||
                     actor.Role == ConversationParticipantRole.Admin &&
                     actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageMembers) ||
                     conversation.Type == ConversationType.Group &&
                     actor.Role == ConversationParticipantRole.Member &&
                     conversation.MemberCapabilities.HasFlag(GroupMemberCapability.InviteMembers);
        if (!canAdd)
        {
            throw ManagementNotAllowed();
        }

        if (await LoadActiveParticipantAsync(conversationId, targetUserId, cancellationToken) is not null)
        {
            throw new MessagingException(
                MessagingErrorCodes.ParticipantAlreadyActive,
                "کاربر از قبل عضو فعال گفتگو است.");
        }

        await EnsureEligibleTargetAsync(
            actorUserId,
            targetUserId,
            conversation.Scope,
            conversation.OrganizationId,
            accessContext,
            nowUtc,
            cancellationToken);
        var added = ConversationParticipant.CreateActive(
                conversation.Id,
                targetUserId,
                nowUtc,
                ConversationParticipantRole.Member,
                conversation.Type == ConversationType.Group &&
                conversation.GroupHistoryPolicy == GroupHistoryPolicy.FromJoin
                    ? conversation.NextMessageSequence
                    : 1);
        dbContext.ConversationParticipants.Add(added);
        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ParticipantChanged,
            added.Id,
            VersionOf(added.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return result;
    }

    public async Task<ManagedConversationStoreResult> RemoveMemberAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var target = await LoadActiveParticipantAsync(conversationId, targetUserId, cancellationToken)
            ?? throw ParticipantNotFound();
        var canRemove = actor.Role == ConversationParticipantRole.Owner &&
                        target.Role != ConversationParticipantRole.Owner ||
                        actor.Role == ConversationParticipantRole.Admin &&
                        actor.AdminPermissions.HasFlag(ConversationAdminPermission.RemoveMembers) &&
                        (target.Role == ConversationParticipantRole.Member ||
                         target.Role == ConversationParticipantRole.Admin &&
                         actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageAdmins));
        if (!canRemove)
        {
            throw ManagementNotAllowed();
        }

        target.Remove(actorUserId, nowUtc);
        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ParticipantChanged,
            target.Id,
            VersionOf(target.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return result;
    }

    public async Task<ManagedConversationStoreResult> LeaveAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        if (!managementPolicy.CanLeave(actor.Role))
        {
            throw new MessagingException(
                MessagingErrorCodes.LastOwnerRequired,
                "مالک باید پیش از خروج، مالکیت را به عضو دیگری منتقل کند.");
        }

        actor.Leave(nowUtc);
        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ParticipantChanged,
            actor.Id,
            VersionOf(actor.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return result;
    }

    public async Task<ManagedConversationStoreResult> ChangeMemberRoleAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        ConversationParticipantRole role,
        ConversationAdminPermission adminPermissions,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var target = await LoadActiveParticipantAsync(conversationId, targetUserId, cancellationToken)
            ?? throw ParticipantNotFound();
        var canManageAdmins = actor.Role == ConversationParticipantRole.Owner ||
                              actor.Role == ConversationParticipantRole.Admin &&
                              actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageAdmins);
        var canGrantPermissions = actor.Role == ConversationParticipantRole.Owner ||
                                  (adminPermissions & ~actor.AdminPermissions) == 0;
        if (!canManageAdmins || !canGrantPermissions ||
            target.Role == ConversationParticipantRole.Owner ||
            role is not ConversationParticipantRole.Admin and not ConversationParticipantRole.Member)
        {
            throw ManagementNotAllowed();
        }

        target.ChangeRole(role, role == ConversationParticipantRole.Admin
            ? adminPermissions
            : ConversationAdminPermission.None);
        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ParticipantChanged,
            target.Id,
            VersionOf(target.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return result;
    }

}
