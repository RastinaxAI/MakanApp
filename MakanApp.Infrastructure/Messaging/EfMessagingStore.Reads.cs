using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private const int LastMessagePreviewLength = 120;

    public async Task<IReadOnlyCollection<ConversationSummaryStoreRecord>> GetMyConversationsAsync(
        Guid userId,
        AccessContext accessContext,
        ConversationListFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var workspaceType = accessContext.WorkspaceType;
        var organizationId = accessContext.OrganizationId;
        var membershipId = accessContext.MembershipId;
        var query =
            from participant in dbContext.ConversationParticipants
            join conversation in dbContext.Conversations
                on participant.ConversationId equals conversation.Id
            join stateValue in dbContext.ConversationUserStates
                on new { ConversationId = conversation.Id, UserId = userId }
                equals new { stateValue.ConversationId, stateValue.UserId }
                into states
            from state in states.DefaultIfEmpty()
            join organizationValue in dbContext.Organizations
                on conversation.OrganizationId equals (Guid?)organizationValue.Id
                into organizations
            from organization in organizations.DefaultIfEmpty()
            where participant.UserId == userId &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  conversation.Status == ConversationStatus.Active &&
                  (filter == ConversationListFilter.Archived
                      ? state != null && state.ArchivedAtUtc != null
                      : state == null || state.ArchivedAtUtc == null) &&
                  (filter != ConversationListFilter.Direct || conversation.Type == ConversationType.Direct) &&
                  (filter != ConversationListFilter.Group || conversation.Type == ConversationType.Group) &&
                  (filter != ConversationListFilter.Channel || conversation.Type == ConversationType.Channel) &&
                  (filter != ConversationListFilter.Unread ||
                   state != null && state.IsManuallyUnread ||
                   dbContext.Messages.Any(message =>
                       message.ConversationId == conversation.Id &&
                       message.Sequence >= participant.HistoryVisibleFromSequence &&
                       message.Sequence > participant.LastReadMessageSequence &&
                       message.SenderUserId != userId &&
                       message.DeletedAtUtc == null &&
                       !dbContext.MessageUserVisibilities.Any(hidden =>
                           hidden.MessageId == message.Id && hidden.UserId == userId))) &&
                  (workspaceType == WorkspaceType.Personal &&
                   conversation.Scope == ConversationScope.Personal ||
                   workspaceType == WorkspaceType.Organization &&
                   organizationId != null &&
                   membershipId != null &&
                   conversation.Scope == ConversationScope.Organization &&
                   conversation.OrganizationId == organizationId &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == membershipId &&
                       membership.UserId == userId &&
                       membership.OrganizationId == organizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null))
            let lastMessage = dbContext.Messages
                .Where(message => message.ConversationId == conversation.Id &&
                                  message.Sequence >= participant.HistoryVisibleFromSequence &&
                                  !dbContext.MessageUserVisibilities.Any(hidden =>
                                      hidden.MessageId == message.Id && hidden.UserId == userId))
                .OrderByDescending(message => message.Sequence)
                .FirstOrDefault()
            select new
            {
                Conversation = conversation,
                Participant = participant,
                State = state,
                OrganizationName = organization == null ? null : organization.Name,
                LastMessage = lastMessage
            };

        var conversationRows = await query
            .AsNoTracking()
            .OrderByDescending(item => item.State != null && item.State.PinnedAtUtc != null)
            .ThenByDescending(item => item.LastMessage == null
                ? item.Conversation.CreatedAtUtc
                : item.LastMessage.SentAtUtc)
            .ThenByDescending(item => item.Conversation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var results = new List<ConversationSummaryStoreRecord>(conversationRows.Length);
        foreach (var row in conversationRows)
        {
            var conversation = row.Conversation;
            SafeMessagingIdentityRecord? identity = null;
            if (conversation.Type == ConversationType.Direct)
            {
                var otherUserId = conversation.GetDirectPair().Other(userId);
                identity = await LoadSafeIdentityAsync(otherUserId, cancellationToken);
            }
            var lastMessage = row.LastMessage;
            results.Add(new ConversationSummaryStoreRecord(
                conversation,
                row.Participant,
                identity,
                CreatePreview(lastMessage?.Text),
                lastMessage?.SentAtUtc,
                lastMessage?.Sequence,
                await GetUnreadCountAsync(
                    conversation.Id,
                    userId,
                    row.Participant.LastReadMessageSequence,
                    row.Participant.HistoryVisibleFromSequence,
                    cancellationToken),
                row.State,
                row.OrganizationName));
        }

        return results;
    }

    public async Task<ConversationMessagePageStoreResult> GetConversationMessagesAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long? beforeSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            throw ConversationNotFound();
        }

        var participant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(
            participant => participant.ConversationId == conversationId &&
                           participant.UserId == userId &&
                           participant.Status == ConversationParticipantStatus.Active &&
                           participant.EndedAtUtc == null,
            cancellationToken);
        if (participant is null)
        {
            throw ConversationNotFound();
        }

        var hasActiveOrganizationMembership = conversation.Scope == ConversationScope.Personal ||
            conversation.OrganizationId.HasValue &&
            accessContext.MembershipId.HasValue &&
            await dbContext.Memberships.AnyAsync(
                membership => membership.Id == accessContext.MembershipId.Value &&
                              membership.UserId == userId &&
                              membership.OrganizationId == conversation.OrganizationId.Value &&
                              membership.Status == MembershipStatus.Active &&
                              membership.EndedAtUtc == null,
                cancellationToken);
        if (!eligibilityPolicy.CanRead(new ConversationReadFacts(
                conversation.Scope,
                conversation.OrganizationId,
                accessContext,
                participant.IsActive,
                hasActiveOrganizationMembership)))
        {
            throw new MessagingException(
                conversation.Scope == ConversationScope.Organization
                    ? MessagingErrorCodes.OrganizationScopeMismatch
                    : MessagingErrorCodes.ConversationNotAllowed,
                "دسترسی خواندن این گفتگو در زمینه فعلی مجاز نیست.");
        }

        var descending = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId &&
                              message.Sequence >= participant.HistoryVisibleFromSequence &&
                              !dbContext.MessageUserVisibilities.Any(hidden =>
                                  hidden.MessageId == message.Id && hidden.UserId == userId) &&
                              (!beforeSequence.HasValue || message.Sequence < beforeSequence.Value))
            .OrderByDescending(message => message.Sequence)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var messages = descending.Reverse().ToArray();
        long? nextBeforeSequence = null;
        if (messages.Length > 0)
        {
            var firstSequence = messages[0].Sequence;
            if (await dbContext.Messages.AnyAsync(
                    message => message.ConversationId == conversationId &&
                               message.Sequence >= participant.HistoryVisibleFromSequence &&
                               !dbContext.MessageUserVisibilities.Any(hidden =>
                                   hidden.MessageId == message.Id && hidden.UserId == userId) &&
                               message.Sequence < firstSequence,
                    cancellationToken))
            {
                nextBeforeSequence = firstSequence;
            }
        }

        return new ConversationMessagePageStoreResult(
            await LoadMessageRecordsAsync(messages, userId, cancellationToken),
            nextBeforeSequence);
    }

    private static string? CreatePreview(string? text)
    {
        if (text is null || text.Length <= LastMessagePreviewLength)
        {
            return text;
        }

        return text[..LastMessagePreviewLength];
    }
}
