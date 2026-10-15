using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ManagedConversationStoreResult> UpdateMemberPermissionsAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        ConversationAdminPermission permissions,
        byte[] expectedVersion,
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
        if (target.Role != ConversationParticipantRole.Admin ||
            actor.Role != ConversationParticipantRole.Owner &&
            (actor.Role != ConversationParticipantRole.Admin ||
             !actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageAdmins) ||
             (permissions & ~actor.AdminPermissions) != 0))
        {
            throw PermissionRequired();
        }

        ApplyParticipantVersion(target, expectedVersion);
        if (conversation.Scope == ConversationScope.Personal && conversation.Type == ConversationType.Channel &&
            permissions.HasFlag(ConversationAdminPermission.PublishMessages) && !await IsVerifiedAdultAsync(targetUserId, cancellationToken))
            throw ManagementNotAllowed();
        target.UpdateAdminPermissions(permissions);
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

    public async Task<ManagedConversationStoreResult> UpdateSettingsAsync(
        Guid actorUserId,
        Guid conversationId,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        byte[] expectedVersion,
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
        var allowed = actor.Role == ConversationParticipantRole.Owner ||
            actor.Role == ConversationParticipantRole.Admin &&
            (conversation.Visibility == visibility ||
             actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageVisibility)) &&
            (conversation.GroupHistoryPolicy == groupHistoryPolicy ||
             actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageHistory)) &&
            (conversation.MemberCapabilities == memberCapabilities ||
             actor.AdminPermissions.HasFlag(ConversationAdminPermission.ManageMembers));
        if (!allowed)
        {
            throw PermissionRequired();
        }

        await EnsurePersonalCreationSafetyAsync(actorUserId, conversation.Scope, conversation.Type, visibility, cancellationToken);
        if (conversation.CreatedByUserId.HasValue)
            await EnsurePersonalCreationSafetyAsync(conversation.CreatedByUserId.Value, conversation.Scope, conversation.Type, visibility, cancellationToken);
        if (!conversation.RowVersion.SequenceEqual(expectedVersion))
        {
            throw StateConflict();
        }

        dbContext.Entry(conversation).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        try
        {
            conversation.UpdateBehaviorSettings(visibility, groupHistoryPolicy, memberCapabilities);
        }
        catch (ArgumentException exception)
        {
            throw new MessagingException(
                MessagingErrorCodes.ManagedConversationInvalid,
                "تنظیمات گفتگو با نوع آن سازگار نیست.",
                exception);
        }

        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ConversationChanged,
            conversation.Id,
            VersionOf(conversation.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return result;
    }

    public async Task<DiscoverableConversationStorePage> DiscoverAsync(
        Guid actorUserId,
        ConversationType? type,
        string? query,
        int page,
        int pageSize,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var workspaceType = accessContext.WorkspaceType;
        var organizationId = accessContext.OrganizationId;
        var membershipId = accessContext.MembershipId;
        var rows = await (
            from conversation in dbContext.Conversations.AsNoTracking()
            join organizationValue in dbContext.Organizations.AsNoTracking()
                on conversation.OrganizationId equals (Guid?)organizationValue.Id
                into organizations
            from organization in organizations.DefaultIfEmpty()
            where conversation.Status == ConversationStatus.Active &&
                  conversation.ManagementPolicy == ConversationManagementPolicy.UserManaged &&
                  conversation.Visibility == ConversationVisibility.Public &&
                  (conversation.Type == ConversationType.Group ||
                   conversation.Type == ConversationType.Channel) &&
                  (!type.HasValue || conversation.Type == type.Value) &&
                  (query == null || conversation.Title!.Contains(query)) &&
                  (workspaceType == WorkspaceType.Personal &&
                   conversation.Scope == ConversationScope.Personal ||
                   workspaceType == WorkspaceType.Organization &&
                   organizationId != null && membershipId != null &&
                   conversation.Scope == ConversationScope.Organization &&
                   conversation.OrganizationId == organizationId &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == membershipId &&
                       membership.UserId == actorUserId &&
                       membership.OrganizationId == organizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null))
            orderby conversation.CreatedAtUtc descending, conversation.Id descending
            select new
            {
                Conversation = conversation,
                OrganizationName = organization == null ? null : organization.Name,
                IsJoined = dbContext.ConversationParticipants.Any(participant =>
                    participant.ConversationId == conversation.Id &&
                    participant.UserId == actorUserId &&
                    participant.Status == ConversationParticipantStatus.Active &&
                    participant.EndedAtUtc == null)
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .ToArrayAsync(cancellationToken);
        return new DiscoverableConversationStorePage(
            rows.Take(pageSize)
                .Select(row => new DiscoverableConversationStoreRecord(
                    row.Conversation,
                    row.OrganizationName,
                    row.IsJoined))
                .ToArray(),
            rows.Length > pageSize);
    }

    public async Task<ManagedConversationStoreResult> JoinPublicAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        if (conversation.Status != ConversationStatus.Active ||
            conversation.ManagementPolicy != ConversationManagementPolicy.UserManaged ||
            conversation.Visibility != ConversationVisibility.Public ||
            conversation.Type is not ConversationType.Group and not ConversationType.Channel ||
            !await CanAccessScopeAsync(conversation, actorUserId, accessContext, cancellationToken))
        {
            throw NotJoinable();
        }

        var existing = await LoadActiveParticipantAsync(conversationId, actorUserId, cancellationToken);
        await EnsurePersonalAudienceSafetyAsync(conversation, actorUserId, nowUtc, cancellationToken);
        if (existing is not null)
        {
            var existingResult = await LoadManagedResultAsync(conversation, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existingResult;
        }

        var participant = ConversationParticipant.CreateActive(
            conversationId,
            actorUserId,
            nowUtc,
            ConversationParticipantRole.Member,
            conversation.Type == ConversationType.Group &&
            conversation.GroupHistoryPolicy == GroupHistoryPolicy.FromJoin
                ? conversation.NextMessageSequence
                : 1);
        dbContext.ConversationParticipants.Add(participant);
        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ParticipantChanged,
            participant.Id,
            VersionOf(participant.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return result;
    }

    private void ApplyParticipantVersion(ConversationParticipant participant, byte[] expectedVersion)
    {
        if (!participant.RowVersion.SequenceEqual(expectedVersion))
        {
            throw StateConflict();
        }

        dbContext.Entry(participant).Property(item => item.RowVersion).OriginalValue = expectedVersion;
    }

    private static MessagingException PermissionRequired() =>
        new(
            MessagingErrorCodes.ConversationPermissionRequired,
            "مجوز لازم برای این عملیات وجود ندارد.");

    private static MessagingException NotJoinable() =>
        new(
            MessagingErrorCodes.ConversationNotJoinable,
            "گفتگو برای عضویت عمومی در دسترس نیست.");

    private static MessagingException StateConflict() =>
        new(
            MessagingErrorCodes.ConversationStateConflict,
            "وضعیت گفتگو هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید.");
}
