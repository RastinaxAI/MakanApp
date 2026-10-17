using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record ChecklistItemInput(Guid ItemId, string Text);
public sealed record CreateConversationChecklistCommand(Guid ClientMessageId, string Title, ConversationChecklistMode? Mode,
    IReadOnlyCollection<ChecklistItemInput> Items, Guid? TopicId = null, bool IsSilent = false);
public sealed record UpdateConversationChecklistCommand(string Title, ConversationChecklistMode? Mode,
    IReadOnlyCollection<ChecklistItemInput> Items, string ExpectedVersion);
public sealed record SetChecklistItemCommand(bool? IsChecked, string ExpectedDefinitionVersion, string? ExpectedStateVersion = null);
public sealed record ConversationChecklistItemResult(Guid ItemId, string Text, bool IsChecked, string? StateVersion);
public sealed record ConversationChecklistResult(Guid MessageId, Guid ConversationId, string Title, ConversationChecklistMode Mode,
    IReadOnlyCollection<ConversationChecklistItemResult> Items, bool IsCompleted, string Version, string MessageVersion);

public interface IConversationChecklistStore
{
    Task<Message?> PrepareChecklistCreationAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command,
        byte[] creationHash, AccessContext context, CancellationToken ct);
    Task<Message> CreateChecklistAsync(Guid userId, Guid sessionId, Guid conversationId, SendMessageStoreCommand command,
        ConversationChecklistDefinition definition, byte[] creationHash, bool safetyEvaluated, AccessContext context, CancellationToken ct);
    Task<ConversationChecklistResult> GetChecklistAsync(Guid userId, Guid conversationId, Guid messageId, AccessContext context, CancellationToken ct);
    Task<ConversationChecklistResult> SetChecklistItemAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, Guid itemId,
        bool isChecked, byte[] definitionVersion, byte[]? stateVersion, AccessContext context, CancellationToken ct);
    Task<Guid?> PrepareChecklistDefinitionAsync(Guid userId, Guid conversationId, Guid messageId, ConversationChecklistDefinition definition,
        byte[] expectedVersion, AccessContext context, CancellationToken ct);
    Task<ConversationChecklistResult> UpdateChecklistDefinitionAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId,
        ConversationChecklistDefinition definition, byte[] expectedVersion, bool safetyEvaluated, AccessContext context, string correlationId, CancellationToken ct);
}
