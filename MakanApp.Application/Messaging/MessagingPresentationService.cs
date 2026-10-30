using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class MessagingPresentationService(IAccessContextResolver contexts, IMessagingPresentationStore store,
    MessagingOperationContextResolver? operationContexts = null)
{
    private const int MaximumRawPresentationTextLength = 1024;
    public async Task<IReadOnlyCollection<ChatFolderResult>> GetFoldersAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await ResolveAccountAsync(userId, sessionId, cancellationToken);
        return await store.GetFoldersAsync(userId, cancellationToken);
    }

    public async Task<ChatFolderResult> SaveFolderAsync(Guid userId, Guid sessionId, Guid folderId, SaveChatFolderCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (command.Name is null || command.Name.Length > MaximumRawPresentationTextLength) throw new ArgumentException("ورودی نام معتبر نیست یا بیش از حد طولانی است.");
            var candidate = ChatFolder.Create(folderId, userId);
            candidate.Update(command.Name, command.SortOrder, command.IncludedTypes, command.ExcludedTypes, command.UnreadOnly);
            if (command.Overrides is null || command.Overrides.Count > ChatFolder.MaximumOverrides || command.Overrides.Any(o => o is null) ||
                command.Overrides.Select(o => o.ConversationId).Distinct().Count() != command.Overrides.Count)
                throw new ArgumentException("انتخاب گفتگوها معتبر نیست.");
            foreach (var item in command.Overrides) FolderConversationOverride.Create(folderId, item.ConversationId, item.Included, item.PinOrder);
        }
        catch (ArgumentException ex) { throw new MessagingException(MessagingErrorCodes.PresentationInvalid, "پوشه معتبر نیست.", ex); }
        var expected = Version(command.ExpectedVersion);
        var context = operationContexts is null ? await contexts.ResolveAsync(userId, sessionId, cancellationToken)
            : await operationContexts.FolderAsync(userId, sessionId, command.Overrides, cancellationToken);
        return await store.SaveFolderAsync(userId, folderId, command, expected, context, cancellationToken);
    }

    public async Task DeleteFolderAsync(Guid userId, Guid sessionId, Guid folderId, string expectedVersion, CancellationToken cancellationToken)
    {
        var version = Version(expectedVersion) ?? throw new MessagingException(MessagingErrorCodes.PresentationInvalid, "نسخه الزامی است.");
        var context = await ResolveAccountAsync(userId, sessionId, cancellationToken);
        await store.DeleteFolderAsync(userId, folderId, version, context, cancellationToken);
    }

    public async Task<SavedMessageLabelsResult> GetSavedLabelsAsync(Guid userId, Guid sessionId, Guid messageId, CancellationToken cancellationToken)
    {
        var context = await ResolveAccountAsync(userId, sessionId, cancellationToken);
        return await store.GetSavedLabelsAsync(userId, messageId, context, cancellationToken);
    }

    public async Task<SavedMessageLabelsResult> ReplaceSavedLabelsAsync(Guid userId, Guid sessionId, Guid messageId,
        ReplaceSavedLabelsCommand command, CancellationToken cancellationToken)
    {
        string[] labels;
        try
        {
            if (command.Labels is null || command.Labels.Count > SavedMessageLabel.MaximumLabels || command.Labels.Any(l => l is null || l.Length > MaximumRawPresentationTextLength)) throw new ArgumentException("برچسب خام معتبر نیست.");
            labels = command.Labels.Select(SavedMessageLabel.Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
        catch (ArgumentException ex) { throw new MessagingException(MessagingErrorCodes.PresentationInvalid, "برچسب معتبر نیست.", ex); }
        var context = await ResolveAccountAsync(userId, sessionId, cancellationToken);
        return await store.ReplaceSavedLabelsAsync(userId, messageId, labels, Version(command.ExpectedVersion), context, cancellationToken);
    }

    private static byte[]? Version(string? encoded)
    {
        if (encoded is null) return null;
        try { var value = Convert.FromBase64String(encoded); if (value.Length == 8) return value; }
        catch (FormatException) { }
        throw new MessagingException(MessagingErrorCodes.PresentationInvalid, "نسخه معتبر نیست.");
    }
    private Task<AccessContext> ResolveAccountAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        operationContexts is null ? contexts.ResolveAsync(userId, sessionId, ct) : operationContexts.PersonalAsync(userId, sessionId, ct);
}
