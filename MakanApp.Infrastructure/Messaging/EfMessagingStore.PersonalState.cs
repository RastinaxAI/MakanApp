using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<(MessageUserVisibility Visibility, bool AlreadyHidden)> DeleteMessageForMeAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
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
        var message = await LockMessageAsync(conversationId, messageId, cancellationToken)
            ?? throw MessageNotFound();
        var visibleFromSequence = await GetVisibleFromSequenceAsync(
            conversationId,
            userId,
            participant.HistoryVisibleFromSequence,
            cancellationToken);
        if (message.Sequence < visibleFromSequence)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationHistoryNotVisible,
                "این پیام در تاریخچه قابل مشاهده کاربر نیست.");
        }

        var existing = await dbContext.MessageUserVisibilities
            .FromSqlInterpolated($"SELECT * FROM [messaging].[MessageUserVisibilities] WITH (UPDLOCK, HOLDLOCK) WHERE [MessageId] = {messageId} AND [UserId] = {userId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return (existing, true);
        }

        var created = MessageUserVisibility.Create(messageId, userId, nowUtc);
        dbContext.MessageUserVisibilities.Add(created);
        await SaveSyncAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.MessageHiddenForUser,
            messageId,
            null,
            nowUtc,
            userId,
            userId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return (created, false);
    }

    public Task<ConversationUserState> SetConversationArchivedAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        bool archived,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        MutateConversationStateAsync(
            userId,
            conversationId,
            accessContext,
            state =>
            {
                var changed = state.IsArchived != archived;
                if (changed && archived)
                {
                    state.Archive(nowUtc);
                }
                else if (changed)
                {
                    state.Unarchive(nowUtc);
                }

                return changed;
            },
            nowUtc,
            cancellationToken);

    public Task<ConversationUserState> SetConversationPinnedAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        bool pinned,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        MutateConversationStateAsync(
            userId,
            conversationId,
            accessContext,
            state =>
            {
                var changed = state.IsPinned != pinned;
                if (changed && pinned)
                {
                    state.Pin(nowUtc);
                }
                else if (changed)
                {
                    state.Unpin(nowUtc);
                }

                return changed;
            },
            nowUtc,
            cancellationToken);

    public Task<ConversationUserState> SetConversationMuteAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        ConversationMuteMode mode,
        DateTime? mutedUntilUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        MutateConversationStateAsync(
            userId,
            conversationId,
            accessContext,
            state =>
            {
                var changed = state.MuteMode != mode || state.MutedUntilUtc != mutedUntilUtc;
                if (!changed)
                {
                    return false;
                }

                if (mode == ConversationMuteMode.Permanent)
                {
                    state.MutePermanently(nowUtc);
                }
                else if (mode == ConversationMuteMode.Until && mutedUntilUtc.HasValue)
                {
                    state.MuteUntil(mutedUntilUtc.Value, nowUtc);
                }
                else if (mode == ConversationMuteMode.None)
                {
                    state.Unmute(nowUtc);
                }
                else
                {
                    throw new MessagingException(
                        MessagingErrorCodes.ConversationStateConflict,
                        "تنظیم بی‌صدا معتبر نیست.");
                }

                return changed;
            },
            nowUtc,
            cancellationToken);

    public Task<ConversationUserState> MarkConversationUnreadAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        MutateConversationStateAsync(
            userId,
            conversationId,
            accessContext,
            state =>
            {
                var changed = !state.IsManuallyUnread;
                if (changed)
                {
                    state.MarkUnread(nowUtc);
                }
                return changed;
            },
            nowUtc,
            cancellationToken);

    private async Task<ConversationUserState> MutateConversationStateAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        Func<ConversationUserState, bool> mutate,
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

        var changed = mutate(state);
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
}
