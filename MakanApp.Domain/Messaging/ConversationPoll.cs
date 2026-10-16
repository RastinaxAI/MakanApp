namespace MakanApp.Domain.Messaging;

public enum ConversationPollStatus { Open = 1, Closed = 2 }

public sealed class ConversationPoll
{
    private ConversationPoll() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid CreatorUserId { get; private set; }
    public bool IsQuiz { get; private set; }
    public bool IsAnonymous { get; private set; }
    public bool AllowsMultipleAnswers { get; private set; }
    public int OptionCount { get; private set; }
    public byte[] DefinitionHash { get; private set; } = [];
    public ConversationPollStatus Status { get; private set; }
    public long Revision { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static ConversationPoll Create(Message message, ConversationPollDefinition definition, byte[] definitionHash)
    {
        if (message.Kind != MessageKind.Poll || message.IsDeleted || message.ForwardedFromMessageId.HasValue || message.Text != definition.Question || definitionHash.Length != 32)
            throw new ArgumentException("تعریف باید به پیام واقعی همین نظرسنجی متصل باشد.");
        return new() { Id = message.Id, ConversationId = message.ConversationId, CreatorUserId = message.SenderUserId,
            IsQuiz = definition.IsQuiz, IsAnonymous = definition.IsAnonymous, AllowsMultipleAnswers = definition.AllowsMultipleAnswers,
            OptionCount = definition.Options.Count, DefinitionHash = definitionHash.ToArray(), Status = ConversationPollStatus.Open,
            Revision = 1, CreatedAtUtc = message.SentAtUtc, UpdatedAtUtc = message.SentAtUtc };
    }
    public int[] ValidateSelection(IReadOnlyCollection<int> numbers)
    {
        if (numbers is null || numbers.Count is < 1 or > ConversationPollDefinition.MaximumOptions || numbers.Any(n => n < 1 || n > OptionCount) ||
            numbers.Distinct().Count() != numbers.Count || !AllowsMultipleAnswers && numbers.Count != 1) throw new ArgumentException("انتخاب گزینه‌ها معتبر نیست.");
        return numbers.Order().ToArray();
    }
    public void RecordVoteChange(DateTime nowUtc)
    {
        if (Status != ConversationPollStatus.Open) throw new InvalidOperationException("نظرسنجی بسته است.");
        Advance(nowUtc);
    }
    public bool Close(Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("بستن به actor معتبر نیاز دارد.");
        if (Status == ConversationPollStatus.Closed) return false;
        Advance(nowUtc); Status = ConversationPollStatus.Closed; ClosedAtUtc = nowUtc; ClosedByUserId = userId; return true;
    }
    private void Advance(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || nowUtc < CreatedAtUtc || Revision == long.MaxValue) throw new ArgumentException("زمان یا نسخه معتبر نیست.");
        Revision++; UpdatedAtUtc = nowUtc;
    }
}
