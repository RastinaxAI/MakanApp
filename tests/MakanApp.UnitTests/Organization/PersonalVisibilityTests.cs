using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Organization;

public sealed class PersonalVisibilityTests
{
    [Fact]
    public void NoRoleOrPendingInvitationDoesNotHidePersonal()
    {
        var snapshot = WorkspaceFactory.CreateSnapshot([]);
        Assert.True(snapshot.PersonalWorkspaceVisible);
        Assert.False(snapshot.HasEffectiveEducationalRole);
        Assert.True(snapshot.Workspaces.Single().CanEnter);
        Assert.Null(snapshot.Workspaces.Single().Role);
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Manager)]
    public void EffectiveRoleHidesCardWithoutDisablingAccount(OrganizationRole role)
    {
        var snapshot = WorkspaceFactory.CreateSnapshot([Record(role)]);
        Assert.False(snapshot.PersonalWorkspaceVisible);
        Assert.True(snapshot.HasEffectiveEducationalRole);
        Assert.All(snapshot.Workspaces, w => Assert.True(w.HasEffectiveEducationalRole));
        Assert.True(snapshot.Workspaces.Single(w => w.WorkspaceType == WorkspaceType.Personal).CanEnter);
    }

    [Fact]
    public void EndingOneRolePreservesParentInOtherOrganization()
    {
        var teacher = Record(OrganizationRole.Teacher) with { RoleStatus = RoleAssignmentStatus.Ended };
        var parent = Record(OrganizationRole.Parent);
        var snapshot = WorkspaceFactory.CreateSnapshot([teacher, parent]);
        Assert.False(snapshot.PersonalWorkspaceVisible);
        Assert.False(snapshot.Workspaces.Single(w => w.Role == OrganizationRole.Teacher).CanEnter);
        Assert.True(snapshot.Workspaces.Single(w => w.Role == OrganizationRole.Parent).CanEnter);
        Assert.NotEqual(teacher.OrganizationId, parent.OrganizationId);
    }

    [Fact]
    public void LastRoleEndedRestoresCardAndChangesSnapshotVersion()
    {
        var teacher = Record(OrganizationRole.Teacher);
        var before = WorkspaceFactory.CreateSnapshot([teacher]);
        var after = WorkspaceFactory.CreateSnapshot([teacher with { RoleStatus = RoleAssignmentStatus.Ended }]);
        Assert.True(after.PersonalWorkspaceVisible);
        Assert.NotEqual(before.Version, after.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SuspendedVisibilityRemainsAnExplicitOpenDecision(bool organizationSuspended)
    {
        var role = Record(OrganizationRole.Teacher) with
        {
            OrganizationStatus = organizationSuspended ? OrganizationStatus.Suspended : OrganizationStatus.Active,
            MembershipStatus = organizationSuspended ? MembershipStatus.Active : MembershipStatus.Suspended
        };
        var snapshot = WorkspaceFactory.CreateSnapshot([role]);
        Assert.Null(snapshot.PersonalWorkspaceVisible);
        Assert.Equal("ProductDecisionRequired", snapshot.PersonalVisibilityDecision);
        Assert.False(snapshot.Workspaces.Single(w => w.Role.HasValue).CanEnter);
        Assert.True(snapshot.Workspaces.Single(w => !w.Role.HasValue).CanEnter);
    }

    [Fact]
    public void AnotherEffectiveRoleDeterminesVisibilityDespiteSuspendedRole()
    {
        var snapshot = WorkspaceFactory.CreateSnapshot([Record(OrganizationRole.Teacher) with { MembershipStatus = MembershipStatus.Suspended }, Record(OrganizationRole.Parent)]);
        Assert.False(snapshot.PersonalWorkspaceVisible);
    }

    [Fact]
    public void SnapshotVersionIsIndependentOfRecordOrdering()
    {
        var first = Record(OrganizationRole.Teacher); var second = Record(OrganizationRole.Parent);
        Assert.Equal(WorkspaceFactory.CreateSnapshot([first, second]).Version, WorkspaceFactory.CreateSnapshot([second, first]).Version);
    }

    private static OrganizationWorkspaceRecord Record(OrganizationRole role) =>
        new(Guid.NewGuid(), "سازمان آزمایشی", Guid.NewGuid(), role);
}
