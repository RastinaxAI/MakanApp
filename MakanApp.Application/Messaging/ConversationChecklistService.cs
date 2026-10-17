using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class ConversationChecklistService(IAccessContextResolver contexts, IConversationChecklistStore checklists,
    IMessagingStore messages, IContentSafetyProvider safety, TimeProvider clock)
{
    public async Task<MessageReceiptResult> CreateAsync(Guid userId, Guid sessionId, Guid conversationId,
        CreateConversationChecklistCommand command, CancellationToken ct)
    {
        if (conversationId == Guid.Empty || command.ClientMessageId == Guid.Empty || command.TopicId == Guid.Empty) throw Invalid();
        var definition = Definition(command.Title, command.Mode, command.Items);
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { definition.Title, definition.Mode, definition.Items, command.TopicId, command.IsSilent }));
        var message = new SendMessageStoreCommand(command.ClientMessageId, MessageKind.Checklist, definition.Title, [], null, [], TopicId: command.TopicId, IsSilent: command.IsSilent);
        var existing = await checklists.PrepareChecklistCreationAsync(userId, conversationId, message, hash, context, ct);
        if (existing is not null) return MessagingService.ToReceipt(existing);
        var gate = await EvaluateAsync(userId, conversationId, context, command.ClientMessageId, definition, command.TopicId, null, hash, ct);
        return MessagingService.ToReceipt(await checklists.CreateChecklistAsync(userId, sessionId, conversationId, message, definition, hash, gate, context, ct));
    }

    public async Task<ConversationChecklistResult> GetAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, CancellationToken ct) =>
        await checklists.GetChecklistAsync(userId, conversationId, messageId, await contexts.ResolveAsync(userId, sessionId, ct), ct);

    public async Task<ConversationChecklistResult> SetItemAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, Guid itemId,
        SetChecklistItemCommand command, CancellationToken ct)
    {
        if (itemId == Guid.Empty || !command.IsChecked.HasValue) throw Invalid();
        var definitionVersion = Version(command.ExpectedDefinitionVersion)!;
        var stateVersion = Version(command.ExpectedStateVersion, true);
        return await checklists.SetChecklistItemAsync(userId, sessionId, conversationId, messageId, itemId, command.IsChecked.Value,
            definitionVersion, stateVersion, await contexts.ResolveAsync(userId, sessionId, ct), ct);
    }

    public async Task<ConversationChecklistResult> UpdateAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId,
        UpdateConversationChecklistCommand command, string correlationId, CancellationToken ct)
    {
        var definition = Definition(command.Title, command.Mode, command.Items);
        var version = Version(command.ExpectedVersion)!;
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        var topicId = await checklists.PrepareChecklistDefinitionAsync(userId, conversationId, messageId, definition, version, context, ct);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { messageId, command.ExpectedVersion, definition.Title, definition.Mode, definition.Items }));
        var evaluated = await EvaluateAsync(userId, conversationId, context, messageId, definition, topicId, messageId, hash, ct);
        return await checklists.UpdateChecklistDefinitionAsync(userId, sessionId, conversationId, messageId, definition, version, evaluated, context, correlationId, ct);
    }

    private async Task<bool> EvaluateAsync(Guid userId, Guid conversationId, AccessContext context, Guid operationId,
        ConversationChecklistDefinition definition, Guid? topicId, Guid? messageId, byte[] hash, CancellationToken ct)
    {
        var attempt = SHA256.HashData(operationId.ToByteArray().Concat(hash).ToArray());
        var command = new SendMessageStoreCommand(new Guid(attempt.AsSpan(0, 16)), MessageKind.Text, definition.ContentText, [], null, [],
            TopicId: topicId, SafetyPurpose: MessageSafetyPurpose.ChecklistMetadata, SafetyResourceId: messageId);
        return (await MessageContentSafetyGate.ApplyAsync(messages, safety, clock, userId, conversationId, context, command,
            MessagingService.HashSendPayload(conversationId, command), ct)).Evaluated;
    }

    private static ConversationChecklistDefinition Definition(string title, ConversationChecklistMode? mode, IReadOnlyCollection<ChecklistItemInput> items)
    {
        try
        {
            if (mode is null || items is null || items.Any(i => i is null)) throw new ArgumentException("تعریف صریح الزامی است.");
            return ConversationChecklistDefinition.Create(title, mode.Value, items.Select(i => new ConversationChecklistItemDefinition(i.ItemId, i.Text)).ToArray());
        }
        catch (ArgumentException ex) { throw Invalid(ex); }
    }

    private static byte[]? Version(string? value, bool optional = false)
    {
        if (value is null && optional) return null;
        try { if (value is not null) { var bytes = Convert.FromBase64String(value); if (bytes.Length == 8) return bytes; } }
        catch (FormatException) { }
        throw Invalid();
    }
    private static MessagingException Invalid(Exception? ex = null) => new(MessagingErrorCodes.ChecklistInvalid, "درخواست چک‌لیست معتبر نیست.", ex);
}
