using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class SchoolInitialManagerTests
{
    [Fact]
    public void ExistingActionFingerprintShapeIsPreservedWhenManagerIsNotApplicable()
    {
        var operation = Guid.NewGuid();
        var command = new MakanApp.Application.Organization.SchoolApplicationActionCommand(operation, 3, "reason", SchoolApplicationStatus.NeedsChanges);
        var oldShape = new { ClientOperationId = operation, ExpectedVersion = 3L, ApplicantFeedback = "reason", Decision = (SchoolApplicationStatus?)SchoolApplicationStatus.NeedsChanges };
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(oldShape), System.Text.Json.JsonSerializer.Serialize(command));
    }

    private static SchoolApplication Reviewing()
    {
        var now = DateTime.UtcNow;
        var app = SchoolApplication.Create(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization, new("School", "Legal", null, null, null, 0, 0), now);
        app.Submit("approved", now, ["License"]); app.StartReview(now); return app;
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public void ApprovalRequiresExplicitManagerAndReviewerWithoutPartialMutation(bool manager, bool reviewer)
    {
        var app = Reviewing(); var version = app.Version;
        Assert.Null(app.FinalInitialManagerUserId); Assert.Equal(app.ApplicantUserId, app.ProposedInitialManagerUserId);
        Assert.Throws<ArgumentException>(() => app.Decide(SchoolApplicationStatus.Approved, DateTime.UtcNow,
            manager ? Guid.NewGuid() : null, reviewer ? Guid.NewGuid() : null));
        Assert.Equal(version, app.Version); Assert.Equal(SchoolApplicationStatus.UnderReview, app.Status);
        Assert.Null(app.FinalInitialManagerUserId); Assert.Null(app.InitialManagerConfirmedAtUtc);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitManagerSelectionIsFrozenAndHistoryContainsActualDecision(bool applicant)
    {
        var app = Reviewing(); var reviewer = Guid.NewGuid(); var target = applicant ? app.ApplicantUserId : Guid.NewGuid(); var now = DateTime.UtcNow;
        app.Decide(SchoolApplicationStatus.Approved, now, target, reviewer);
        Assert.Equal(target, app.FinalInitialManagerUserId); Assert.Equal(reviewer, app.InitialManagerConfirmedByUserId); Assert.Equal(now, app.InitialManagerConfirmedAtUtc);
        var decision = SchoolApplicationDecision.Create(app, reviewer, "explicit selection", now);
        var history = SchoolApplicationHistory.Create(app, reviewer, "Approved", "explicit selection", now);
        Assert.Equal(target, history.FinalInitialManagerUserId); Assert.Equal(app.ApplicantUserId, history.ProposedInitialManagerUserId);
        Assert.Equal(app.Version, decision.ApplicationVersion); Assert.Equal(target, decision.FinalInitialManagerUserId);
        Assert.Throws<InvalidOperationException>(() => app.ConfirmMissingInitialManager(Guid.NewGuid(), reviewer, now));
        Assert.Throws<InvalidOperationException>(() => app.UpdateDraft(app.DraftData(), now));
    }

    [Theory]
    [InlineData(SchoolApplicationStatus.NeedsChanges)] [InlineData(SchoolApplicationStatus.Rejected)]
    public void NonApprovalDoesNotRequireOrAssignAManager(SchoolApplicationStatus decision)
    {
        var app = Reviewing(); app.Decide(decision, DateTime.UtcNow);
        Assert.Null(app.FinalInitialManagerUserId); Assert.Null(app.InitialManagerConfirmedByUserId);
    }

    [Fact]
    public void MissingAndIncompleteAndSuspendedAccountAreIneligible()
    {
        Assert.False(SchoolInitialManagerPolicy.IsEligible(null));
        var user = User.Create(DateTime.UtcNow); Assert.False(SchoolInitialManagerPolicy.IsEligible(user));
        user.Suspend(DateTime.UtcNow); Assert.False(SchoolInitialManagerPolicy.IsEligible(user));
    }

    [Fact]
    public void SchoolProvisioningFreezesPaymentManagerAndInvitationDeadline()
    {
        var now = DateTime.UtcNow; var manager = Guid.NewGuid(); var reviewer = Guid.NewGuid(); var payment = Guid.NewGuid();
        var request = PlatformProvisioningRequest.ForPaidSchool(Guid.NewGuid(), Guid.NewGuid(), payment, reviewer, manager, "School", Guid.NewGuid(), now);
        Assert.Null(request.InvitationExpiresAtUtc);
        request.SetSchoolInvitationExpiry(now.AddDays(7), now); request.SetSchoolInvitationExpiry(now.AddDays(10), now);
        Assert.Equal(now.AddDays(7), request.InvitationExpiresAtUtc);
        Assert.Throws<InvalidOperationException>(() => request.ConfirmSchoolManager(Guid.NewGuid(), reviewer, payment, now));
        Assert.Throws<InvalidOperationException>(() => request.ConfirmSchoolManager(manager, reviewer, Guid.NewGuid(), now));
        Assert.Throws<InvalidOperationException>(() => request.Complete(now));
        request.MarkOrganizationCreated(Guid.NewGuid(), now); var organization = request.OrganizationId;
        request.MarkNeedsAttention("synthetic_failure", now); request.Resume(now);
        Assert.Equal(organization, request.OrganizationId); Assert.Equal(PlatformProvisioningStatus.OrganizationCreated, request.Status);
    }
}
