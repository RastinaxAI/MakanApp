using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record FolderOverrideCommand(Guid ConversationId, bool Included, int? PinOrder = null);
public sealed record SaveChatFolderCommand(string Name, int SortOrder, FolderConversationTypes IncludedTypes,
    FolderConversationTypes ExcludedTypes, bool UnreadOnly, IReadOnlyCollection<FolderOverrideCommand> Overrides, string? ExpectedVersion);
public sealed record ChatFolderResult(Guid Id, string Name, int SortOrder, FolderConversationTypes IncludedTypes,
    FolderConversationTypes ExcludedTypes, bool UnreadOnly, IReadOnlyCollection<FolderOverrideCommand> Overrides, string Version);
public sealed record ReplaceSavedLabelsCommand(IReadOnlyCollection<string> Labels, string? ExpectedVersion);
public sealed record SavedMessageLabelsResult(Guid MessageId, IReadOnlyCollection<string> Labels, string? Version);

public interface IMessagingPresentationStore
{
    Task<IReadOnlyCollection<ChatFolderResult>> GetFoldersAsync(Guid userId, CancellationToken cancellationToken);
    Task<ChatFolderResult> SaveFolderAsync(Guid userId, Guid folderId, SaveChatFolderCommand command, byte[]? expectedVersion,
        AccessContext context, CancellationToken cancellationToken);
    Task DeleteFolderAsync(Guid userId, Guid folderId, byte[] expectedVersion, AccessContext context, CancellationToken cancellationToken);
    Task<SavedMessageLabelsResult> GetSavedLabelsAsync(Guid userId, Guid messageId, AccessContext context, CancellationToken cancellationToken);
    Task<SavedMessageLabelsResult> ReplaceSavedLabelsAsync(Guid userId, Guid messageId, string[] labels, byte[]? expectedVersion,
        AccessContext context, CancellationToken cancellationToken);
}
