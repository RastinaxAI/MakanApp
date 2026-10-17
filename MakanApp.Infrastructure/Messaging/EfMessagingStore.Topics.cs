using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IConversationTopicStore
{
    public async Task<ConversationTopicPageResult> GetTopicsAsync(Guid userId, Guid conversationId, AccessContext context, int skip, int take, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        if (conversation.Type != ConversationType.Group) throw TopicNotFound();
        var visibleFrom = await GetVisibleFromSequenceAsync(conversationId, userId, actor.HistoryVisibleFromSequence, ct);
        var rows = await dbContext.ConversationTopics.AsNoTracking().Where(t => t.ConversationId == conversationId &&
            (visibleFrom <= 1 || t.CreatedAtUtc >= actor.JoinedAtUtc || dbContext.Messages.Any(m => m.TopicId == t.Id &&
                m.Sequence >= visibleFrom && !dbContext.MessageUserVisibilities.Any(h => h.MessageId == m.Id && h.UserId == userId))))
            .OrderBy(t => t.CreatedAtUtc).ThenBy(t => t.Id).Skip(skip).Take(take + 1).ToArrayAsync(ct);
        return new(rows.Take(take).Select(t => MapTopic(t)).ToArray(), rows.Length > take,
            await CanManageTopicsAsync(conversation, actor, userId, context, ct));
    }

    public async Task<bool> PrepareTopicAsync(Guid userId, Guid conversationId, Guid topicId, string title, byte[]? expectedVersion, AccessContext context, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureTopicManagementAsync(conversation, actor, userId, context, timeProvider.GetUtcNow().UtcDateTime, ct);
        var topic = await dbContext.ConversationTopics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == topicId, ct);
        ValidateTopicMutation(topic, conversationId, userId, title, expectedVersion);
        return expectedVersion is null && topic is not null;
    }

    public async Task<ConversationTopicResult> CreateTopicAsync(Guid userId, Guid sessionId, Guid conversationId, Guid topicId, string title,
        bool safetyEvaluated, AccessContext context, string correlationId, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ConversationNotFound();
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureTopicManagementAsync(conversation, actor, userId, context, now, ct);
        var existing = await dbContext.ConversationTopics.SingleOrDefaultAsync(t => t.Id == topicId, ct);
        ValidateTopicMutation(existing, conversationId, userId, title, null);
        if (existing is not null) { await transaction.CommitAsync(ct); return MapTopic(existing, true); }
        await EnsureSafetyGateSatisfiedAsync(conversation, safetyEvaluated, ct);
        var topic = ConversationTopic.Create(topicId, conversation, userId, title, now);
        dbContext.ConversationTopics.Add(topic);
        await FinishTopicMutationAsync(conversation, topic, userId, sessionId, correlationId, now, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return MapTopic(topic);
    }

    public async Task<ConversationTopicResult> UpdateTopicAsync(Guid userId, Guid sessionId, Guid conversationId, Guid topicId,
        UpdateConversationTopicCommand command, byte[] expectedVersion, bool safetyEvaluated, AccessContext context, string correlationId, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ConversationNotFound();
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureTopicManagementAsync(conversation, actor, userId, context, now, ct);
        var topic = await dbContext.ConversationTopics.SingleOrDefaultAsync(t => t.Id == topicId, ct);
        ValidateTopicMutation(topic, conversationId, userId, command.Title, expectedVersion);
        await EnsureSafetyGateSatisfiedAsync(conversation, safetyEvaluated, ct);
        dbContext.Entry(topic!).Property(t => t.RowVersion).OriginalValue = expectedVersion;
        topic!.Update(command.Title, command.Status, now);
        await FinishTopicMutationAsync(conversation, topic, userId, sessionId, correlationId, now, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal(); return MapTopic(topic);
    }

    private async Task FinishTopicMutationAsync(Conversation conversation, ConversationTopic topic, Guid userId, Guid sessionId,
        string correlationId, DateTime now, CancellationToken ct)
    {
        await AuditAdvancedAsync(userId, sessionId, "messaging.topic.changed", "ConversationTopic", topic.Id.ToString("D"),
            "تغییر موضوع گفتگو", correlationId, now, ct);
        await SaveManagementAsync(ct);
        await AppendChangeAsync(conversation, MessagingChangeType.TopicChanged, topic.Id, VersionOf(topic.RowVersion), now, userId, null, ct);
    }

    private static void ValidateTopicMutation(ConversationTopic? topic, Guid conversationId, Guid userId, string title, byte[]? expectedVersion)
    {
        if (topic is null) { if (expectedVersion is not null) throw TopicNotFound(); return; }
        if (topic.ConversationId != conversationId) throw TopicNotFound();
        if (expectedVersion is null)
        {
            if (topic.CreatedByUserId != userId || topic.Title != title) throw StateConflict();
        }
        else if (!topic.RowVersion.SequenceEqual(expectedVersion)) throw StateConflict();
    }

    private async Task<bool> CanManageTopicsAsync(Conversation conversation, ConversationParticipant actor, Guid userId, AccessContext context, CancellationToken ct)
    {
        if (conversation.Type != ConversationType.Group || conversation.Status != ConversationStatus.Active) return false;
        if (conversation.ManagementPolicy == ConversationManagementPolicy.UserManaged)
            return actor.CanManage(ConversationAdminPermission.ManageTopics);
        // اختیار معلم از assignment جاری کلاس است، نه ارتقای ساختگی participant به Admin.
        return conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && context.ActiveRole == OrganizationRole.Teacher &&
            await MessengerRelationshipQuery.ClassMembers(dbContext).AnyAsync(m => m.ClassId == conversation.AcademicClassId &&
                m.OrganizationId == conversation.OrganizationId && m.UserId == userId && m.IsTeacher, ct);
    }

    private async Task EnsureTopicManagementAsync(Conversation conversation, ConversationParticipant actor, Guid userId, AccessContext context, DateTime now, CancellationToken ct)
    {
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        if (!await CanManageTopicsAsync(conversation, actor, userId, context, ct)) throw PermissionRequired();
        await EnsureActorCanMessageAsync(userId, now, ct);
        await EnsureNotBannedAsync(conversation.Id, userId, now, ct);
        await EnsurePersonalAudienceSafetyAsync(conversation, userId, now, ct);
        if (actor.Role == ConversationParticipantRole.Owner)
            await EnsurePersonalCreationSafetyAsync(userId, conversation.Scope, conversation.Type, conversation.Visibility, ct);
    }

    private Task EnsureSafetyOperationAuthorizedAsync(Conversation conversation, ConversationParticipant actor, Guid userId, AccessContext context,
        SendMessageStoreCommand command, DateTime now, CancellationToken ct) => command.SafetyPurpose switch
        {
            MessageSafetyPurpose.TopicMetadata => EnsureTopicManagementAsync(conversation, actor, userId, context, now, ct),
            MessageSafetyPurpose.ConversationMetadata => EnsureInfoManagementAsync(conversation, actor, userId, context, now, ct),
            MessageSafetyPurpose.PollMetadata => EnsurePollMetadataAuthorizedAsync(conversation, actor, userId, context, command.TopicId, now, ct),
            MessageSafetyPurpose.ChecklistMetadata => EnsureChecklistMetadataAuthorizedAsync(conversation, actor, userId, context, command, now, ct),
            MessageSafetyPurpose.Message => EnsureCanSendAdvancedAsync(conversation, actor, userId, context, command.Kind, now, ct),
            _ => throw new MessagingException(MessagingErrorCodes.MessageKindInvalid, "نوع عملیات ایمنی معتبر نیست.")
        };

    private async Task EnsureTopicOpenAsync(Conversation conversation, Guid? topicId, CancellationToken ct)
    {
        if (!topicId.HasValue) return;
        if (conversation.Type != ConversationType.Group) throw TopicNotFound();
        var topic = await dbContext.ConversationTopics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == topicId && t.ConversationId == conversation.Id, ct) ?? throw TopicNotFound();
        if (topic.Status != ConversationTopicStatus.Open) throw new MessagingException(MessagingErrorCodes.TopicClosed, "موضوع بسته است.");
    }
    private static ConversationTopicResult MapTopic(ConversationTopic topic, bool alreadyExisted = false) => new(topic.Id, topic.ConversationId, topic.Title,
        topic.CreatedByUserId, topic.Status, AdvancedUtc(topic.CreatedAtUtc), Convert.ToBase64String(topic.RowVersion), alreadyExisted);
    private static MessagingException TopicNotFound() => new(MessagingErrorCodes.TopicNotFound, "موضوع در دسترس نیست.");
}
