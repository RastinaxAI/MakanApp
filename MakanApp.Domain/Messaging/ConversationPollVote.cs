namespace MakanApp.Domain.Messaging;

public sealed class ConversationPollVote
{
    private ConversationPollVote() { }
    public Guid PollId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsQuiz { get; private set; }
    public long Revision { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static ConversationPollVote Create(ConversationPoll poll, Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty || poll.Status != ConversationPollStatus.Open || nowUtc.Kind != DateTimeKind.Utc || nowUtc < poll.CreatedAtUtc) throw new ArgumentException("ثبت رأی معتبر نیست.");
        return new() { PollId = poll.Id, UserId = userId, IsQuiz = poll.IsQuiz, Revision = 1, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc };
    }
    public void Change(DateTime nowUtc)
    {
        if (IsQuiz) throw new InvalidOperationException("پاسخ نهایی Quiz قابل تغییر نیست.");
        if (nowUtc.Kind != DateTimeKind.Utc || nowUtc < CreatedAtUtc || Revision == long.MaxValue) throw new ArgumentException("زمان یا نسخهٔ رأی معتبر نیست.");
        Revision++; UpdatedAtUtc = nowUtc;
    }
}
