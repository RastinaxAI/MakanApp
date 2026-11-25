using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class PublishedAssignmentPolicyTests
{
    private static readonly DateTime Created = new(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PublishedExtensionChangesOnlySubmissionPolicy()
    {
        var version = Version(); version.Publish(Created.AddMinutes(1));
        var before = (version.Id, version.AssignmentId, version.VersionNumber, version.Title, version.Description,
            version.MaxScore, version.MaxAttempts, version.CreatedAtUtc, version.PublishedAtUtc);
        version.UpdatePublishedSubmissionPolicy(Created.AddDays(2), true, Created.AddHours(1));
        Assert.Equal(before, (version.Id, version.AssignmentId, version.VersionNumber, version.Title, version.Description,
            version.MaxScore, version.MaxAttempts, version.CreatedAtUtc, version.PublishedAtUtc));
        Assert.Equal(Created.AddDays(2), version.DueAtUtc);
        Assert.True(version.AllowLateSubmission); Assert.True(version.IsPublished);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-48)]
    public void PublishedDeadlineCannotBeShortened(int hours)
    {
        var version = Version(); version.Publish(Created.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => version.UpdatePublishedSubmissionPolicy(
            version.DueAtUtc.AddHours(hours), true, Created.AddHours(1)));
        Assert.Equal(Created.AddDays(1), version.DueAtUtc);
        Assert.False(version.AllowLateSubmission);
    }

    [Fact]
    public void NewExtendedDeadlineMustBeInTheFuture()
    {
        var version = Version(); version.Publish(Created.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => version.UpdatePublishedSubmissionPolicy(
            Created.AddDays(2), true, Created.AddDays(3)));
    }

    [Fact]
    public void LatePermissionCanBeEnabledAndDisabledWithoutReplacingExpiredDeadline()
    {
        var version = Version(); version.Publish(Created.AddMinutes(1));
        var due = version.DueAtUtc;
        version.UpdatePublishedSubmissionPolicy(due, true, Created.AddDays(2));
        Assert.True(version.AllowLateSubmission);
        version.UpdatePublishedSubmissionPolicy(due, false, Created.AddDays(2));
        Assert.False(version.AllowLateSubmission); Assert.Equal(due, version.DueAtUtc);
    }

    [Fact]
    public void DraftCannotUseThePublishedPolicyMutation()
    {
        var version = Version();
        Assert.Throws<InvalidOperationException>(() => version.UpdatePublishedSubmissionPolicy(
            Created.AddDays(2), true, Created.AddHours(1)));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void NewDeadlineMustExplicitlyRepresentUtc(DateTimeKind kind)
    {
        var version = Version(); version.Publish(Created.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => version.UpdatePublishedSubmissionPolicy(
            DateTime.SpecifyKind(Created.AddDays(2), kind), true, Created.AddHours(1)));
    }

    [Fact]
    public void ExtendingPolicyDoesNotRewriteHistoricalReceiptLateFlag()
    {
        var version = Version(); version.Publish(Created.AddMinutes(1));
        version.UpdatePublishedSubmissionPolicy(version.DueAtUtc, true, Created.AddHours(1));
        var attempt = SubmissionAttempt.CreateDraft(version.Scope, version.AssignmentId, version.Id,
            Guid.NewGuid(), new AcademicParticipantRef(AcademicClassKind.OrganizationClass, Guid.NewGuid()), 1, Created);
        attempt.SaveAnswer("پاسخ تاریخی", Created.AddHours(1));
        var acceptedAt = version.DueAtUtc.AddTicks(1);
        attempt.Submit(acceptedAt, version.DueAtUtc, true, false);
        version.UpdatePublishedSubmissionPolicy(Created.AddDays(2), false, acceptedAt);
        Assert.True(attempt.IsLate); Assert.Equal(acceptedAt, attempt.SubmittedAtUtc);
        Assert.Equal(SubmissionAttemptStatus.Submitted, attempt.Status);
    }

    private static AssignmentVersion Version() => AssignmentVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), 1, "عنوان ثابت", "محتوای ثابت", Created.AddDays(1), false, 3, 10m, Guid.NewGuid(), Created);
}
