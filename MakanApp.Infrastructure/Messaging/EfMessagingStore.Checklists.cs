using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IConversationChecklistStore
{
    public async Task<Message?> PrepareChecklistCreationAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command,
        byte[] creationHash, AccessContext context, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, timeProvider.GetUtcNow().UtcDateTime, ct);
        var existing = await FindExistingChecklistCreationAsync(userId, conversationId, command, creationHash, ct);
        if (existing is not null) return existing;
        await EnsureTopicOpenAsync(conversation, command.TopicId, ct);
        await ValidateScheduledSendAsync(userId, conversationId, command, false, ct);
        return null;
    }

    public async Task<Message> CreateChecklistAsync(Guid userId, Guid sessionId, Guid conversationId, SendMessageStoreCommand command,
        ConversationChecklistDefinition definition, byte[] creationHash, bool safetyEvaluated, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ConversationNotFound();
        await dbContext.Entry(conversation).ReloadAsync(ct);
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await dbContext.Entry(actor).ReloadAsync(ct);
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, now, ct);
        var existing = await FindExistingChecklistCreationAsync(userId, conversationId, command, creationHash, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return existing; }
        await EnsureTopicOpenAsync(conversation, command.TopicId, ct);
        await EnsureSafetyGateSatisfiedAsync(conversation, safetyEvaluated, ct);
        var created = await CreateMessageAsync(conversation, actor, userId, command, Message.StorageMaximumTextLength, now, ct);
        var checklist = ConversationChecklist.Create(created.Message, definition, creationHash);
        dbContext.ConversationChecklists.Add(checklist);
        dbContext.ConversationChecklistItems.AddRange(definition.Items.Select((item, index) => ConversationChecklistItem.Create(checklist, item, index + 1)));
        await SaveChecklistChangesAsync(ct);
        await EnqueueMessageNotificationsAsync(conversation, created.Message, userId, now, ct);
        await AppendChangeAsync(conversation, MessagingChangeType.MessageCreated, created.Message.Id, VersionOf(created.Message.RowVersion), now, userId, null, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return created.Message;
    }

    private async Task<Message?> FindExistingChecklistCreationAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command, byte[] hash, CancellationToken ct)
    {
        var message = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.SenderUserId == userId && m.ClientMessageId == command.ClientMessageId, ct);
        if (message is null) return null;
        // fingerprint ساخت ثابت می‌ماند، حتی اگر تعریف بعداً با مجوز ویرایش شده باشد.
        var original = await dbContext.ConversationChecklists.AsNoTracking().Where(c => c.Id == message.Id).Select(c => c.CreationHash).SingleOrDefaultAsync(ct);
        if (original is null || message.Kind != MessageKind.Checklist || !CryptographicOperations.FixedTimeEquals(original, hash))
            throw new MessagingException(MessagingErrorCodes.MessageIdempotencyConflict, "شناسه قبلاً برای تعریف دیگری استفاده شده است.");
        return message;
    }

    public async Task<ConversationChecklistResult> GetChecklistAsync(Guid userId, Guid conversationId, Guid messageId, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisibleChecklistAsync(userId, conversationId, messageId, context, ct);
        var result = await MapChecklistAsync(resource.Checklist, resource.Message, userId, ct);
        await transaction.CommitAsync(ct); return result;
    }

    public async Task<ConversationChecklistResult> SetChecklistItemAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId,
        Guid itemId, bool isChecked, byte[] definitionVersion, byte[]? stateVersion, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisibleChecklistAsync(userId, conversationId, messageId, context, ct);
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        await EnsureInteractiveMessageInteractionAsync(resource.Conversation, resource.Actor, userId, context, now, ct);
        RequireChecklistVersion(resource.Checklist, definitionVersion);
        var item = await dbContext.ConversationChecklistItems.SingleOrDefaultAsync(i => i.ChecklistId == messageId && i.Id == itemId && i.RetiredAtRevision == null, ct) ?? throw ChecklistNotFound();
        ConversationChecklistPersonalState? own = null;
        if (resource.Checklist.Mode == ConversationChecklistMode.Personal)
            own = await dbContext.ConversationChecklistPersonalStates.SingleOrDefaultAsync(s => s.ChecklistId == messageId && s.ItemId == itemId && s.UserId == userId, ct);
        var current = resource.Checklist.Mode == ConversationChecklistMode.Personal ? own?.IsChecked ?? false : item.IsChecked;
        if (current == isChecked)
        {
            var retry = await MapChecklistAsync(resource.Checklist, resource.Message, userId, ct);
            await transaction.CommitAsync(ct); return retry;
        }
        var currentVersion = resource.Checklist.Mode == ConversationChecklistMode.Personal ? own?.RowVersion : item.RowVersion;
        if (currentVersion is null ? stateVersion is not null : stateVersion is null || !currentVersion.SequenceEqual(stateVersion))
            throw new MessagingException(MessagingErrorCodes.ChecklistStateConflict, "وضعیت آیتم در دستگاه دیگری تغییر کرده است.");
        if (resource.Checklist.Mode == ConversationChecklistMode.Personal)
        {
            if (own is null)
            {
                own = ConversationChecklistPersonalState.Create(item, userId, isChecked);
                dbContext.ConversationChecklistPersonalStates.Add(own);
            }
            else own.SetChecked(isChecked);
        }
        else item.SetSharedChecked(isChecked);
        await SaveChecklistChangesAsync(ct);
        var eventVersion = own?.RowVersion ?? item.RowVersion;
        await AppendChangeAsync(resource.Conversation, MessagingChangeType.ChecklistChanged, messageId, VersionOf(eventVersion), now,
            null, resource.Checklist.Mode == ConversationChecklistMode.Personal ? userId : null, ct);
        var result = await MapChecklistAsync(resource.Checklist, resource.Message, userId, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return result;
    }

    private async Task<(Conversation Conversation, ConversationParticipant Actor, Message Message, ConversationChecklist Checklist)> LoadVisibleChecklistAsync(
        Guid userId, Guid conversationId, Guid messageId, AccessContext context, CancellationToken ct, bool tracking = true)
    {
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ChecklistNotFound();
        // preflight و provider در همین scope اجرا می‌شوند؛ identity map مجوز cacheشده نیست.
        await dbContext.Entry(conversation).ReloadAsync(ct);
        await EnsureAdvancedSessionAsync(userId, context.SessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ChecklistNotFound();
        await dbContext.Entry(actor).ReloadAsync(ct);
        var resource = await LoadChecklistResourceAsync(conversation, actor, userId, messageId, context, ct, tracking);
        return (conversation, actor, resource.Message, resource.Checklist);
    }

    private async Task<(Message Message, ConversationChecklist Checklist)> LoadChecklistResourceAsync(Conversation conversation,
        ConversationParticipant actor, Guid userId, Guid messageId, AccessContext context, CancellationToken ct, bool tracking = true)
    {
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        var visibleFrom = await GetVisibleFromSequenceAsync(conversation.Id, userId, actor.HistoryVisibleFromSequence, ct);
        var messages = tracking ? dbContext.Messages.AsQueryable() : dbContext.Messages.AsNoTracking();
        var checklists = tracking ? dbContext.ConversationChecklists.AsQueryable() : dbContext.ConversationChecklists.AsNoTracking();
        var message = await messages.SingleOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversation.Id &&
            m.Kind == MessageKind.Checklist && m.DeletedAtUtc == null && m.Sequence >= visibleFrom &&
            !dbContext.MessageUserVisibilities.Any(v => v.MessageId == m.Id && v.UserId == userId), ct) ?? throw ChecklistNotFound();
        var checklist = await checklists.SingleOrDefaultAsync(c => c.Id == messageId && c.ConversationId == conversation.Id, ct) ?? throw ChecklistNotFound();
        return (message, checklist);
    }

    private async Task<ConversationChecklistResult> MapChecklistAsync(ConversationChecklist checklist, Message message, Guid userId, CancellationToken ct)
    {
        var items = await dbContext.ConversationChecklistItems.AsNoTracking().Where(i => i.ChecklistId == checklist.Id && i.RetiredAtRevision == null)
            .OrderBy(i => i.Position).ThenBy(i => i.Id).ToArrayAsync(ct);
        // حتی سازنده و Admin هم query وضعیت شخصیِ دیگران ندارند.
        var own = checklist.Mode == ConversationChecklistMode.Personal
            ? await dbContext.ConversationChecklistPersonalStates.AsNoTracking().Where(s => s.ChecklistId == checklist.Id && s.UserId == userId).ToDictionaryAsync(s => s.ItemId, ct)
            : new Dictionary<Guid, ConversationChecklistPersonalState>();
        var results = items.Select(i => new ConversationChecklistItemResult(i.Id, i.Text,
            checklist.Mode == ConversationChecklistMode.Personal ? own.GetValueOrDefault(i.Id)?.IsChecked ?? false : i.IsChecked,
            checklist.Mode == ConversationChecklistMode.Personal ? own.TryGetValue(i.Id, out var state) ? Convert.ToBase64String(state.RowVersion) : null : Convert.ToBase64String(i.RowVersion))).ToArray();
        return new(checklist.Id, checklist.ConversationId, message.Text!, checklist.Mode, results,
            results.Length > 0 && results.All(i => i.IsChecked), Convert.ToBase64String(checklist.RowVersion), Convert.ToBase64String(message.RowVersion));
    }

    private static void RequireChecklistVersion(ConversationChecklist checklist, byte[] expectedVersion)
    {
        if (!checklist.RowVersion.SequenceEqual(expectedVersion)) throw new MessagingException(MessagingErrorCodes.ChecklistConflict, "نسخهٔ تعریف تغییر کرده است.");
    }

    private async Task SaveChecklistChangesAsync(CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new MessagingException(MessagingErrorCodes.ChecklistStateConflict, "نسخهٔ چک‌لیست تغییر کرده است.", ex); }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex)) { throw new MessagingException(MessagingErrorCodes.ChecklistStateConflict, "ثبت هم‌زمان تکراری است.", ex); }
    }
    private static MessagingException ChecklistNotFound() => new(MessagingErrorCodes.ChecklistNotFound, "چک‌لیست یا آیتم در دسترس نیست.");
}
