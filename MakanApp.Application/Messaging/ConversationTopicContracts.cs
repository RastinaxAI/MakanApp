using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record CreateConversationTopicCommand(Guid ClientTopicId, string Title);
public sealed record UpdateConversationTopicCommand(string Title, ConversationTopicStatus Status, string ExpectedVersion);
public sealed record ConversationTopicResult(Guid TopicId, Guid ConversationId, string Title, Guid CreatedByUserId,
    ConversationTopicStatus Status, DateTime CreatedAtUtc, string Version, bool AlreadyExisted = false);
public sealed record ConversationTopicPageResult(IReadOnlyCollection<ConversationTopicResult> Items, bool HasMore, bool CanManage);
public interface IConversationTopicStore
{
    Task<ConversationTopicPageResult> GetTopicsAsync(Guid userId, Guid conversationId, AccessContext context, int skip, int take, CancellationToken ct);
    Task<bool> PrepareTopicAsync(Guid userId, Guid conversationId, Guid topicId, string title,
        byte[]? expectedVersion, AccessContext context, CancellationToken ct);
    Task<ConversationTopicResult> CreateTopicAsync(Guid userId, Guid sessionId, Guid conversationId, Guid topicId, string title,
        bool safetyEvaluated, AccessContext context, string correlationId, CancellationToken ct);
    Task<ConversationTopicResult> UpdateTopicAsync(Guid userId, Guid sessionId, Guid conversationId, Guid topicId,
        UpdateConversationTopicCommand command, byte[] expectedVersion, bool safetyEvaluated, AccessContext context, string correlationId, CancellationToken ct);
}
