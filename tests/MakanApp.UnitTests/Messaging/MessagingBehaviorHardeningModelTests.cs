using System.Security.Cryptography;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingBehaviorHardeningModelTests
{
    private static readonly DateTime SentAtUtc = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(59, 59, true)]
    [InlineData(60, 0, true)]
    [InlineData(60, 1, false)]
    public void SenderMutationWindowUsesOriginalSentTime(
        int elapsedMinutes,
        int elapsedSeconds,
        bool expected)
    {
        var message = CreateMessage();
        var nowUtc = SentAtUtc.AddMinutes(elapsedMinutes).AddSeconds(elapsedSeconds);

        Assert.Equal(expected, message.IsWithinSenderMutationWindow(nowUtc));
    }

    [Fact]
    public void GroupSettingsAreExplicitAndValidated()
    {
        var conversation = CreateGroup(
            GroupHistoryPolicy.FromJoin,
            GroupMemberCapability.SendMessages);

        Assert.Equal(ConversationVisibility.Public, conversation.Visibility);
        Assert.Equal(GroupHistoryPolicy.FromJoin, conversation.GroupHistoryPolicy);
        Assert.Equal(GroupMemberCapability.SendMessages, conversation.MemberCapabilities);
    }

    [Fact]
    public void ChannelRejectsGroupOnlyHistoryAndMemberCapabilities()
    {
        Assert.Throws<ArgumentException>(() => Conversation.CreateUserManaged(
            ConversationType.Channel,
            ConversationScope.Personal,
            null,
            "کانال",
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            RandomNumberGenerator.GetBytes(Conversation.CreationPayloadHashLength),
            ConversationVisibility.Private,
            GroupHistoryPolicy.FromJoin,
            GroupMemberCapability.SendMessages,
            SentAtUtc));
    }

    [Fact]
    public void ParticipantHistoryBoundaryAndAdminPermissionsAreStableSnapshots()
    {
        var participant = ConversationParticipant.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SentAtUtc,
            ConversationParticipantRole.Admin,
            42,
            ConversationAdminPermission.DeleteMessages);

        Assert.Equal(42, participant.HistoryVisibleFromSequence);
        Assert.Equal(ConversationAdminPermission.DeleteMessages, participant.AdminPermissions);
        participant.UpdateAdminPermissions(ConversationAdminPermission.ManageMembers);
        Assert.Equal(ConversationAdminPermission.ManageMembers, participant.AdminPermissions);
    }

    [Fact]
    public void NonAdminCannotReceiveAdminPermissions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConversationParticipant.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SentAtUtc,
            ConversationParticipantRole.Member,
            1,
            ConversationAdminPermission.DeleteMessages));
    }

    [Fact]
    public void PersonalStateKeepsArchivePinMuteAndManualUnreadIndependent()
    {
        var state = ConversationUserState.Create(Guid.NewGuid(), Guid.NewGuid(), SentAtUtc);

        state.Archive(SentAtUtc.AddMinutes(1));
        state.Pin(SentAtUtc.AddMinutes(2));
        state.MuteUntil(SentAtUtc.AddHours(2), SentAtUtc.AddMinutes(3));
        state.MarkUnread(SentAtUtc.AddMinutes(4));

        Assert.True(state.IsArchived);
        Assert.True(state.IsPinned);
        Assert.True(state.IsMutedAt(SentAtUtc.AddHours(1)));
        Assert.False(state.IsMutedAt(SentAtUtc.AddHours(3)));
        Assert.True(state.IsManuallyUnread);
    }

    private static Message CreateMessage() => Message.CreateText(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        1,
        "متن",
        Message.StorageMaximumTextLength,
        SentAtUtc);

    private static Conversation CreateGroup(
        GroupHistoryPolicy historyPolicy,
        GroupMemberCapability capabilities) =>
        Conversation.CreateUserManaged(
            ConversationType.Group,
            ConversationScope.Personal,
            null,
            "گروه",
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            RandomNumberGenerator.GetBytes(Conversation.CreationPayloadHashLength),
            ConversationVisibility.Public,
            historyPolicy,
            capabilities,
            SentAtUtc);
}
