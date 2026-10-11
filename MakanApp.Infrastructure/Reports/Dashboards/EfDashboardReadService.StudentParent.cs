using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Dashboards;

public sealed partial class EfDashboardReadService
{
    public async Task<StudentDashboardDto> ReadStudentAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken)
    {
        var organizationId = accessContext.OrganizationId!.Value;
        var learnerId = await GetLearnerOrganizationPersonIdAsync(
            organizationId,
            accessContext.UserId,
            cancellationToken);
        if (!learnerId.HasValue)
        {
            return new StudentDashboardDto(
                dataAsOfUtc,
                organizationId,
                DashboardAudience.Student,
                null,
                null,
                [],
                [],
                EmptyAttendance(),
                0);
        }

        var sessions = await ReadUpcomingSessionsAsync(
            organizationId,
            learnerId.Value,
            dataAsOfUtc,
            1,
            cancellationToken);
        var actionableAssignments = CreateAssignmentsQuery(organizationId, learnerId.Value)
            .Where(item =>
                item.IsActionable &&
                item.DueAtUtc > dataAsOfUtc &&
                !item.HasSubmission &&
                !item.IsGraded);
        var nearestAssignment = await actionableAssignments
            .AsNoTracking()
            .OrderBy(item => item.DueAtUtc)
            .ThenBy(item => item.AssignmentId)
            .Select(item => new DashboardAssignmentItem(
                item.AssignmentId,
                item.AssignmentVersionId,
                item.ClassId,
                item.ClassTitle,
                item.Title,
                item.DueAtUtc,
                item.AllowLateSubmission,
                item.HasDraft
                    ? StudentAssignmentActionStatus.Draft
                    : StudentAssignmentActionStatus.NotStarted))
            .FirstOrDefaultAsync(cancellationToken);
        var pendingAssignmentCount = await actionableAssignments.CountAsync(cancellationToken);
        var feedback = await ReadStudentReleasedAssignmentsAsync(
            organizationId,
            learnerId.Value,
            cancellationToken);
        var exams = await ReadExamsAsync(
            organizationId,
            learnerId.Value,
            dataAsOfUtc,
            cancellationToken);
        var attendance = await ReadAttendanceSummaryAsync(
            organizationId,
            learnerId.Value,
            dataAsOfUtc,
            cancellationToken);

        return new StudentDashboardDto(
            dataAsOfUtc,
            organizationId,
            DashboardAudience.Student,
            sessions.FirstOrDefault(),
            nearestAssignment,
            feedback,
            exams,
            attendance,
            pendingAssignmentCount);
    }

    public async Task<ParentDashboardDto> ReadParentAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken)
    {
        var organizationId = accessContext.OrganizationId!.Value;
        if (!accessContext.SubjectOrganizationPersonId.HasValue)
        {
            return new ParentDashboardDto(
                dataAsOfUtc,
                organizationId,
                DashboardAudience.Parent,
                null,
                [],
                [],
                [],
                [],
                EmptyAttendance(),
                0);
        }

        var learnerId = accessContext.SubjectOrganizationPersonId.Value;
        var learner = await GetLearnerAsync(organizationId, learnerId, cancellationToken);
        if (learner is null)
        {
            return new ParentDashboardDto(
                dataAsOfUtc,
                organizationId,
                DashboardAudience.Parent,
                null,
                [],
                [],
                [],
                [],
                EmptyAttendance(),
                0);
        }

        var sessions = await ReadUpcomingSessionsAsync(
            organizationId,
            learnerId,
            dataAsOfUtc,
            SessionLimit,
            cancellationToken);
        var assignmentsQuery = CreateAssignmentsQuery(organizationId, learnerId);
        var assignments = await assignmentsQuery
            .AsNoTracking()
            .OrderByDescending(item => item.DueAtUtc)
            .ThenBy(item => item.AssignmentId)
            .Select(item => new DashboardAssignmentItem(
                item.AssignmentId,
                item.AssignmentVersionId,
                item.ClassId,
                item.ClassTitle,
                item.Title,
                item.DueAtUtc,
                item.AllowLateSubmission,
                item.IsGraded
                    ? StudentAssignmentActionStatus.Graded
                    : item.HasLateSubmission
                        ? StudentAssignmentActionStatus.SubmittedLate
                        : item.HasSubmission
                            ? StudentAssignmentActionStatus.Submitted
                            : item.HasDraft
                                ? StudentAssignmentActionStatus.Draft
                                : StudentAssignmentActionStatus.NotStarted))
            .Take(AssignmentLimit)
            .ToArrayAsync(cancellationToken);
        var attentionCount = await assignmentsQuery.CountAsync(item =>
            item.DueAtUtc > dataAsOfUtc &&
            !item.HasSubmission &&
            !item.IsGraded,
            cancellationToken);
        var releasedResults = await ReadParentReleasedAssignmentsAsync(
            organizationId,
            learnerId,
            cancellationToken);
        var exams = await ReadExamsAsync(
            organizationId,
            learnerId,
            dataAsOfUtc,
            cancellationToken);
        var attendance = await ReadAttendanceSummaryAsync(
            organizationId,
            learnerId,
            dataAsOfUtc,
            cancellationToken);

        return new ParentDashboardDto(
            dataAsOfUtc,
            organizationId,
            DashboardAudience.Parent,
            learner,
            sessions,
            assignments,
            releasedResults,
            exams,
            attendance,
            attentionCount);
    }

    private async Task<IReadOnlyCollection<StudentReleasedAssignmentItem>>
        ReadStudentReleasedAssignmentsAsync(
            Guid organizationId,
            Guid learnerOrganizationPersonId,
            CancellationToken cancellationToken) =>
        await (
            from release in _dbContext.GradeReleases
            join evaluation in _dbContext.EvaluationRevisions
                on new { release.OrganizationId, EvaluationRevisionId = release.EvaluationRevisionId }
                equals new { evaluation.OrganizationId, EvaluationRevisionId = evaluation.Id }
            join attempt in _dbContext.SubmissionAttempts
                on new { release.OrganizationId, SubmissionAttemptId = release.SubmissionAttemptId }
                equals new { attempt.OrganizationId, SubmissionAttemptId = attempt.Id }
            join enrollment in _dbContext.Enrollments
                on new { attempt.OrganizationId, attempt.EnrollmentId }
                equals new { enrollment.OrganizationId, EnrollmentId = enrollment.Id }
            join version in _dbContext.AssignmentVersions
                on new { attempt.OrganizationId, AssignmentVersionId = attempt.AssignmentVersionId }
                equals new { version.OrganizationId, AssignmentVersionId = version.Id }
            where release.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  evaluation.Status == EvaluationRevisionStatus.Released
            orderby release.ReleasedAtUtc descending, release.Id
            select new StudentReleasedAssignmentItem(
                attempt.Id,
                attempt.AssignmentId,
                version.Title,
                evaluation.Score,
                version.MaxScore,
                evaluation.LearnerFeedback,
                release.ReleasedAtUtc))
        .AsNoTracking()
        .Take(FeedbackLimit)
        .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyCollection<ParentReleasedAssignmentItem>>
        ReadParentReleasedAssignmentsAsync(
            Guid organizationId,
            Guid learnerOrganizationPersonId,
            CancellationToken cancellationToken) =>
        await (
            from release in _dbContext.GradeReleases
            join evaluation in _dbContext.EvaluationRevisions
                on new { release.OrganizationId, EvaluationRevisionId = release.EvaluationRevisionId }
                equals new { evaluation.OrganizationId, EvaluationRevisionId = evaluation.Id }
            join attempt in _dbContext.SubmissionAttempts
                on new { release.OrganizationId, SubmissionAttemptId = release.SubmissionAttemptId }
                equals new { attempt.OrganizationId, SubmissionAttemptId = attempt.Id }
            join enrollment in _dbContext.Enrollments
                on new { attempt.OrganizationId, attempt.EnrollmentId }
                equals new { enrollment.OrganizationId, EnrollmentId = enrollment.Id }
            join version in _dbContext.AssignmentVersions
                on new { attempt.OrganizationId, AssignmentVersionId = attempt.AssignmentVersionId }
                equals new { version.OrganizationId, AssignmentVersionId = version.Id }
            where release.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  evaluation.Status == EvaluationRevisionStatus.Released
            orderby release.ReleasedAtUtc descending, release.Id
            select new ParentReleasedAssignmentItem(
                attempt.Id,
                attempt.AssignmentId,
                version.Title,
                evaluation.Score,
                version.MaxScore,
                evaluation.GuardianVisibleFeedback,
                release.ReleasedAtUtc))
        .AsNoTracking()
        .Take(FeedbackLimit)
        .ToArrayAsync(cancellationToken);
}
