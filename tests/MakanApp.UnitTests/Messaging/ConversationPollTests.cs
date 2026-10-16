using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ConversationPollTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    private static ConversationPollDefinition Definition(bool quiz = false, bool multiple = false) => ConversationPollDefinition.Create("پرسش", ["اول", "دوم"], multiple, true, quiz, quiz ? 2 : null, quiz ? "توضیح" : null);
    private static Message PollMessage(string text = "پرسش", int attachments = 0) => Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, MessageKind.Poll, text, attachments, null, null, Message.StorageMaximumTextLength, Now);
    private static ConversationPoll Poll(bool quiz = false, bool multiple = false) => ConversationPoll.Create(PollMessage(), Definition(quiz, multiple), new byte[32]);
    [Fact]
    public void DefinitionUsesImmutableNormalizedSnapshot()
    {
        var options = new[] { " اول ", " دوم " }; var definition = ConversationPollDefinition.Create(" پرسش ", options, false, true, false, null, null);
        options[0] = "بازنویسی"; Assert.Equal("پرسش", definition.Question); Assert.Equal("اول", definition.Options[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)definition.Options)[0] = "تغییر");
    }
    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void DefinitionRejectsOptionCountOutsideBounds(int count) => Assert.Throws<ArgumentException>(() =>
        ConversationPollDefinition.Create("پرسش", Enumerable.Range(1, count).Select(i => i.ToString()).ToArray(), false, true, false, null, null));
    [Fact]
    public void DefinitionRejectsBlankDuplicateControlAndOversizedOptions()
    {
        foreach (var options in new[] { new[] { " ", "x" }, new[] { "x", " X " }, new[] { "x\n", "y\0" }, new[] { "x", new string('y', 151) } })
            Assert.Throws<ArgumentException>(() => ConversationPollDefinition.Create("پرسش", options, false, true, false, null, null));
        Assert.Throws<ArgumentException>(() => ConversationPollDefinition.Create(new string('q', 301), ["x", "y"], false, true, false, null, null));
    }
    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    public void QuizRequiresOneValidCorrectOptionAndSingleChoice(int? correct, bool multiple) => Assert.Throws<ArgumentException>(() =>
        ConversationPollDefinition.Create("پرسش", ["اول", "دوم"], multiple, false, true, correct, null));
    [Fact]
    public void OrdinaryPollCannotCarryAnswerKeyOrExplanation()
    {
        Assert.Throws<ArgumentException>(() => ConversationPollDefinition.Create("پرسش", ["اول", "دوم"], false, true, false, 1, null));
        Assert.Throws<ArgumentException>(() => ConversationPollDefinition.Create("پرسش", ["اول", "دوم"], false, true, false, null, "توضیح"));
    }
    [Fact]
    public void SingleChoiceAndMultiChoiceValidatePersistableOptionNumbers()
    {
        var single = Poll(); Assert.Equal(new[] { 2 }, single.ValidateSelection([2]));
        foreach (var invalid in new[] { Array.Empty<int>(), new[] { 0 }, new[] { 3 }, new[] { 1, 2 }, new[] { 1, 1 } }) Assert.Throws<ArgumentException>(() => single.ValidateSelection(invalid));
        Assert.Equal(new[] { 1, 2 }, Poll(multiple: true).ValidateSelection([2, 1]));
    }
    [Fact]
    public void CloseIsFinalIdempotentAndStopsVoteChanges()
    {
        var poll = Poll(); poll.RecordVoteChange(Now.AddSeconds(1)); var revision = poll.Revision; var actor = Guid.NewGuid();
        Assert.True(poll.Close(actor, Now.AddSeconds(2))); Assert.False(poll.Close(actor, Now.AddSeconds(3)));
        Assert.Equal(ConversationPollStatus.Closed, poll.Status); Assert.Equal(revision + 1, poll.Revision); Assert.Equal(Now.AddSeconds(2), poll.ClosedAtUtc);
        Assert.Throws<InvalidOperationException>(() => poll.RecordVoteChange(Now.AddSeconds(4)));
    }
    [Fact]
    public void QuizVoteIsFinalWhileOrdinaryVoteHasRevision()
    {
        var final = ConversationPollVote.Create(Poll(quiz: true), Guid.NewGuid(), Now); Assert.Equal(1, final.Revision);
        Assert.Throws<InvalidOperationException>(() => final.Change(Now.AddSeconds(1))); Assert.Equal(1, final.Revision);
        var mutable = ConversationPollVote.Create(Poll(), Guid.NewGuid(), Now); mutable.Change(Now.AddSeconds(1)); Assert.Equal(2, mutable.Revision);
    }
    [Fact]
    public void PublishedPollQuestionCannotBeEditedOrUsedAsMedia()
    {
        var message = PollMessage(); Assert.Throws<InvalidOperationException>(() => message.EditText("تغییر", 4000, Now.AddSeconds(1)));
        Assert.Equal("پرسش", message.Text); Assert.Equal(1, message.CurrentRevisionNumber);
        Assert.Throws<ArgumentException>(() => PollMessage(attachments: 1));
    }
    [Fact]
    public void AnswerKeyBelongsOnlyToQuizAndValidOption()
    {
        Assert.Throws<ArgumentException>(() => ConversationQuizAnswer.Create(Guid.NewGuid(), Definition()));
        var key = ConversationQuizAnswer.Create(Guid.NewGuid(), Definition(quiz: true)); Assert.True(key.IsQuiz); Assert.Equal(2, key.CorrectOptionNumber); Assert.Equal("توضیح", key.Explanation);
    }
    [Fact]
    public void PollMustMatchRealMessageAndFingerprintShape()
    {
        Assert.Throws<ArgumentException>(() => ConversationPoll.Create(PollMessage("متفاوت"), Definition(), new byte[32]));
        Assert.Throws<ArgumentException>(() => ConversationPoll.Create(PollMessage(), Definition(), new byte[31]));
        Assert.Throws<ArgumentException>(() => ConversationPollOption.Create(Guid.NewGuid(), 3, Definition()));
    }
    [Fact]
    public void VoteAndCloseUseServerUtcAndNonemptyActor()
    {
        var poll = Poll(); Assert.Throws<ArgumentException>(() => poll.Close(Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => poll.RecordVoteChange(DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => ConversationPollVote.Create(poll, Guid.NewGuid(), Now.AddTicks(-1)));
    }
}
