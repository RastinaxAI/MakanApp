using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IConversationPollStore
{
    public async Task<Message?> PreparePollCreationAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command,
        byte[] definitionHash, AccessContext context, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, timeProvider.GetUtcNow().UtcDateTime, ct);
        var existing = await FindExistingPollCreationAsync(userId, conversationId, command, definitionHash, ct);
        if (existing is not null) return existing;
        await EnsureTopicOpenAsync(conversation, command.TopicId, ct);
        await ValidateScheduledSendAsync(userId, conversationId, command, false, ct);
        return null;
    }

    public async Task<Message> CreatePollAsync(Guid userId, Guid sessionId, Guid conversationId, SendMessageStoreCommand command,
        ConversationPollDefinition definition, byte[] definitionHash, bool safetyEvaluated, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ConversationNotFound();
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, now, ct);
        var existing = await FindExistingPollCreationAsync(userId, conversationId, command, definitionHash, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return existing; }
        await EnsureTopicOpenAsync(conversation, command.TopicId, ct);
        await EnsureSafetyGateSatisfiedAsync(conversation, safetyEvaluated, ct);
        var created = await CreateMessageAsync(conversation, actor, userId, command, Message.StorageMaximumTextLength, now, ct);
        var poll = ConversationPoll.Create(created.Message, definition, definitionHash);
        dbContext.ConversationPolls.Add(poll);
        dbContext.ConversationPollOptions.AddRange(Enumerable.Range(1, definition.Options.Count).Select(n => ConversationPollOption.Create(poll.Id, n, definition)));
        if (definition.IsQuiz) dbContext.ConversationQuizAnswers.Add(ConversationQuizAnswer.Create(poll.Id, definition));
        await SavePollChangesAsync(ct);
        await EnqueueMessageNotificationsAsync(conversation, created.Message, userId, now, ct);
        await AppendChangeAsync(conversation, MessagingChangeType.MessageCreated, created.Message.Id, VersionOf(created.Message.RowVersion), now, userId, null, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return created.Message;
    }

    private async Task<Message?> FindExistingPollCreationAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command, byte[] definitionHash, CancellationToken ct)
    {
        var message = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.SenderUserId == userId && m.ClientMessageId == command.ClientMessageId, ct);
        if (message is null) return null;
        await EnsureRetryMatchesAsync(message, command, ct);
        var hash = await dbContext.ConversationPolls.AsNoTracking().Where(p => p.Id == message.Id).Select(p => p.DefinitionHash).SingleOrDefaultAsync(ct);
        if (hash is null || !CryptographicOperations.FixedTimeEquals(hash, definitionHash))
            throw new MessagingException(MessagingErrorCodes.MessageIdempotencyConflict, "شناسه قبلاً برای تعریف دیگری استفاده شده است.");
        return message;
    }

    public async Task<ConversationPollResult> GetPollAsync(Guid userId, Guid conversationId, Guid messageId, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisiblePollAsync(userId, conversationId, messageId, context, ct);
        var result = await MapPollAsync(resource.Poll, resource.Message, userId, ct);
        await transaction.CommitAsync(ct); return result;
    }

    public async Task<ConversationPollResult> VotePollAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId,
        int[] numbers, byte[]? expectedVoteVersion, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisiblePollAsync(userId, conversationId, messageId, context, ct);
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        await EnsureInteractiveMessageInteractionAsync(resource.Conversation, resource.Actor, userId, context, now, ct);
        try { numbers = resource.Poll.ValidateSelection(numbers); } catch (ArgumentException ex) { throw new MessagingException(MessagingErrorCodes.PollInvalid, "انتخاب معتبر نیست.", ex); }
        var vote = await dbContext.ConversationPollVotes.SingleOrDefaultAsync(v => v.PollId == messageId && v.UserId == userId, ct);
        var selections = await dbContext.ConversationPollSelections.Where(s => s.PollId == messageId && s.UserId == userId).OrderBy(s => s.OptionNumber).ToArrayAsync(ct);
        if (vote is not null && selections.Select(s => s.OptionNumber).SequenceEqual(numbers))
        {
            var retry = await MapPollAsync(resource.Poll, resource.Message, userId, ct); await transaction.CommitAsync(ct); return retry;
        }
        if (resource.Poll.Status != ConversationPollStatus.Open) throw new MessagingException(MessagingErrorCodes.PollClosed, "نظرسنجی بسته است.");
        if (vote is not null && resource.Poll.IsQuiz) throw new MessagingException(MessagingErrorCodes.PollVoteImmutable, "پاسخ نهایی Quiz قابل تغییر نیست.");
        if (vote is null)
        {
            if (expectedVoteVersion is not null) throw PollVoteConflict();
            vote = ConversationPollVote.Create(resource.Poll, userId, now); dbContext.ConversationPollVotes.Add(vote);
        }
        else
        {
            if (expectedVoteVersion is null || !vote.RowVersion.SequenceEqual(expectedVoteVersion)) throw PollVoteConflict();
            dbContext.Entry(vote).Property(v => v.RowVersion).OriginalValue = expectedVoteVersion;
            vote.Change(now); dbContext.ConversationPollSelections.RemoveRange(selections);
        }
        resource.Poll.RecordVoteChange(now);
        // تعویض کلیدهای یکسان، دو Save در یک transaction دارد؛ هیچ snapshot نیمه‌ای قابل مشاهده نیست.
        await SavePollChangesAsync(ct);
        dbContext.ConversationPollSelections.AddRange(numbers.Select(n => ConversationPollSelection.Create(vote, n)));
        await SavePollChangesAsync(ct);
        await AppendChangeAsync(resource.Conversation, MessagingChangeType.PollChanged, messageId, VersionOf(resource.Poll.RowVersion), now, null, null, ct);
        var result = await MapPollAsync(resource.Poll, resource.Message, userId, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return result;
    }

    public async Task<ConversationPollResult> ClosePollAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId,
        byte[] expectedVersion, AccessContext context, string correlationId, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisiblePollAsync(userId, conversationId, messageId, context, ct);
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        await EnsureInteractiveMessageInteractionAsync(resource.Conversation, resource.Actor, userId, context, now, ct);
        if (resource.Poll.CreatorUserId == userId)
            await EnsureCanSendAdvancedAsync(resource.Conversation, resource.Actor, userId, context, MessageKind.Text, now, ct);
        else if (!resource.Actor.CanManage(ConversationAdminPermission.DeleteMessages)) throw PermissionRequired();
        if (resource.Poll.Status == ConversationPollStatus.Closed)
        {
            var retry = await MapPollAsync(resource.Poll, resource.Message, userId, ct); await transaction.CommitAsync(ct); return retry;
        }
        if (!resource.Poll.RowVersion.SequenceEqual(expectedVersion)) throw PollConflict();
        dbContext.Entry(resource.Poll).Property(p => p.RowVersion).OriginalValue = expectedVersion;
        resource.Poll.Close(userId, now);
        await AuditAdvancedAsync(userId, sessionId, "messaging.poll.close", "ConversationPoll", messageId.ToString("D"), "بستن نهایی نظرسنجی", correlationId, now, ct);
        await SavePollChangesAsync(ct);
        await AppendChangeAsync(resource.Conversation, MessagingChangeType.PollChanged, messageId, VersionOf(resource.Poll.RowVersion), now, null, null, ct);
        var result = await MapPollAsync(resource.Poll, resource.Message, userId, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return result;
    }

    private async Task<(Conversation Conversation, ConversationParticipant Actor, Message Message, ConversationPoll Poll)> LoadVisiblePollAsync(
        Guid userId, Guid conversationId, Guid messageId, AccessContext context, CancellationToken ct)
    {
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw PollNotFound();
        await EnsureAdvancedSessionAsync(userId, context.SessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw PollNotFound();
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        var visibleFrom = await GetVisibleFromSequenceAsync(conversationId, userId, actor.HistoryVisibleFromSequence, ct);
        var message = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId &&
            m.Kind == MessageKind.Poll && m.DeletedAtUtc == null && m.Sequence >= visibleFrom && !dbContext.MessageUserVisibilities.Any(v => v.MessageId == m.Id && v.UserId == userId), ct) ?? throw PollNotFound();
        var poll = await dbContext.ConversationPolls.SingleOrDefaultAsync(p => p.Id == messageId && p.ConversationId == conversationId, ct) ?? throw PollNotFound();
        return (conversation, actor, message, poll);
    }

    private async Task EnsurePollMetadataAuthorizedAsync(Conversation conversation, ConversationParticipant actor, Guid userId,
        AccessContext context, Guid? topicId, DateTime now, CancellationToken ct)
    {
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, now, ct);
        await EnsureTopicOpenAsync(conversation, topicId, ct);
    }

    private async Task EnsureInteractiveMessageInteractionAsync(Conversation conversation, ConversationParticipant actor, Guid userId, AccessContext context, DateTime now, CancellationToken ct)
    {
        await EnsureActorCanMessageAsync(userId, now, ct);
        if (conversation.Status != ConversationStatus.Active) throw new MessagingException(MessagingErrorCodes.ConversationArchived, "گفتگوی بایگانی‌شده تعامل تازه نمی‌پذیرد.");
        if (conversation.Type == ConversationType.Direct)
            await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, now, ct);
        else if (conversation.Type is ConversationType.Group or ConversationType.Channel)
        {
            // رأی subscriber کانال، Publish نیست؛ بااین‌حال ممنوعیت و رابطهٔ جاری معتبرند.
            await EnsureCapabilityNotRestrictedAsync(conversation.Id, userId, GroupMemberCapability.SendMessages, now, ct);
            await EnsurePersonalAudienceSafetyAsync(conversation, userId, now, ct);
            if (actor.Role == ConversationParticipantRole.Owner) await EnsurePersonalCreationSafetyAsync(userId, conversation.Scope, conversation.Type, conversation.Visibility, ct);
        }
    }

    private async Task<ConversationPollResult> MapPollAsync(ConversationPoll poll, Message message, Guid userId, CancellationToken ct)
    {
        var options = await dbContext.ConversationPollOptions.AsNoTracking().Where(o => o.PollId == poll.Id).OrderBy(o => o.Number).ToArrayAsync(ct);
        var counts = await dbContext.ConversationPollSelections.AsNoTracking().Where(s => s.PollId == poll.Id).GroupBy(s => s.OptionNumber)
            .Select(g => new { Number = g.Key, Count = g.LongCount() }).ToDictionaryAsync(g => g.Number, g => g.Count, ct);
        var total = await dbContext.ConversationPollVotes.LongCountAsync(v => v.PollId == poll.Id, ct);
        var own = await dbContext.ConversationPollVotes.AsNoTracking().SingleOrDefaultAsync(v => v.PollId == poll.Id && v.UserId == userId, ct);
        var selected = own is null ? [] : await dbContext.ConversationPollSelections.Where(s => s.PollId == poll.Id && s.UserId == userId).Select(s => s.OptionNumber).ToArrayAsync(ct);
        // پاسخ‌نداده، حتی بعد از Close یا برای سازنده، query کلید پاسخ هم ندارد.
        var answer = poll.IsQuiz && own is not null ? await dbContext.ConversationQuizAnswers.AsNoTracking().SingleAsync(k => k.PollId == poll.Id, ct) : null;
        return new(poll.Id, poll.ConversationId, message.Text!, poll.AllowsMultipleAnswers, poll.IsAnonymous, poll.IsQuiz, poll.Status,
            AdvancedUtc(poll.CreatedAtUtc), poll.ClosedAtUtc is null ? null : AdvancedUtc(poll.ClosedAtUtc.Value),
            options.Select(o => new ConversationPollOptionResult(o.Number, o.Text, counts.GetValueOrDefault(o.Number), selected.Contains(o.Number))).ToArray(),
            total, own is null ? null : Convert.ToBase64String(own.RowVersion), answer?.CorrectOptionNumber, answer?.Explanation, Convert.ToBase64String(poll.RowVersion));
    }

    private async Task SavePollChangesAsync(CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new MessagingException(MessagingErrorCodes.PollConflict, "نسخهٔ نظرسنجی تغییر کرده است.", ex); }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex)) { throw new MessagingException(MessagingErrorCodes.PollVoteConflict, "ثبت هم‌زمان رأی تکراری است.", ex); }
    }
    private static MessagingException PollNotFound() => new(MessagingErrorCodes.PollNotFound, "نظرسنجی در دسترس نیست.");
    private static MessagingException PollConflict() => new(MessagingErrorCodes.PollConflict, "نسخهٔ نظرسنجی تغییر کرده است.");
    private static MessagingException PollVoteConflict() => new(MessagingErrorCodes.PollVoteConflict, "نسخهٔ رأی از دستگاه دیگری تغییر کرده است.");
}
