using MakanApp.Application.Organization;

namespace MakanApp.Application.Messaging;

public sealed class ScheduledMessageService(IAccessContextResolver contexts, IScheduledMessageStore schedules, MessagingOptions options, MessagingOperationContextResolver? operationContexts = null)
{
    public async Task<ScheduledMessageResult> ScheduleAsync(Guid userId, Guid sessionId, Guid conversationId, ScheduleMessageCommand command,
        string correlationId, CancellationToken ct)
    {
        if (command.ScheduledAtUtc.Kind != DateTimeKind.Utc || command.Message is null) throw Invalid();
        var message = MessageCommandValidation.Validate(command.Message, options);
        return await schedules.ScheduleAsync(userId, sessionId, conversationId, message, MessagingService.HashSendPayload(conversationId, message),
            command.ScheduledAtUtc, await ResolveAsync(userId, sessionId, conversationId, ct), correlationId, ct);
    }
    public async Task<ScheduledMessagePageResult> GetAsync(Guid userId, Guid sessionId, Guid conversationId, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw Invalid();
        return await schedules.GetScheduledMessagesAsync(userId, conversationId, await ResolveAsync(userId, sessionId, conversationId, ct), (page - 1) * pageSize, pageSize, ct);
    }
    public async Task<ScheduledMessageResult> CancelAsync(Guid userId, Guid sessionId, Guid conversationId, Guid id, CancelScheduledMessageCommand command,
        string correlationId, CancellationToken ct)
    {
        byte[] version;
        try { version = Convert.FromBase64String(command.ExpectedVersion); if (version.Length != 8) throw Invalid(); }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException) { throw Invalid(); }
        return await schedules.CancelScheduledMessageAsync(userId, sessionId, conversationId, id, version,
            await ResolveAsync(userId, sessionId, conversationId, ct), correlationId, ct);
    }
    private static MessagingException Invalid() => new(MessagingErrorCodes.ScheduledInvalid, "درخواست زمان‌بندی معتبر نیست.");
    private Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct) =>
        operationContexts is null ? contexts.ResolveAsync(userId, sessionId, ct)
            : operationContexts.ConversationAsync(userId, sessionId, conversationId, ct);
}
