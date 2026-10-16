namespace MakanApp.Domain.Messaging;

public sealed class ConversationPollSelection
{
    private ConversationPollSelection() { }
    public Guid PollId { get; private set; }
    public Guid UserId { get; private set; }
    public int OptionNumber { get; private set; }
    public static ConversationPollSelection Create(ConversationPollVote vote, int optionNumber)
    {
        if (optionNumber is < 1 or > ConversationPollDefinition.MaximumOptions) throw new ArgumentException("شمارهٔ گزینه معتبر نیست.");
        return new() { PollId = vote.PollId, UserId = vote.UserId, OptionNumber = optionNumber };
    }
}
