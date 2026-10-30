using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class ConversationPollService(IAccessContextResolver contexts, IConversationPollStore polls,
    IMessagingStore messages, IContentSafetyProvider safety, TimeProvider clock, MessagingOperationContextResolver? operationContexts = null)
{
    public async Task<MessageReceiptResult> CreateAsync(Guid userId, Guid sessionId, Guid conversationId, CreateConversationPollCommand command, CancellationToken ct)
    {
        ConversationPollDefinition definition;
        try
        {
            if (conversationId == Guid.Empty || command.ClientMessageId == Guid.Empty || command.TopicId == Guid.Empty || !command.IsAnonymous.HasValue) throw new ArgumentException("انتخاب صریح حریم خصوصی الزامی است.");
            definition = ConversationPollDefinition.Create(command.Question, command.Options, command.AllowsMultipleAnswers,
                command.IsAnonymous.Value, command.IsQuiz, command.CorrectOptionNumber, command.Explanation);
        }
        catch (ArgumentException ex) { throw Invalid(ex); }
        var context = await ResolveAsync(userId, sessionId, conversationId, ct);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { definition.Question, definition.Options,
            definition.AllowsMultipleAnswers, definition.IsAnonymous, definition.IsQuiz, definition.CorrectOptionNumber, definition.Explanation, command.TopicId, command.IsSilent }));
        var message = new SendMessageStoreCommand(command.ClientMessageId, MessageKind.Poll, definition.Question, [], null, [], TopicId: command.TopicId, IsSilent: command.IsSilent);
        var existing = await polls.PreparePollCreationAsync(userId, conversationId, message, hash, context, ct);
        if (existing is not null) return MessagingService.ToReceipt(existing);
        // هیچ پاسخ صحیح یا explanation در Message/Revision/Search/رویداد عمومی قرار نمی‌گیرد.
        var attempt = SHA256.HashData(command.ClientMessageId.ToByteArray().Concat(hash).ToArray());
        var safetyCommand = new SendMessageStoreCommand(new Guid(attempt.AsSpan(0, 16)), MessageKind.Text,
            definition.Question + "\n" + string.Join('\n', definition.Options) + "\n" + definition.Explanation,
            [], null, [], TopicId: command.TopicId, SafetyPurpose: MessageSafetyPurpose.PollMetadata);
        var gate = await MessageContentSafetyGate.ApplyAsync(messages, safety, clock, userId, conversationId, context,
            safetyCommand, MessagingService.HashSendPayload(conversationId, safetyCommand), ct);
        return MessagingService.ToReceipt(await polls.CreatePollAsync(userId, sessionId, conversationId, message, definition, hash, gate.Evaluated, context, ct));
    }
    public async Task<ConversationPollResult> GetAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, CancellationToken ct) =>
        await polls.GetPollAsync(userId, conversationId, messageId, await ResolveAsync(userId, sessionId, conversationId, ct), ct);
    public async Task<ConversationPollResult> VoteAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, VoteConversationPollCommand command, CancellationToken ct)
    {
        if (command.OptionNumbers is null || command.OptionNumbers.Count is < 1 or > ConversationPollDefinition.MaximumOptions ||
            command.OptionNumbers.Any(n => n is < 1 or > ConversationPollDefinition.MaximumOptions) || command.OptionNumbers.Distinct().Count() != command.OptionNumbers.Count) throw Invalid();
        var version = Version(command.ExpectedVoteVersion, true);
        return await polls.VotePollAsync(userId, sessionId, conversationId, messageId, command.OptionNumbers.Order().ToArray(), version, await ResolveAsync(userId, sessionId, conversationId, ct), ct);
    }
    public async Task<ConversationPollResult> CloseAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, CloseConversationPollCommand command, string correlationId, CancellationToken ct) =>
        await polls.ClosePollAsync(userId, sessionId, conversationId, messageId, Version(command.ExpectedVersion)!, await ResolveAsync(userId, sessionId, conversationId, ct), correlationId, ct);
    public async Task<ConversationPollVoterPageResult> GetVotersAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, int? optionNumber, int page, int pageSize, CancellationToken ct)
    {
        if (optionNumber is < 1 or > ConversationPollDefinition.MaximumOptions || page is < 1 or > 100000 || pageSize is < 1 or > 100) throw Invalid();
        return await polls.GetPollVotersAsync(userId, conversationId, messageId, optionNumber, (page - 1) * pageSize, pageSize, await ResolveAsync(userId, sessionId, conversationId, ct), ct);
    }
    private static byte[]? Version(string? encoded, bool optional = false)
    {
        if (encoded is null && optional) return null;
        try { if (encoded is not null) { var decoded = Convert.FromBase64String(encoded); if (decoded.Length == 8) return decoded; } }
        catch (FormatException) { }
        throw Invalid();
    }
    private static MessagingException Invalid(Exception? ex = null) => new(MessagingErrorCodes.PollInvalid, "درخواست نظرسنجی معتبر نیست.", ex);
    private Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct) =>
        operationContexts is null ? contexts.ResolveAsync(userId, sessionId, ct)
            : operationContexts.ConversationAsync(userId, sessionId, conversationId, ct);
}
