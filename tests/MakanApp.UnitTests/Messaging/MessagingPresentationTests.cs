using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingPresentationTests
{
    [Fact]
    public void FolderTypeMaskAndRevisionRemainExplicit()
    {
        var folder = ChatFolder.Create(Guid.NewGuid(), Guid.NewGuid());
        folder.Update("  درس‌ها  ", 2, FolderConversationTypes.Group | FolderConversationTypes.Channel, FolderConversationTypes.Channel, true);
        Assert.Equal("درس‌ها", folder.Name); Assert.Equal(1, folder.Revision);
        Assert.Equal(new[] { ConversationType.Group, ConversationType.Channel }, folder.IncludedConversationTypes());
        Assert.Equal(new[] { ConversationType.Channel }, folder.ExcludedConversationTypes());
        folder.Update(folder.Name, 1, FolderConversationTypes.All, FolderConversationTypes.None, false); Assert.Equal(2, folder.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("name\ninvalid")]
    public void EmptyOrControlFolderNamesAreRejected(string name) => Assert.Throws<ArgumentException>(() =>
        ChatFolder.Create(Guid.NewGuid(), Guid.NewGuid()).Update(name, 0, FolderConversationTypes.All, FolderConversationTypes.None, false));

    [Fact]
    public void UnknownTypeBitsAndInvalidOverrideAreRejected()
    {
        var folder = ChatFolder.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => folder.Update("name", 0, (FolderConversationTypes)16, FolderConversationTypes.None, false));
        Assert.Throws<ArgumentException>(() => FolderConversationOverride.Create(folder.Id, Guid.NewGuid(), false, 1));
        Assert.Throws<ArgumentException>(() => FolderConversationOverride.Create(folder.Id, Guid.NewGuid(), true, 200));
    }

    [Fact]
    public void SavedLabelNormalizationIsStableAndRejectsInvalidInput()
    {
        Assert.Equal("LESSON", SavedMessageLabel.Normalize(" lesson "));
        Assert.Equal(SavedMessageLabel.Normalize("Ａ"), SavedMessageLabel.Normalize("a"));
        Assert.Throws<ArgumentException>(() => SavedMessageLabel.Normalize(new string('x', 33)));
        Assert.Throws<ArgumentException>(() => SavedMessageLabel.Normalize("\n"));
        var organization = SavedMessageOrganization.Create(Guid.NewGuid(), Guid.NewGuid()); organization.Touch(); organization.Touch();
        Assert.Equal(2, organization.Revision);
    }
}
