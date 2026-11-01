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
    public Task<ManagedConversationStoreResult> CreateAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        CreateCoreAsync(
            actorUserId,
            type,
            scope,
            title,
            description,
            initialParticipantUserIds,
            clientOperationId,
            creationPayloadHash,
            visibility,
            groupHistoryPolicy,
            memberCapabilities,
            accessContext,
            nowUtc,
            0,
            cancellationToken);

    private async Task<ManagedConversationStoreResult> CreateCoreAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        AccessContext accessContext,
        DateTime nowUtc,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var organizationId = scope == ConversationScope.Organization
            ? accessContext.OrganizationId
            : null;

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var existing = await LockCreationAsync(actorUserId, clientOperationId, cancellationToken);
            if (existing is not null)
            {
                EnsureCreationRetryMatches(existing, creationPayloadHash);
                var existingResult = await LoadManagedResultAsync(existing, true, cancellationToken, actorUserId);
                await transaction.CommitAsync(cancellationToken);
                return existingResult;
            }

            var actor = await LoadUserSnapshotAsync(actorUserId, cancellationToken);
            await EnsureActorCanMessageAsync(actorUserId, nowUtc, cancellationToken);
            if (actor is null ||
                !managementPolicy.CanCreate(scope, accessContext, actor.AgeCategory) ||
                !await HasActiveActorContextAsync(actorUserId, scope, organizationId, accessContext, cancellationToken))
            {
                throw ManagementNotAllowed();
            }

            var conversation = Conversation.CreateUserManaged(
                type,
                scope,
                organizationId,
                title,
                description,
                actorUserId,
                clientOperationId,
                creationPayloadHash,
                visibility,
                groupHistoryPolicy,
                memberCapabilities,
                nowUtc);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.Add(
                ConversationParticipant.CreateActive(
                    conversation.Id,
                    actorUserId,
                    nowUtc,
                    ConversationParticipantRole.Owner));
            foreach (var targetUserId in initialParticipantUserIds)
            {
                var facts = await LoadEligibilityFactsAsync(actorUserId, targetUserId, scope, organizationId,
                    accessContext, nowUtc, cancellationToken);
                var available = eligibilityPolicy.CanInviteToManagedConversation(facts) &&
                    !await HasEffectiveUserBlockAsync(actorUserId, targetUserId, cancellationToken);
                ConversationInvitation? invitation = null;
                var outcome = ConversationInitialTargetOutcome.NotAvailable;
                if (available && scope == ConversationScope.Personal)
                {
                    invitation = ConversationInvitation.Create(conversation.Id, actorUserId, targetUserId,
                        nowUtc.Add(ConversationInvitation.DefaultInitialLifetime), nowUtc);
                    dbContext.ConversationInvitations.Add(invitation);
                    await AuditInvitationAsync(actorUserId, accessContext.SessionId, invitation,
                        System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"), nowUtc, cancellationToken);
                    outcome = ConversationInitialTargetOutcome.InvitationPending;
                }
                else if (available)
                {
                    dbContext.ConversationParticipants.Add(ConversationParticipant.CreateActive(
                        conversation.Id, targetUserId, nowUtc, ConversationParticipantRole.Member));
                    outcome = ConversationInitialTargetOutcome.Added;
                }
                dbContext.ConversationInitialTargets.Add(ConversationInitialTarget.Create(conversation.Id, targetUserId, outcome, invitation));
            }

            realtime.CaptureMessaging();
            await dbContext.SaveChangesAsync(cancellationToken);
            await AppendChangeAsync(
                conversation,
                MessagingChangeType.ConversationChanged,
                conversation.Id,
                null,
                nowUtc,
                actorUserId,
                null,
                cancellationToken);
            var result = await LoadManagedResultAsync(conversation, false, cancellationToken, actorUserId);
            await transaction.CommitAsync(cancellationToken);
            outboxWakeSignal.Signal();
            return result;
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var existing = await dbContext.Conversations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CreatedByUserId == actorUserId &&
                            item.ClientOperationId == clientOperationId &&
                            item.ManagementPolicy == ConversationManagementPolicy.UserManaged,
                    cancellationToken);
            if (existing is null)
            {
                throw ConcurrencyConflict(exception);
            }

            EnsureCreationRetryMatches(existing, creationPayloadHash);
            return await LoadManagedResultAsync(existing, true, cancellationToken, actorUserId);
        }
        catch (DbUpdateException exception) when (IsDeadlock(exception))
        {
            return await RetryManagedCreationAfterDeadlockAsync(
                actorUserId,
                type,
                scope,
                title,
                description,
                initialParticipantUserIds,
                clientOperationId,
                creationPayloadHash,
                visibility,
                groupHistoryPolicy,
                memberCapabilities,
                accessContext,
                nowUtc,
                retryCount,
                exception,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return await RetryManagedCreationAfterDeadlockAsync(
                actorUserId,
                type,
                scope,
                title,
                description,
                initialParticipantUserIds,
                clientOperationId,
                creationPayloadHash,
                visibility,
                groupHistoryPolicy,
                memberCapabilities,
                accessContext,
                nowUtc,
                retryCount,
                exception,
                cancellationToken);
        }
    }

    private async Task<ManagedConversationStoreResult> RetryManagedCreationAfterDeadlockAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        AccessContext accessContext,
        DateTime nowUtc,
        int retryCount,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (retryCount >= 3)
        {
            throw ConcurrencyConflict(exception);
        }

        dbContext.ChangeTracker.Clear();
        await Task.Delay(TimeSpan.FromMilliseconds(20 * (retryCount + 1)), cancellationToken);
        return await CreateCoreAsync(
            actorUserId,
            type,
            scope,
            title,
            description,
            initialParticipantUserIds,
            clientOperationId,
            creationPayloadHash,
            visibility,
            groupHistoryPolicy,
            memberCapabilities,
            accessContext,
            nowUtc,
            retryCount + 1,
            cancellationToken);
    }

}
