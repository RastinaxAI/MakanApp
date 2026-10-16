using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record CreateConversationPollCommand(Guid ClientMessageId, string Question, IReadOnlyCollection<string> Options,
    bool AllowsMultipleAnswers, bool? IsAnonymous, bool IsQuiz = false, int? CorrectOptionNumber = null,
    string? Explanation = null, Guid? TopicId = null, bool IsSilent = false);
public sealed record VoteConversationPollCommand(IReadOnlyCollection<int> OptionNumbers, string? ExpectedVoteVersion = null);
public sealed record CloseConversationPollCommand(string ExpectedVersion);
public sealed record ConversationPollOptionResult(int Number, string Text, long VoteCount, bool IsSelected);
public sealed record ConversationPollResult(Guid MessageId, Guid ConversationId, string Question, bool AllowsMultipleAnswers,
    bool IsAnonymous, bool IsQuiz, ConversationPollStatus Status, DateTime CreatedAtUtc, DateTime? ClosedAtUtc,
    IReadOnlyCollection<ConversationPollOptionResult> Options, long TotalVoters, string? OwnVoteVersion,
    int? CorrectOptionNumber, string? Explanation, string Version);
public sealed record ConversationPollVoterResult(SafeMessagingIdentityResult User, IReadOnlyCollection<int> OptionNumbers, DateTime UpdatedAtUtc);
public sealed record ConversationPollVoterPageResult(IReadOnlyCollection<ConversationPollVoterResult> Items, bool HasMore);
public interface IConversationPollStore
{
    Task<Message?> PreparePollCreationAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command, byte[] definitionHash, AccessContext context, CancellationToken ct);
    Task<Message> CreatePollAsync(Guid userId, Guid sessionId, Guid conversationId, SendMessageStoreCommand command, ConversationPollDefinition definition,
        byte[] definitionHash, bool safetyEvaluated, AccessContext context, CancellationToken ct);
    Task<ConversationPollResult> GetPollAsync(Guid userId, Guid conversationId, Guid messageId, AccessContext context, CancellationToken ct);
    Task<ConversationPollResult> VotePollAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, int[] numbers,
        byte[]? expectedVoteVersion, AccessContext context, CancellationToken ct);
    Task<ConversationPollResult> ClosePollAsync(Guid userId, Guid sessionId, Guid conversationId, Guid messageId, byte[] expectedVersion,
        AccessContext context, string correlationId, CancellationToken ct);
    Task<ConversationPollVoterPageResult> GetPollVotersAsync(Guid userId, Guid conversationId, Guid messageId, int? optionNumber,
        int skip, int take, AccessContext context, CancellationToken ct);
}
