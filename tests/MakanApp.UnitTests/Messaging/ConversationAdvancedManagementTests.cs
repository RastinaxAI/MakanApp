using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ConversationAdvancedManagementTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void LinkEnforcesExpiryRevocationAndMaximumUses()
    {
        var link = ConversationInviteLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), Now.AddHours(1), 1, false, Now);
        link.Consume(Now); Assert.Equal(1, link.UsedCount); Assert.Throws<InvalidOperationException>(() => link.Consume(Now));
        link.Revoke(Now); Assert.False(link.IsOpen(Now));
        Assert.Throws<ArgumentException>(() => ConversationInviteLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), Now, 1, false, Now));
    }
    [Fact]
    public void LinkRejectsInvalidLimitsHashesAndCreatorContext()
    {
        Assert.Throws<ArgumentException>(() => ConversationInviteLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invalid", Now.AddHours(1), 1, false, Now));
        Assert.Throws<ArgumentException>(() => ConversationInviteLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), Now.AddHours(1), 0, false, Now));
        Assert.Throws<ArgumentException>(() => ConversationInviteLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), Now.AddHours(1), 1, false, Now, Guid.NewGuid()));
    }
    [Fact]
    public void JoinRequestTerminalStateIsIdempotentButCannotBeRewritten()
    {
        var request = ConversationJoinRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(1), Now);
        request.Complete(ConversationJoinRequestStatus.Approved, Now); request.Complete(ConversationJoinRequestStatus.Approved, Now.AddSeconds(1));
        Assert.Equal(Now, request.CompletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => request.Complete(ConversationJoinRequestStatus.Rejected, Now));
    }
    [Fact]
    public void GranularCapabilitiesDoNotEscalateAndOwnerCannotBeRestricted()
    {
        var conversationId = Guid.NewGuid();
        var owner = ConversationParticipant.CreateActive(conversationId, Guid.NewGuid(), Now, ConversationParticipantRole.Owner);
        var admin = ConversationParticipant.CreateActive(conversationId, Guid.NewGuid(), Now, ConversationParticipantRole.Admin, 1,
            ConversationAdminPermission.ManageInvites | ConversationAdminPermission.BanMembers);
        var member = ConversationParticipant.CreateActive(conversationId, Guid.NewGuid(), Now);
        Assert.True(admin.CanManage(ConversationAdminPermission.ManageInvites)); Assert.False(admin.CanManage(ConversationAdminPermission.ManageAdmins));
        Assert.False(admin.CanManage((ConversationAdminPermission)(1 << 30))); Assert.False(owner.CanManage(ConversationAdminPermission.None));
        Assert.False(admin.CanRestrictMember(owner)); Assert.True(admin.CanRestrictMember(member)); Assert.False(owner.CanRestrictMember(owner));
        Assert.True(owner.CanRestrictMember(admin));
    }
    [Fact]
    public void IndividualRestrictionOverridesCapabilityUntilExpiryAndClearingDoesNotRejoin()
    {
        var restriction = ConversationMemberRestriction.Create(Guid.NewGuid(), Guid.NewGuid());
        restriction.Update(false, GroupMemberCapability.SendMessages, Now.AddSeconds(10), Now);
        Assert.True(restriction.Denies(GroupMemberCapability.SendMessages | GroupMemberCapability.SendMedia, Now));
        Assert.False(restriction.Denies(GroupMemberCapability.SendMedia, Now)); Assert.False(restriction.IsEffective(Now.AddSeconds(10)));
        restriction.Update(false, GroupMemberCapability.None, null, Now); Assert.False(restriction.IsEffective(Now)); Assert.Equal(2, restriction.Revision);
    }
}
