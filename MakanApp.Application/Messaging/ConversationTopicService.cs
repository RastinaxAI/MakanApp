using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class ConversationTopicService(IAccessContextResolver contexts, IConversationTopicStore topics,
    IMessagingStore messages, IContentSafetyProvider safety, TimeProvider clock)
{
    public async Task<ConversationTopicPageResult> GetAsync(Guid userId, Guid sessionId, Guid conversationId, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw Invalid();
        return await topics.GetTopicsAsync(userId, conversationId, await contexts.ResolveAsync(userId, sessionId, ct), (page - 1) * pageSize, pageSize, ct);
    }

    public async Task<ConversationTopicResult> CreateAsync(Guid userId, Guid sessionId, Guid conversationId,
        CreateConversationTopicCommand command, string correlationId, CancellationToken ct)
    {
        var title = Title(command.Title);
        if (command.ClientTopicId == Guid.Empty) throw Invalid();
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        await topics.PrepareTopicAsync(userId, conversationId, command.ClientTopicId, title, null, context, ct);
        var evaluated = await EvaluateAsync(userId, conversationId, command.ClientTopicId, title, null, context, ct);
        return await topics.CreateTopicAsync(userId, sessionId, conversationId, command.ClientTopicId, title, evaluated,
            context, correlationId, ct);
    }

    public async Task<ConversationTopicResult> UpdateAsync(Guid userId, Guid sessionId, Guid conversationId, Guid topicId,
        UpdateConversationTopicCommand command, string correlationId, CancellationToken ct)
    {
        var title = Title(command.Title); var version = Version(command.ExpectedVersion);
        if (topicId == Guid.Empty || !Enum.IsDefined(command.Status)) throw Invalid();
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        await topics.PrepareTopicAsync(userId, conversationId, topicId, title, version, context, ct);
        var evaluated = await EvaluateAsync(userId, conversationId, topicId, title, command.ExpectedVersion, context, ct);
        return await topics.UpdateTopicAsync(userId, sessionId, conversationId, topicId, command with { Title = title }, version,
            evaluated, context, correlationId, ct);
    }

    private async Task<bool> EvaluateAsync(Guid userId, Guid conversationId, Guid topicId, string title, string? version, AccessContext context, CancellationToken ct)
    {
        // فضای نامِ تلاش مستقل است؛ موضوع حتی با Guid برابر پیام، از receipt پیام عبور نمی‌کند.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"topic:{topicId:N}:{version}:{title}"));
        var command = new SendMessageStoreCommand(new Guid(hash.AsSpan(0, 16)), MessageKind.Text, title, [], null, [],
            TopicId: topicId, SafetyPurpose: MessageSafetyPurpose.TopicMetadata);
        var result = await MessageContentSafetyGate.ApplyAsync(messages, safety, clock, userId, conversationId, context,
            command, MessagingService.HashSendPayload(conversationId, command), ct);
        return result.Evaluated;
    }
    private static string Title(string title)
    {
        try { return Conversation.NormalizeTitle(title); } catch (ArgumentException) { throw Invalid(); }
    }
    private static byte[] Version(string value)
    {
        try { var result = Convert.FromBase64String(value); if (result.Length == 8) return result; } catch (Exception ex) when (ex is FormatException or ArgumentNullException) { }
        throw Invalid();
    }
    private static MessagingException Invalid() => new(MessagingErrorCodes.TopicInvalid, "درخواست موضوع معتبر نیست.");
}
