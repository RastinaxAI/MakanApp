using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record ScheduleMessageCommand(DateTime ScheduledAtUtc, SendMessageCommand Message);
public sealed record CancelScheduledMessageCommand(string ExpectedVersion);
public sealed record ScheduledMessageResult(Guid Id, Guid ConversationId, Guid ClientMessageId, ScheduledMessageStatus Status,
    DateTime ScheduledAtUtc, DateTime CreatedAtUtc, DateTime? CompletedAtUtc, Guid? DispatchedMessageId, string? LastFailureCode,
    ScheduledMessageContent Content, string Version, bool AlreadyExisted = false);
public sealed record ScheduledMessagePageResult(IReadOnlyCollection<ScheduledMessageResult> Items, bool HasMore);
public sealed record ScheduledMessageClaim(Guid Id, Guid Token);
public sealed record ScheduledMessageWork(Guid Id, Guid ConversationId, Guid UserId, ScheduledMessageAuthority Authority, SendMessageStoreCommand Command);
public interface IScheduledMessageStore
{
    Task<ScheduledMessageResult> ScheduleAsync(Guid userId, Guid sessionId, Guid conversationId, SendMessageStoreCommand command,
        byte[] payloadHash, DateTime scheduledAtUtc, AccessContext context, string correlationId, CancellationToken ct);
    Task<ScheduledMessagePageResult> GetScheduledMessagesAsync(Guid userId, Guid conversationId, AccessContext context, int skip, int take, CancellationToken ct);
    Task<ScheduledMessageResult> CancelScheduledMessageAsync(Guid userId, Guid sessionId, Guid conversationId, Guid id, byte[] expectedVersion,
        AccessContext context, string correlationId, CancellationToken ct);
    Task<ScheduledMessageClaim?> ClaimNextScheduledMessageAsync(CancellationToken ct);
    Task<ScheduledMessageWork?> GetScheduledMessageWorkAsync(ScheduledMessageClaim claim, CancellationToken ct);
    Task FailScheduledMessageAsync(ScheduledMessageClaim claim, string code, bool retryable, CancellationToken ct);
}
