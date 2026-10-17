using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<Guid?> PrepareChecklistDefinitionAsync(Guid userId, Guid conversationId, Guid messageId,
        ConversationChecklistDefinition definition, byte[] expectedVersion, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisibleChecklistAsync(userId, conversationId, messageId, context, ct, tracking: false);
        await EnsureChecklistDefinitionPermissionAsync(resource.Conversation, resource.Actor, resource.Message, userId, context, timeProvider.GetUtcNow().UtcDateTime, ct);
        RequireChecklistVersion(resource.Checklist, expectedVersion);
        await ValidateChecklistStructureAsync(resource.Checklist, definition, ct, tracking: false);
        await transaction.CommitAsync(ct); return resource.Message.TopicId;
    }

    public async Task<ConversationChecklistResult> UpdateChecklistDefinitionAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId,
        ConversationChecklistDefinition definition, byte[] expectedVersion, bool safetyEvaluated, AccessContext context, string correlationId, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisibleChecklistAsync(userId, conversationId, messageId, context, ct);
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        await EnsureChecklistDefinitionPermissionAsync(resource.Conversation, resource.Actor, resource.Message, userId, context, now, ct);
        RequireChecklistVersion(resource.Checklist, expectedVersion);
        var existing = await ValidateChecklistStructureAsync(resource.Checklist, definition, ct);
        await EnsureSafetyGateSatisfiedAsync(resource.Conversation, safetyEvaluated, ct);
        resource.Checklist.AdvanceDefinition(definition.Mode, now);
        var activeIds = definition.Items.Select(i => i.ItemId).ToHashSet();
        foreach (var item in existing.Where(i => i.IsActive && !activeIds.Contains(i.Id))) item.Retire(resource.Checklist.DefinitionRevision);
        for (var index = 0; index < definition.Items.Count; index++)
        {
            var item = definition.Items[index]; var persisted = existing.SingleOrDefault(i => i.Id == item.ItemId);
            if (persisted is null) dbContext.ConversationChecklistItems.Add(ConversationChecklistItem.Create(resource.Checklist, item, index + 1));
            else persisted.Move(index + 1);
        }
        var revision = resource.Message.EditChecklistTitle(definition.Title, now);
        dbContext.MessageRevisions.Add(MessageRevision.Create(messageId, revision, definition.Title, userId, now));
        await AuditAdvancedAsync(userId, sessionId, "messaging.checklist.definition.changed", "ConversationChecklist", messageId.ToString("D"),
            "ویرایش نسخه‌دار تعریف چک‌لیست", correlationId, now, ct);
        await SaveChecklistChangesAsync(ct);
        await AppendChangeAsync(resource.Conversation, MessagingChangeType.ChecklistChanged, messageId, VersionOf(resource.Checklist.RowVersion), now, userId, null, ct);
        var result = await MapChecklistAsync(resource.Checklist, resource.Message, userId, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return result;
    }

    private async Task<ConversationChecklistItem[]> ValidateChecklistStructureAsync(ConversationChecklist checklist, ConversationChecklistDefinition definition, CancellationToken ct, bool tracking = true)
    {
        var query = tracking ? dbContext.ConversationChecklistItems.AsQueryable() : dbContext.ConversationChecklistItems.AsNoTracking();
        var existing = await query.Where(i => i.ChecklistId == checklist.Id).ToArrayAsync(ct);
        // این تصمیم مستقل از وجود تیک دیگران است تا خطای ویرایش، فعالیت شخصی آن‌ها را افشا نکند.
        if (checklist.Mode != definition.Mode || definition.Items.Any(i => existing.Any(e => e.Id == i.ItemId && (!e.IsActive || e.Text != i.Text))))
            throw new MessagingException(MessagingErrorCodes.ChecklistStructureImmutable, "Mode و معنای شناسهٔ منتشرشده ثابت‌اند؛ جایگزین را با شناسهٔ تازه بسازید.");
        return existing;
    }

    private async Task EnsureChecklistDefinitionPermissionAsync(Conversation conversation, ConversationParticipant actor, Message message,
        Guid userId, AccessContext context, DateTime now, CancellationToken ct)
    {
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, now, ct);
        await EnsureTopicOpenAsync(conversation, message.TopicId, ct);
        if (message.SenderUserId == userId)
        {
            if (!message.IsWithinSenderMutationWindow(now))
                throw new MessagingException(MessagingErrorCodes.MessageEditWindowExpired, "مهلت یک‌ساعتهٔ ویرایش سازنده پایان یافته است.");
        }
        else if (conversation.ManagementPolicy != ConversationManagementPolicy.UserManaged ||
            conversation.Type is not (ConversationType.Group or ConversationType.Channel) || !actor.CanManage(ConversationAdminPermission.DeleteMessages))
            throw PermissionRequired();
    }

    private async Task EnsureChecklistMetadataAuthorizedAsync(Conversation conversation, ConversationParticipant actor, Guid userId,
        AccessContext context, SendMessageStoreCommand command, DateTime now, CancellationToken ct)
    {
        if (command.SafetyResourceId.HasValue)
        {
            var resource = await LoadChecklistResourceAsync(conversation, actor, userId, command.SafetyResourceId.Value, context, ct, tracking: false);
            await EnsureChecklistDefinitionPermissionAsync(conversation, actor, resource.Message, userId, context, now, ct);
        }
        else
        {
            await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, MessageKind.Text, now, ct);
            await EnsureTopicOpenAsync(conversation, command.TopicId, ct);
        }
    }
}
