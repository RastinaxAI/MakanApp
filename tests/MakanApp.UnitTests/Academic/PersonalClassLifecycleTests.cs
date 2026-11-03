using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Academic;

public sealed class PersonalClassLifecycleTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void ApprovalKeepsCommercialHostAndRequiresAcceptedManagerBeforeActivation()
    {
        var request = Request(); request.Submit(Now);
        var classroom = PersonalClass.FromApproval(request, Now);
        Assert.Equal(request.RequesterUserId, classroom.HostUserId);
        Assert.Null(classroom.ManagerUserId); Assert.Equal(10, classroom.Capacity);
        classroom.RecordProvisioning(true);
        Assert.Throws<InvalidOperationException>(() => classroom.Activate(true));
        var manager = Guid.NewGuid(); classroom.SetManager(manager); classroom.Activate(true);
        Assert.Equal(PersonalClassState.Active, classroom.State);
        Assert.Equal(request.RequesterUserId, classroom.HostUserId);
        Assert.NotEqual(classroom.HostUserId, classroom.ManagerUserId);
    }
    [Fact]
    public void LegacyHasNoGuessedCapacityAndDoesNotOpenJoinRequests()
    {
        var c = PersonalClass.Create(Guid.NewGuid(), "قدیمی", Guid.NewGuid(), Now);
        Assert.Null(c.Capacity); Assert.False(c.AcceptingJoinRequests); Assert.Equal(PersonalClassMigrationState.LegacyActive, c.MigrationState);
        c.Configure(3, true, 3);
        Assert.Throws<InvalidOperationException>(() => c.Configure(2, true, 3));
        c.Archive(Now); Assert.False(c.AcceptingJoinRequests);
        Assert.Throws<InvalidOperationException>(() => c.Configure(4, true, 3));
    }
    [Fact]
    public void ManagerTeacherCombinationDoesNotImplicitlyMakeLearnerOrDelegateTeacherPowers()
    {
        var member = PersonalClassParticipant.Admit(Guid.NewGuid(), Guid.NewGuid(), PersonalClassLocalRoles.Manager | PersonalClassLocalRoles.Teacher, Now);
        Assert.False(member.LocalRoles.HasFlag(PersonalClassLocalRoles.Learner));
        Assert.Equal(PersonalClassDelegatedCapabilities.None, member.DelegatedCapabilities);
        member.ChangeRoles(PersonalClassLocalRoles.Teacher, PersonalClassDelegatedCapabilities.PublishCommunication);
        Assert.Throws<InvalidOperationException>(() => member.ChangeRoles(PersonalClassLocalRoles.Learner, PersonalClassDelegatedCapabilities.PublishCommunication));
        member.End(Now, "پایان عضویت");
        Assert.Throws<InvalidOperationException>(() => member.ChangeRoles(PersonalClassLocalRoles.Manager, PersonalClassDelegatedCapabilities.None));
    }
    [Fact]
    public void TransferRequiresRecipientConsentAndCannotBeDecidedTwice()
    {
        var manager = Guid.NewGuid(); var target = Guid.NewGuid();
        var transfer = PersonalClassManagerTransfer.Create(Guid.NewGuid(), manager, manager, target, Guid.NewGuid(), "تحویل مسئولیت", Now);
        Assert.Throws<InvalidOperationException>(() => transfer.Decide(PersonalClassManagerTransferStatus.Accepted, manager, Now));
        transfer.Decide(PersonalClassManagerTransferStatus.Accepted, target, Now);
        Assert.Equal(PersonalClassManagerTransferStatus.Accepted, transfer.Status);
        Assert.Throws<InvalidOperationException>(() => transfer.Decide(PersonalClassManagerTransferStatus.Accepted, target, Now));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosedInvitationCannotBeAccepted(bool decline)
    {
        var user = Guid.NewGuid();
        var invitation = PersonalClassInvitation.Create(Guid.NewGuid(), user, Guid.NewGuid(), Now, Now.AddDays(7));
        invitation.Close(decline, "بسته‌شدن صریح", Now);
        Assert.Equal(decline ? "Declined" : "Revoked", invitation.StatusAt(Now));
        Assert.Throws<InvalidOperationException>(() => invitation.Accept(user, Now));
    }
    [Fact]
    public void InvitationExpiresAtServerDeadlineAndReplayDoesNotExtendIt()
    {
        var user = Guid.NewGuid(); var invitation = PersonalClassInvitation.Create(Guid.NewGuid(), user, Guid.NewGuid(), Now, Now.AddDays(7));
        Assert.Equal("Expired", invitation.StatusAt(Now.AddDays(7)));
        Assert.Throws<InvalidOperationException>(() => invitation.Accept(user, Now.AddDays(7)));
        Assert.True(invitation.Accept(user, Now)); Assert.False(invitation.Accept(user, Now.AddMinutes(1)));
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAtUtc);
    }
    [Fact]
    public void JoinApprovalRequiresRealMembershipBindingAndClosureIsFinal()
    {
        var join = PersonalClassJoinRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Null(join.MembershipId);
        Assert.Throws<ArgumentException>(() => join.Close(PersonalClassJoinRequestStatus.Approved, "تأیید", Now));
        var member = Guid.NewGuid(); join.Close(PersonalClassJoinRequestStatus.Approved, "تأیید", Now, member);
        Assert.Equal(member, join.MembershipId);
        Assert.Throws<InvalidOperationException>(() => join.Close(PersonalClassJoinRequestStatus.Cancelled, "لغو", Now));
    }
    [Theory]
    [InlineData(ConversationType.Group)]
    [InlineData(ConversationType.Channel)]
    public void IndependentSpaceHasTypedSourceWithoutOrganizationOrUserOwner(ConversationType purpose)
    {
        var id = Guid.NewGuid(); var c = Conversation.CreatePersonalClassSpace(purpose, id, "فضای کلاس", Now);
        Assert.Equal(id, c.PersonalClassId); Assert.Null(c.OrganizationId); Assert.Null(c.AcademicClassId);
        Assert.Null(c.CreatedByUserId); Assert.Null(c.ClientOperationId);
        Assert.Equal(ConversationScope.Personal, c.Scope);
        Assert.Equal(ConversationManagementPolicy.SystemManagedAcademic, c.ManagementPolicy);
        var provision = OfficialSpaceProvision.RequestPersonalClass(id, purpose, Now);
        Assert.Null(provision.OrganizationId); Assert.Null(provision.ClassId); Assert.Equal(id, provision.PersonalClassId);
    }
    private static PersonalClassRequest Request() => PersonalClassRequest.Create(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), "کلاس", "هدف", null, 10, null, null, Now);
}
