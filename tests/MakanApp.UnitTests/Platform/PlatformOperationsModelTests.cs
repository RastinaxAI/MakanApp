using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.UnitTests.Platform;

public sealed class PlatformOperationsModelTests
{
    private static readonly DateTime NowUtc =
        DateTime.SpecifyKind(new DateTime(2026, 10, 13, 8, 0, 0), DateTimeKind.Utc);

    [Fact]
    public void UserSuspensionHasExplicitReversibleState()
    {
        var user = User.Create(NowUtc.AddDays(-1));

        user.Suspend(NowUtc);

        Assert.Equal(UserAccountStatus.Suspended, user.AccountStatus);
        Assert.Equal(NowUtc, user.SuspendedAtUtc);

        user.Reactivate(NowUtc.AddMinutes(1));

        Assert.Equal(UserAccountStatus.Active, user.AccountStatus);
        Assert.Null(user.SuspendedAtUtc);
    }

    [Fact]
    public void OrganizationCanBeSuspendedAndExplicitlyReactivated()
    {
        var organization = MakanApp.Domain.Organization.Organization.Create("Test", NowUtc);

        organization.Suspend();
        Assert.Equal(OrganizationStatus.Suspended, organization.Status);

        organization.Reactivate();
        Assert.Equal(OrganizationStatus.Active, organization.Status);
    }

    [Fact]
    public void ProvisioningStagesRemainExplicitAndPayloadCanBeMatchedForRetry()
    {
        var managerId = Guid.NewGuid();
        var planVersionId = Guid.NewGuid();
        var endsAtUtc = NowUtc.AddDays(30);
        var invitationExpiresAtUtc = NowUtc.AddDays(7);
        var request = PlatformProvisioningRequest.Create(
            "client-operation",
            Guid.NewGuid(),
            "School",
            planVersionId,
            managerId,
            endsAtUtc,
            invitationExpiresAtUtc,
            "approved sale",
            NowUtc);

        request.MarkOrganizationCreated(Guid.NewGuid(), NowUtc.AddSeconds(1));
        request.MarkSubscriptionAssigned(Guid.NewGuid(), NowUtc.AddSeconds(2));
        request.MarkManagerInvitationCreated(Guid.NewGuid(), NowUtc.AddSeconds(3));
        request.Complete(NowUtc.AddSeconds(4));

        Assert.True(request.Matches("School", planVersionId, managerId, endsAtUtc, invitationExpiresAtUtc));
        Assert.Equal(PlatformProvisioningStatus.Completed, request.Status);
    }
}
