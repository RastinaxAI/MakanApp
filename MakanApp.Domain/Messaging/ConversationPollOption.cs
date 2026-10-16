namespace MakanApp.Domain.Messaging;

public sealed class ConversationPollOption
{
    private ConversationPollOption() { }
    public Guid PollId { get; private set; }
    public int Number { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public static ConversationPollOption Create(Guid pollId, int number, ConversationPollDefinition definition)
    {
        if (pollId == Guid.Empty || number < 1 || number > definition.Options.Count) throw new ArgumentException("گزینه متعلق به تعریف معتبر نیست.");
        return new() { PollId = pollId, Number = number, Text = definition.Options[number - 1] };
    }
}
