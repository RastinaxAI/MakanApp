using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ConversationChecklistTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    private static ConversationChecklistDefinition Definition(ConversationChecklistMode mode = ConversationChecklistMode.Personal) =>
        ConversationChecklistDefinition.Create("عنوان", mode, [new(Guid.NewGuid(), "کار اول")]);
    private static Message ChecklistMessage() => Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
        MessageKind.Checklist, "عنوان", 0, null, null, 4000, Now);
    private static ConversationChecklist Checklist(ConversationChecklistMode mode = ConversationChecklistMode.Personal) =>
        ConversationChecklist.Create(ChecklistMessage(), Definition(mode), new byte[32]);

    [Fact]
    public void DefinitionNormalizesAndCopiesImmutableItems()
    {
        var id = Guid.NewGuid(); var items = new[] { new ConversationChecklistItemDefinition(id, " کار اول ") };
        var definition = ConversationChecklistDefinition.Create(" عنوان ", ConversationChecklistMode.Personal, items);
        items[0] = new(Guid.NewGuid(), "دیگر");
        Assert.Equal("عنوان", definition.Title); Assert.Equal(new(id, "کار اول"), Assert.Single(definition.Items));
        Assert.Throws<NotSupportedException>(() => ((IList<ConversationChecklistItemDefinition>)definition.Items)[0] = items[0]);
    }

    [Theory]
    [InlineData(0)] [InlineData(21)]
    public void DefinitionRejectsEmptyAndUnboundedLists(int count) => Assert.Throws<ArgumentException>(() =>
        ConversationChecklistDefinition.Create("عنوان", ConversationChecklistMode.Personal,
            Enumerable.Range(0, count).Select(i => new ConversationChecklistItemDefinition(Guid.NewGuid(), i.ToString())).ToArray()));

    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(-1)]
    public void DefinitionRejectsUnknownMode(int mode) => Assert.Throws<ArgumentException>(() =>
        ConversationChecklistDefinition.Create("عنوان", (ConversationChecklistMode)mode, [new(Guid.NewGuid(), "کار")]));

    [Fact]
    public void DefinitionRequiresStableUniqueIdsAndBoundedPlainText()
    {
        var id = Guid.NewGuid();
        foreach (var items in new ConversationChecklistItemDefinition[][] { [new(Guid.Empty, "کار")], [new(id, "اول"), new(id, "دوم")],
            [new(id, " ")], [new(id, "کار\nپنهان")], [new(id, new string('x', 151))] })
            Assert.Throws<ArgumentException>(() => ConversationChecklistDefinition.Create("عنوان", ConversationChecklistMode.Personal, items));
        Assert.Throws<ArgumentException>(() => ConversationChecklistDefinition.Create(new string('x', 301), ConversationChecklistMode.Personal, [new(id, "کار")]));
    }

    [Fact]
    public void ChecklistRequiresMatchingRealMessageAndCreationHash()
    {
        var message = ChecklistMessage(); var definition = Definition();
        Assert.Throws<ArgumentException>(() => ConversationChecklist.Create(message, definition, new byte[31]));
        Assert.Throws<ArgumentException>(() => ConversationChecklist.Create(message,
            ConversationChecklistDefinition.Create("دیگر", definition.Mode, definition.Items), new byte[32]));
        message.Delete(Guid.NewGuid(), Now); Assert.Throws<ArgumentException>(() => ConversationChecklist.Create(message, definition, new byte[32]));
    }

    [Fact]
    public void PublishedOwnershipModeCannotRewriteHistoricalMeaning()
    {
        var checklist = Checklist();
        Assert.Throws<InvalidOperationException>(() => checklist.AdvanceDefinition(ConversationChecklistMode.Collaborative, Now));
        Assert.Equal(ConversationChecklistMode.Personal, checklist.Mode); Assert.Equal(1, checklist.DefinitionRevision);
    }

    [Fact]
    public void DefinitionUsesServerUtcAndAdvancesOnlyExplicitly()
    {
        var checklist = Checklist();
        Assert.Throws<ArgumentException>(() => checklist.AdvanceDefinition(checklist.Mode, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => checklist.AdvanceDefinition(checklist.Mode, Now.AddSeconds(-1)));
        checklist.AdvanceDefinition(checklist.Mode, Now.AddSeconds(1)); Assert.Equal(2, checklist.DefinitionRevision);
    }

    [Fact]
    public void PersonalStateIsUserOwnedAndEquivalentMutationDoesNotAdvanceRevision()
    {
        var definition = Definition(); var checklist = ConversationChecklist.Create(ChecklistMessage(), definition, new byte[32]);
        var item = ConversationChecklistItem.Create(checklist, definition.Items[0], 1); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var first = ConversationChecklistPersonalState.Create(item, a, true); var second = ConversationChecklistPersonalState.Create(item, b, false);
        Assert.False(first.SetChecked(true)); Assert.Equal(1, first.Revision); Assert.False(second.IsChecked);
        Assert.True(first.SetChecked(false)); Assert.Equal(2, first.Revision); Assert.Equal(1, checklist.DefinitionRevision);
        Assert.Equal(a, first.UserId); Assert.Equal(b, second.UserId); Assert.False(item.IsChecked);
        Assert.Throws<InvalidOperationException>(() => item.SetSharedChecked(true));
    }

    [Fact]
    public void CollaborativeItemHasOneIdempotentSharedStateNotPersonalRows()
    {
        var definition = Definition(ConversationChecklistMode.Collaborative); var checklist = ConversationChecklist.Create(ChecklistMessage(), definition, new byte[32]);
        var item = ConversationChecklistItem.Create(checklist, definition.Items[0], 1);
        Assert.True(item.SetSharedChecked(true)); Assert.False(item.SetSharedChecked(true)); Assert.Equal(2, item.StateRevision);
        Assert.True(item.SetSharedChecked(false)); Assert.False(item.SetSharedChecked(false)); Assert.Equal(3, item.StateRevision);
        Assert.Equal(1, checklist.DefinitionRevision);
        Assert.Throws<ArgumentException>(() => ConversationChecklistPersonalState.Create(item, Guid.NewGuid(), true));
    }

    [Fact]
    public void RetirementPreservesStableTextAndStateButRefusesFutureInteraction()
    {
        var definition = Definition(ConversationChecklistMode.Collaborative); var checklist = ConversationChecklist.Create(ChecklistMessage(), definition, new byte[32]);
        var item = ConversationChecklistItem.Create(checklist, definition.Items[0], 1); item.SetSharedChecked(true);
        checklist.AdvanceDefinition(checklist.Mode, Now); item.Retire(checklist.DefinitionRevision);
        Assert.False(item.IsActive); Assert.True(item.IsChecked); Assert.Equal(definition.Items[0].Text, item.Text);
        Assert.Equal(definition.Items[0].ItemId, item.Id); Assert.Throws<InvalidOperationException>(() => item.SetSharedChecked(false));
        Assert.Throws<ArgumentException>(() => item.Move(1));
    }

    [Fact]
    public void GenericEditAndAttachmentCannotBypassChecklistDefinition()
    {
        var message = ChecklistMessage(); Assert.Throws<InvalidOperationException>(() => message.EditText("دورزدن", 4000, Now));
        Assert.Throws<ArgumentException>(() => Message.ValidateContent(MessageKind.Checklist, "عنوان", 1, 4000));
        var revision = message.EditChecklistTitle("عنوان جدید", Now); Assert.Equal(2, revision); Assert.Equal("عنوان جدید", message.Text);
        Assert.Equal("عنوان جدید", message.SearchText);
    }
}
