using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ConversationChangePageStoreResult> GetConversationChangesAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long afterChangeSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ConversationId == conversationId &&
                        item.UserId == userId &&
                        item.Status == ConversationParticipantStatus.Active &&
                        item.EndedAtUtc == null,
                cancellationToken) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);

        var currentSequence = conversation.NextChangeSequence - 1;
        if (afterChangeSequence > currentSequence)
        {
            throw new MessagingException(
                MessagingErrorCodes.ChangeCursorInvalid,
                "نشانگر تغییر از وضعیت فعلی گفتگو جلوتر است.");
        }

        var rows = await dbContext.MessagingChangeEvents
            .AsNoTracking()
            .Where(change => change.ConversationId == conversationId &&
                             change.ChangeSequence > afterChangeSequence &&
                             (!change.AudienceUserId.HasValue || change.AudienceUserId == userId) &&
                             (change.ChangeType != MessagingChangeType.MessageCreated &&
                              change.ChangeType != MessagingChangeType.MessageEdited &&
                              change.ChangeType != MessagingChangeType.MessageDeleted &&
                              change.ChangeType != MessagingChangeType.ReactionChanged &&
                              change.ChangeType != MessagingChangeType.PinChanged ||
                              dbContext.Messages.Any(message =>
                                  message.Id == change.ResourceId &&
                                  message.Sequence >= participant.HistoryVisibleFromSequence &&
                                  !dbContext.MessageUserVisibilities.Any(hidden =>
                                      hidden.MessageId == message.Id && hidden.UserId == userId))))
            .OrderBy(change => change.ChangeSequence)
            .Take(limit + 1)
            .ToArrayAsync(cancellationToken);
        var hasMore = rows.Length > limit;
        var changes = hasMore ? rows[..limit] : rows;
        var observedSequence = changes.Length == 0
            ? currentSequence
            : changes[^1].ChangeSequence;
        var nextSequence = hasMore
            ? observedSequence
            : Math.Max(observedSequence, currentSequence);
        return new ConversationChangePageStoreResult(changes, nextSequence, hasMore);
    }

    public Task<ConversationCursorStoreResult> AdvanceReadCursorAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long upToMessageSequence,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        AdvanceParticipantCursorAsync(
            userId,
            conversationId,
            accessContext,
            upToMessageSequence,
            nowUtc,
            true,
            cancellationToken);

    public Task<ConversationCursorStoreResult> AdvanceDeliveryCursorAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long upToMessageSequence,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        AdvanceParticipantCursorAsync(
            userId,
            conversationId,
            accessContext,
            upToMessageSequence,
            nowUtc,
            false,
            cancellationToken);

    public async Task EnsureRealtimeAccessAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ConversationId == conversationId &&
                        item.UserId == userId &&
                        item.Status == ConversationParticipantStatus.Active &&
                        item.EndedAtUtc == null,
                cancellationToken) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);
    }

    private async Task<ConversationCursorStoreResult> AdvanceParticipantCursorAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long upToMessageSequence,
        DateTime nowUtc,
        bool read,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await LoadActiveParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);

        var messageExists = await dbContext.Messages.AnyAsync(
            message => message.ConversationId == conversationId &&
                       message.Sequence == upToMessageSequence &&
                       message.Sequence >= participant.HistoryVisibleFromSequence &&
                       !dbContext.MessageUserVisibilities.Any(hidden =>
                           hidden.MessageId == message.Id && hidden.UserId == userId),
            cancellationToken);
        if (!messageExists)
        {
            throw new MessagingException(
                read ? MessagingErrorCodes.ReadCursorInvalid : MessagingErrorCodes.DeliveryCursorInvalid,
                "پیام متناظر با نشانگر در تاریخچه قابل مشاهده وجود ندارد.");
        }

        var changed = read
            ? participant.AdvanceRead(upToMessageSequence, nowUtc)
            : participant.AdvanceDelivery(upToMessageSequence, nowUtc);
        var personalState = read
            ? await dbContext.ConversationUserStates.SingleOrDefaultAsync(
                state => state.ConversationId == conversationId && state.UserId == userId,
                cancellationToken)
            : null;
        var manualUnreadCleared = personalState?.ClearManualUnread(nowUtc) ?? false;
        if (changed || manualUnreadCleared)
        {
            await SaveSyncAsync(cancellationToken);
            await AppendChangeAsync(
                conversation,
                read ? MessagingChangeType.ReadCursorAdvanced : MessagingChangeType.DeliveryCursorAdvanced,
                participant.Id,
                VersionOf(participant.RowVersion),
                nowUtc,
                userId,
                userId,
                cancellationToken);
        }

        var unreadCount = await GetUnreadCountAsync(
            conversationId,
            userId,
            participant.LastReadMessageSequence,
            participant.HistoryVisibleFromSequence,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (changed || manualUnreadCleared)
        {
            outboxWakeSignal.Signal();
        }

        return new ConversationCursorStoreResult(participant, unreadCount);
    }

    private Task<int> GetUnreadCountAsync(
        Guid conversationId,
        Guid userId,
        long afterMessageSequence,
        long historyVisibleFromSequence,
        CancellationToken cancellationToken) =>
        dbContext.Messages.CountAsync(
            message => message.ConversationId == conversationId &&
                       message.Sequence >= historyVisibleFromSequence &&
                       message.Sequence > afterMessageSequence &&
                       message.SenderUserId != userId &&
                       message.DeletedAtUtc == null &&
                       !dbContext.MessageUserVisibilities.Any(hidden =>
                           hidden.MessageId == message.Id && hidden.UserId == userId),
            cancellationToken);

    private async Task<MessagingChangeEvent> AppendChangeAsync(
        Conversation conversation,
        MessagingChangeType changeType,
        Guid resourceId,
        string? resourceVersion,
        DateTime occurredAtUtc,
        Guid? actorUserId,
        Guid? audienceUserId,
        CancellationToken cancellationToken)
    {
        var change = MessagingChangeEvent.Create(
            conversation.Id,
            conversation.AllocateNextChangeSequence(),
            changeType,
            resourceId,
            resourceVersion,
            occurredAtUtc,
            actorUserId,
            audienceUserId);
        dbContext.MessagingChangeEvents.Add(change);
        dbContext.MessagingRealtimeOutboxMessages.Add(
            MessagingRealtimeOutboxMessage.Create(change, occurredAtUtc));
        await SaveSyncAsync(cancellationToken);
        return change;
    }

    private async Task SaveSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ConcurrencyConflict(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw ConcurrencyConflict(exception);
        }
    }

    private static string? VersionOf(byte[] rowVersion) =>
        rowVersion.Length == 0 ? null : Convert.ToBase64String(rowVersion);
}
