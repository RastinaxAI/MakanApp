namespace MakanApp.Domain.Messaging;

// پاسخ صحیح از projection عمومیِ گزینه‌ها و پیام جداست؛ تنها پس از پاسخ خود کاربر خوانده می‌شود.
public sealed class ConversationQuizAnswer
{
    private ConversationQuizAnswer() { }
    public Guid PollId { get; private set; }
    public bool IsQuiz { get; private set; }
    public int CorrectOptionNumber { get; private set; }
    public string? Explanation { get; private set; }
    public static ConversationQuizAnswer Create(Guid pollId, ConversationPollDefinition definition)
    {
        if (pollId == Guid.Empty || !definition.IsQuiz || !definition.CorrectOptionNumber.HasValue) throw new ArgumentException("کلید پاسخ فقط متعلق به Quiz معتبر است.");
        return new() { PollId = pollId, IsQuiz = true, CorrectOptionNumber = definition.CorrectOptionNumber.Value, Explanation = definition.Explanation };
    }
}
