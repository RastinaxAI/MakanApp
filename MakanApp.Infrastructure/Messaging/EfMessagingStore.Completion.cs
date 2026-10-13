using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private const string SafeMessageNotificationTitle = "پیام جدید";
    private const string SafeMessageNotificationBody = "پیام جدیدی در ماکان دارید.";

    public Task<SavedConversationStoreResult> GetOrCreateSavedConversationAsync(
        Guid userId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        GetOrCreateSavedConversationCoreAsync(
            userId,
            accessContext,
            nowUtc,
            0,
            cancellationToken);

    private async Task<SavedConversationStoreResult> GetOrCreateSavedConversationCoreAsync(
        Guid userId,
        AccessContext accessContext,
        DateTime nowUtc,
        int retryCount,
        CancellationToken cancellationToken)
    {
        if (accessContext.WorkspaceType != WorkspaceType.Personal ||
            accessContext.OrganizationId.HasValue || accessContext.MembershipId.HasValue)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationNotAllowed,
                "پیام‌های ذخیره‌شده فقط در فضای شخصی در دسترس است.");
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var existing = await dbContext.Conversations
                .FromSqlInterpolated($"SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK, HOLDLOCK) WHERE [Type] = 4 AND [SavedOwnerUserId] = {userId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                await EnsureSavedParticipantAsync(existing.Id, userId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new SavedConversationStoreResult(existing, true);
            }

            var conversation = Conversation.CreateSaved(userId, nowUtc);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.Add(ConversationParticipant.CreateActive(
                conversation.Id,
                userId,
                nowUtc));
            await dbContext.SaveChangesAsync(cancellationToken);
            await AppendChangeAsync(
                conversation,
                MessagingChangeType.ConversationChanged,
                conversation.Id,
                null,
                nowUtc,
                userId,
                userId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            outboxWakeSignal.Signal();
            return new SavedConversationStoreResult(conversation, false);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var existing = await dbContext.Conversations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    conversation => conversation.Type == ConversationType.Saved &&
                                    conversation.SavedOwnerUserId == userId,
                    cancellationToken);
            if (existing is null)
            {
                throw ConcurrencyConflict(exception);
            }

            return new SavedConversationStoreResult(existing, true);
        }
        catch (DbUpdateException exception) when (IsDeadlock(exception))
        {
            return await RetrySavedCreationAfterDeadlockAsync(
                userId, accessContext, nowUtc, retryCount, exception, cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return await RetrySavedCreationAfterDeadlockAsync(
                userId, accessContext, nowUtc, retryCount, exception, cancellationToken);
        }
    }

    private async Task<SavedConversationStoreResult> RetrySavedCreationAfterDeadlockAsync(
        Guid userId,
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
        return await GetOrCreateSavedConversationCoreAsync(
            userId,
            accessContext,
            nowUtc,
            retryCount + 1,
            cancellationToken);
    }

    public async Task<ConversationUserState> ClearConversationHistoryAsync(
        Guid userId,
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
        var participant = await LoadActiveParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, participant, userId, accessContext, cancellationToken);

        var state = await dbContext.ConversationUserStates
            .FromSqlInterpolated($"SELECT * FROM [messaging].[ConversationUserStates] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversationId} AND [UserId] = {userId}")
            .SingleOrDefaultAsync(cancellationToken);
        var isNew = state is null;
        if (state is null)
        {
            state = ConversationUserState.Create(conversationId, userId, nowUtc);
            dbContext.ConversationUserStates.Add(state);
        }

        var changed = state.HideHistoryThrough(conversation.NextMessageSequence - 1, nowUtc);
        if (changed || isNew)
        {
            await SaveSyncAsync(cancellationToken);
            await AppendChangeAsync(
                conversation,
                MessagingChangeType.ConversationUserStateChanged,
                conversationId,
                VersionOf(state.RowVersion),
                nowUtc,
                userId,
                userId,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        if (changed || isNew)
        {
            outboxWakeSignal.Signal();
        }

        return state;
    }

    private async Task<long> GetVisibleFromSequenceAsync(
        Guid conversationId,
        Guid userId,
        long membershipBoundary,
        CancellationToken cancellationToken)
    {
        var hiddenThrough = await dbContext.ConversationUserStates
            .AsNoTracking()
            .Where(state => state.ConversationId == conversationId && state.UserId == userId)
            .Select(state => (long?)state.HistoryHiddenThroughSequence)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        return Math.Max(membershipBoundary, hiddenThrough + 1);
    }

    private async Task EnqueueMessageNotificationsAsync(
        Conversation conversation,
        Message message,
        Guid senderUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (conversation.Type == ConversationType.Saved)
        {
            return;
        }

        var recipients = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.ConversationId == conversation.Id &&
                                  participant.UserId != senderUserId &&
                                  participant.Status == ConversationParticipantStatus.Active &&
                                  participant.EndedAtUtc == null &&
                                  (conversation.Scope == ConversationScope.Personal ||
                                   conversation.OrganizationId.HasValue &&
                                   dbContext.Memberships.Any(membership =>
                                       membership.UserId == participant.UserId &&
                                       membership.OrganizationId == conversation.OrganizationId.Value &&
                                       membership.Status == MembershipStatus.Active &&
                                       membership.EndedAtUtc == null)))
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (recipients.Length == 0)
        {
            return;
        }

        var mentioned = await dbContext.MessageMentions
            .AsNoTracking()
            .Where(mention => mention.MessageId == message.Id)
            .Select(mention => mention.MentionedUserId)
            .ToArrayAsync(cancellationToken);
        Guid? repliedToUserId = null;
        if (message.ReplyToMessageId.HasValue)
        {
            repliedToUserId = await dbContext.Messages
                .AsNoTracking()
                .Where(source => source.Id == message.ReplyToMessageId.Value)
                .Select(source => (Guid?)source.SenderUserId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var states = await dbContext.ConversationUserStates
            .AsNoTracking()
            .Where(state => state.ConversationId == conversation.Id && recipients.Contains(state.UserId))
            .ToDictionaryAsync(state => state.UserId, cancellationToken);
        var mentionedSet = mentioned.ToHashSet();
        // نوع مؤثر هر گیرنده باید در envelope خودش ثبت شود؛ MessageId همچنان کلید dedupe مشترک است.
        var envelopes = recipients.SelectMany(recipientUserId =>
        {
            var type = mentionedSet.Contains(recipientUserId)
                ? NotificationType.MessageMention
                : repliedToUserId == recipientUserId
                    ? NotificationType.MessageReply
                    : NotificationType.MessageReceived;
            var muted = states.TryGetValue(recipientUserId, out var state) && state.IsMutedAt(nowUtc);
            return NotificationOutboxContract.Create(new NotificationIntent(
                message.Id,
                conversation.OrganizationId,
                NotificationCategory.Communication,
                type,
                NotificationResourceType.Conversation,
                conversation.Id,
                message.SentAtUtc,
                [new NotificationRecipientIntent(
                    recipientUserId,
                    SafeMessageNotificationTitle,
                    SafeMessageNotificationBody,
                    muted)]));
        });
        outbox.EnqueueRange(envelopes);
    }

    private async Task EnsureSavedParticipantAsync(
        Guid conversationId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var participants = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.ConversationId == conversationId &&
                                  participant.Status == ConversationParticipantStatus.Active &&
                                  participant.EndedAtUtc == null)
            .Select(participant => participant.UserId)
            .ToArrayAsync(cancellationToken);
        if (participants.Length != 1 || participants[0] != ownerUserId)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationNotAllowed,
                "مالکیت پیام‌های ذخیره‌شده معتبر نیست.");
        }
    }
}
