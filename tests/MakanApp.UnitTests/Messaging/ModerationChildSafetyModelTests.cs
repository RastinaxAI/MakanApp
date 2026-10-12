using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ModerationChildSafetyModelTests
{
    [Fact]
    public void PersistedEnumValuesRemainCompatibleAndReviewReportsUsesNextFlag()
    {
        Assert.Equal(1, (int)AbuseReportReason.Harassment);
        Assert.Equal(6, (int)AbuseReportReason.Other);
        Assert.Equal(7, (int)AbuseReportReason.ChildSafetyConcern);
        Assert.Equal(256, (int)ConversationAdminPermission.ReviewReports);
        Assert.Equal(51, (int)ConversationAdminPermission.LegacyAdminDefault);
        Assert.False(ConversationAdminPermission.DeleteMessages.HasFlag(
            ConversationAdminPermission.ReviewReports));
    }

    [Fact]
    public void ModerationCaseFollowsExplicitReviewLifecycle()
    {
        var createdAtUtc = DateTime.UtcNow;
        var reviewerUserId = Guid.NewGuid();
        var moderationCase = ModerationCase.CreateForReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModerationQueueScope.ConversationModerators,
            ModerationPriority.Normal,
            createdAtUtc);

        moderationCase.StartReview(reviewerUserId, createdAtUtc.AddMinutes(1));
        moderationCase.Resolve(reviewerUserId, "نتیجه داخلی", createdAtUtc.AddMinutes(2));

        Assert.Equal(ModerationCaseStatus.Resolved, moderationCase.Status);
        Assert.Equal(reviewerUserId, moderationCase.AssignedReviewerUserId);
        Assert.Equal("نتیجه داخلی", moderationCase.InternalNote);
        Assert.NotNull(moderationCase.ReviewStartedAtUtc);
        Assert.NotNull(moderationCase.ResolvedAtUtc);
        Assert.Null(moderationCase.DismissedAtUtc);
    }

    [Fact]
    public void ModerationCaseRejectsInvalidTransitionAndDifferentResolver()
    {
        var moderationCase = ModerationCase.CreateForReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModerationQueueScope.ConversationModerators,
            ModerationPriority.Normal,
            DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            moderationCase.Resolve(Guid.NewGuid(), null, DateTime.UtcNow));
        var reviewer = Guid.NewGuid();
        moderationCase.StartReview(reviewer, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() =>
            moderationCase.Dismiss(Guid.NewGuid(), null, DateTime.UtcNow));
    }

    [Fact]
    public void ModerationCaseCanBeDismissedByAssignedReviewer()
    {
        var reviewer = Guid.NewGuid();
        var moderationCase = ModerationCase.CreateForReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModerationQueueScope.ConversationModerators,
            ModerationPriority.Normal,
            DateTime.UtcNow);

        moderationCase.StartReview(reviewer, DateTime.UtcNow);
        moderationCase.Dismiss(reviewer, null, DateTime.UtcNow);

        Assert.Equal(ModerationCaseStatus.Dismissed, moderationCase.Status);
        Assert.NotNull(moderationCase.DismissedAtUtc);
        Assert.Null(moderationCase.ResolvedAtUtc);
    }

    [Fact]
    public void AutomatedSafetyCaseIsAlwaysCriticalPlatformQueue()
    {
        var moderationCase = ModerationCase.CreateForSafetyIncident(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(ModerationCaseSource.AutomatedSafety, moderationCase.Source);
        Assert.Equal(ModerationQueueScope.PlatformSafety, moderationCase.QueueScope);
        Assert.Equal(ModerationPriority.Critical, moderationCase.Priority);
        Assert.Equal(ModerationCaseStatus.Pending, moderationCase.Status);
    }

    [Fact]
    public void SafetyIncidentAcceptsOnlyNonDeliveryDecisionsAndStableHashes()
    {
        var incident = SafetyIncident.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageKind.Text,
            ContentSafetyDecision.Blocked,
            "high",
            null,
            "policy-a",
            "شاهد",
            new byte[32],
            new byte[32],
            DateTime.UtcNow);

        Assert.Equal(SafetyIncidentStatus.Open, incident.Status);
        Assert.Equal(ContentSafetyDecision.Blocked, incident.Decision);
        Assert.Throws<ArgumentException>(() => SafetyIncident.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageKind.Text,
            ContentSafetyDecision.Allowed,
            null,
            null,
            null,
            "متن",
            new byte[32],
            new byte[32],
            DateTime.UtcNow));
    }

    [Fact]
    public void ReportStatusChangesOnlyThroughReviewLifecycle()
    {
        var report = AbuseReport.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            AbuseReportReason.ChildSafetyConcern,
            null,
            MessageKind.Text,
            "شاهد",
            new byte[8],
            new byte[32],
            DateTime.UtcNow);

        report.MarkUnderReview();
        report.Dismiss();

        Assert.Equal(AbuseReportStatus.Dismissed, report.Status);
        Assert.Throws<InvalidOperationException>(report.MarkUnderReview);
    }
}
