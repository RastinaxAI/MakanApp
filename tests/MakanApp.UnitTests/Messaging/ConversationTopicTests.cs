using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ConversationTopicTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static Conversation Group(ConversationType type = ConversationType.Group) => Conversation.CreateUserManaged(type,
        ConversationScope.Personal, null, "گروه", null, Guid.NewGuid(), Guid.NewGuid(), new byte[32], Now);
    [Fact]
    public void TopicLifecyclePreservesIdentityAndSupportsExplicitReopen()
    {
        var topic = ConversationTopic.Create(Guid.NewGuid(), Group(), Guid.NewGuid(), "  موضوع  ", Now);
        Assert.Equal("موضوع", topic.Title); Assert.Equal(ConversationTopicStatus.Open, topic.Status);
        topic.Update("نام تازه", ConversationTopicStatus.Closed, Now.AddMinutes(1));
        topic.Update("نام تازه", ConversationTopicStatus.Open, Now.AddMinutes(2));
        Assert.Equal(3, topic.Revision); Assert.Equal(ConversationTopicStatus.Open, topic.Status);
    }
    [Fact]
    public void TopicRejectsNonGroupEmptyTitleInvalidStatusAndNonUtcTime()
    {
        Assert.Throws<ArgumentException>(() => ConversationTopic.Create(Guid.NewGuid(), Group(ConversationType.Channel), Guid.NewGuid(), "موضوع", Now));
        Assert.Throws<ArgumentException>(() => ConversationTopic.Create(Guid.NewGuid(), Group(), Guid.NewGuid(), " ", Now));
        var topic = ConversationTopic.Create(Guid.NewGuid(), Group(), Guid.NewGuid(), "موضوع", Now);
        Assert.Throws<ArgumentException>(() => topic.Update("موضوع", (ConversationTopicStatus)99, Now));
        Assert.Throws<ArgumentException>(() => topic.Update("موضوع", ConversationTopicStatus.Open, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }
    [Fact]
    public void MessageTopicIsExplicitAndCannotBeEmpty()
    {
        var id = Guid.NewGuid();
        var message = Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, MessageKind.Text, "متن", 0, null, null, 4000, Now, id);
        Assert.Equal(id, message.TopicId);
        Assert.Throws<ArgumentException>(() => Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, MessageKind.Text, "متن", 0, null, null, 4000, Now, Guid.Empty));
    }
    [Fact]
    public void TopicPermissionIsIndependentAndNotGrantedToLegacyAdmin()
    {
        var admin = ConversationParticipant.CreateActive(Guid.NewGuid(), Guid.NewGuid(), Now, ConversationParticipantRole.Admin, 1, ConversationAdminPermission.LegacyAdminDefault);
        Assert.False(admin.CanManage(ConversationAdminPermission.ManageTopics));
        admin.UpdateAdminPermissions(ConversationAdminPermission.ManageTopics);
        Assert.True(admin.CanManage(ConversationAdminPermission.ManageTopics));
        Assert.False(admin.CanManage(ConversationAdminPermission.ManageAdmins));
    }
}
