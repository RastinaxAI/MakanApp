using MakanApp.Application.Reports.Dashboards;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Dashboards;

public sealed partial class EfDashboardReadService : IDashboardReadService
{
    private const int SessionLimit = 10;
    private const int AssignmentLimit = 10;
    private const int FeedbackLimit = 5;
    private const int ExamLimit = 10;
    private const int ClassLimit = 20;

    private readonly MakanDbContext _dbContext;

    public EfDashboardReadService(MakanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private Task<Guid?> GetLearnerOrganizationPersonIdAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from organizationPerson in _dbContext.OrganizationPersons
            join user in _dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.OrganizationId == organizationId &&
                  user.Id == userId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select (Guid?)organizationPerson.Id)
        .SingleOrDefaultAsync(cancellationToken);

    private Task<DashboardLearner?> GetLearnerAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        (
            from organizationPerson in _dbContext.OrganizationPersons
            join person in _dbContext.Persons on organizationPerson.PersonId equals person.Id
            where organizationPerson.OrganizationId == organizationId &&
                  organizationPerson.Id == learnerOrganizationPersonId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select new DashboardLearner(
                organizationPerson.Id,
                person.DisplayName ?? person.FirstName + " " + person.LastName))
        .AsNoTracking()
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyCollection<DashboardSessionItem>> ReadUpcomingSessionsAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime dataAsOfUtc,
        int limit,
        CancellationToken cancellationToken) =>
        await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in _dbContext.Enrollments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            where session.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null &&
                  session.Status == SessionStatus.Scheduled &&
                  session.StartUtc >= dataAsOfUtc
            orderby session.StartUtc, session.Id
            select new DashboardSessionItem(
                session.Id,
                session.ClassId,
                academicClass.Title,
                session.Title,
                session.StartUtc,
                session.EndUtc,
                session.TimeZoneId,
                session.MeetingUrl,
                session.Status))
        .AsNoTracking()
        .Take(limit)
        .ToArrayAsync(cancellationToken);

    private IQueryable<DashboardAssignmentReadRow> CreateAssignmentsQuery(
        Guid organizationId,
        Guid learnerOrganizationPersonId)
    {
        return
            from recipient in _dbContext.AssignmentRecipients
            join enrollment in _dbContext.Enrollments
                on new { recipient.OrganizationId, Id = recipient.EnrollmentId }
                equals new { enrollment.OrganizationId, enrollment.Id }
            join assignment in _dbContext.Assignments
                on new { recipient.OrganizationId, Id = recipient.AssignmentId }
                equals new { assignment.OrganizationId, assignment.Id }
            join version in _dbContext.AssignmentVersions
                on new { recipient.OrganizationId, Id = recipient.AssignmentVersionId }
                equals new { version.OrganizationId, version.Id }
            join academicClass in _dbContext.Classes
                on new { recipient.OrganizationId, Id = recipient.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            let isGraded = _dbContext.GradeReleases.Any(release =>
                release.OrganizationId == organizationId &&
                _dbContext.SubmissionAttempts.Any(attempt =>
                    attempt.OrganizationId == organizationId &&
                    attempt.Id == release.SubmissionAttemptId &&
                    attempt.AssignmentRecipientId == recipient.Id))
            let hasLateSubmission = _dbContext.SubmissionAttempts.Any(attempt =>
                attempt.OrganizationId == organizationId &&
                attempt.AssignmentRecipientId == recipient.Id &&
                attempt.Status == SubmissionAttemptStatus.Submitted &&
                attempt.IsLate)
            let hasSubmission = _dbContext.SubmissionAttempts.Any(attempt =>
                attempt.OrganizationId == organizationId &&
                attempt.AssignmentRecipientId == recipient.Id &&
                attempt.Status == SubmissionAttemptStatus.Submitted)
            let hasDraft = _dbContext.SubmissionAttempts.Any(attempt =>
                attempt.OrganizationId == organizationId &&
                attempt.AssignmentRecipientId == recipient.Id &&
                attempt.Status == SubmissionAttemptStatus.Draft)
            where recipient.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null &&
                  (assignment.Status == AssignmentStatus.Published ||
                   assignment.Status == AssignmentStatus.Closed) &&
                  version.PublishedAtUtc != null
            select new DashboardAssignmentReadRow
            {
                AssignmentId = assignment.Id,
                AssignmentVersionId = version.Id,
                ClassId = assignment.ClassId,
                ClassTitle = academicClass.Title,
                Title = version.Title,
                DueAtUtc = version.DueAtUtc,
                AllowLateSubmission = version.AllowLateSubmission,
                IsGraded = isGraded,
                HasLateSubmission = hasLateSubmission,
                HasSubmission = hasSubmission,
                HasDraft = hasDraft
            };
    }

    private async Task<DashboardAttendanceSummary> ReadAttendanceSummaryAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken)
    {
        var eligibleSessions =
            from session in _dbContext.AcademicSessions
            join enrollment in _dbContext.Enrollments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            where session.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  session.Status != SessionStatus.Cancelled &&
                  session.EndUtc <= dataAsOfUtc
            select new { session.Id, EnrollmentId = enrollment.Id };

        var eligibleCount = await eligibleSessions
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var attendanceQuery =
            from attendance in _dbContext.Attendance
            join eligible in eligibleSessions
                on new { SessionId = attendance.SessionId, attendance.EnrollmentId }
                equals new { SessionId = eligible.Id, eligible.EnrollmentId }
            select attendance.Status;

        var recordedCount = await attendanceQuery.CountAsync(cancellationToken);
        var presentCount = await attendanceQuery.CountAsync(
            status => status == AttendanceStatus.Present,
            cancellationToken);
        var absentCount = await attendanceQuery.CountAsync(
            status => status == AttendanceStatus.Absent,
            cancellationToken);
        var lateCount = await attendanceQuery.CountAsync(
            status => status == AttendanceStatus.Late,
            cancellationToken);
        var excusedCount = await attendanceQuery.CountAsync(
            status => status == AttendanceStatus.Excused,
            cancellationToken);

        return new DashboardAttendanceSummary(
            eligibleCount,
            recordedCount,
            presentCount,
            absentCount,
            lateCount,
            excusedCount,
            Math.Max(0, eligibleCount - recordedCount));
    }

    private async Task<IReadOnlyCollection<DashboardExamItem>> ReadExamsAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken) =>
        await (
            from exam in _dbContext.Exams
            join version in _dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.LatestPublishedVersionNumber!.Value
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    version.VersionNumber
                }
            join academicClass in _dbContext.Classes
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in _dbContext.Enrollments
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            let latestAttemptStatus = _dbContext.ExamAttempts
                .Where(attempt =>
                    attempt.OrganizationId == organizationId &&
                    attempt.ExamId == exam.Id &&
                    attempt.EnrollmentId == enrollment.Id)
                .OrderByDescending(attempt => attempt.AttemptNumber)
                .Select(attempt => (ExamAttemptStatus?)attempt.Status)
                .FirstOrDefault()
            let releasedScore = (
                from attempt in _dbContext.ExamAttempts
                join revision in _dbContext.ExamGradeRevisions
                    on new { attempt.OrganizationId, ExamAttemptId = attempt.Id }
                    equals new { revision.OrganizationId, revision.ExamAttemptId }
                join release in _dbContext.ExamGradeReleases
                    on new { revision.OrganizationId, ExamGradeRevisionId = revision.Id }
                    equals new { release.OrganizationId, release.ExamGradeRevisionId }
                where attempt.OrganizationId == organizationId &&
                      attempt.ExamId == exam.Id &&
                      attempt.EnrollmentId == enrollment.Id &&
                      revision.Status == ExamGradeRevisionStatus.Released
                orderby release.ReleasedAtUtc descending
                select (decimal?)revision.TotalScore).FirstOrDefault()
            let releasedAtUtc = (
                from attempt in _dbContext.ExamAttempts
                join revision in _dbContext.ExamGradeRevisions
                    on new { attempt.OrganizationId, ExamAttemptId = attempt.Id }
                    equals new { revision.OrganizationId, revision.ExamAttemptId }
                join release in _dbContext.ExamGradeReleases
                    on new { revision.OrganizationId, ExamGradeRevisionId = revision.Id }
                    equals new { release.OrganizationId, release.ExamGradeRevisionId }
                where attempt.OrganizationId == organizationId &&
                      attempt.ExamId == exam.Id &&
                      attempt.EnrollmentId == enrollment.Id &&
                      revision.Status == ExamGradeRevisionStatus.Released
                orderby release.ReleasedAtUtc descending
                select (DateTime?)release.ReleasedAtUtc).FirstOrDefault()
            let hasReadyGrade = (
                from attempt in _dbContext.ExamAttempts
                join revision in _dbContext.ExamGradeRevisions
                    on new { attempt.OrganizationId, ExamAttemptId = attempt.Id }
                    equals new { revision.OrganizationId, revision.ExamAttemptId }
                where attempt.OrganizationId == organizationId &&
                      attempt.ExamId == exam.Id &&
                      attempt.EnrollmentId == enrollment.Id &&
                      revision.Status == ExamGradeRevisionStatus.ReadyForRelease
                select revision.Id).Any()
            let hasFinalizedAttempt = _dbContext.ExamAttempts.Any(attempt =>
                attempt.OrganizationId == organizationId &&
                attempt.ExamId == exam.Id &&
                attempt.EnrollmentId == enrollment.Id &&
                attempt.Status == ExamAttemptStatus.Finalized)
            where exam.OrganizationId == organizationId &&
                  exam.Status == ExamStatus.Published &&
                  exam.LatestPublishedVersionNumber != null &&
                  version.Status == ExamVersionStatus.Published &&
                  (version.AvailableUntilUtc >= dataAsOfUtc || releasedScore.HasValue) &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null
            orderby version.AvailableFromUtc, exam.Id
            select new DashboardExamItem(
                exam.Id,
                version.Id,
                exam.ClassId,
                academicClass.Title,
                version.Title,
                version.AvailableFromUtc,
                version.AvailableUntilUtc,
                version.DurationMinutes,
                latestAttemptStatus,
                releasedScore.HasValue
                    ? ExamResultReleaseStatus.Released
                    : hasReadyGrade
                        ? ExamResultReleaseStatus.AwaitingRelease
                        : hasFinalizedAttempt
                            ? ExamResultReleaseStatus.AwaitingGrading
                            : null,
                releasedScore,
                releasedScore.HasValue ? version.MaxScore : null,
                releasedAtUtc))
        .AsNoTracking()
        .Take(ExamLimit)
        .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyCollection<DashboardAttendancePendingItem>> ReadAttendancePendingAsync(
        Guid organizationId,
        Guid? teacherMembershipId,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken) =>
        await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            let expectedCount = _dbContext.Enrollments.Count(enrollment =>
                enrollment.OrganizationId == organizationId &&
                enrollment.ClassId == session.ClassId &&
                enrollment.Status == EnrollmentStatus.Active &&
                enrollment.EndedAtUtc == null)
            let recordedCount = _dbContext.Attendance.Count(attendance =>
                attendance.OrganizationId == organizationId &&
                attendance.SessionId == session.Id)
            where session.OrganizationId == organizationId &&
                  session.Status != SessionStatus.Cancelled &&
                  session.EndUtc <= dataAsOfUtc &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null &&
                  expectedCount > recordedCount &&
                  (!teacherMembershipId.HasValue || _dbContext.TeacherAssignments.Any(assignment =>
                      assignment.OrganizationId == organizationId &&
                      assignment.ClassId == session.ClassId &&
                      assignment.TeacherMembershipId == teacherMembershipId.Value &&
                      assignment.Status == TeacherAssignmentStatus.Active &&
                      assignment.EndedAtUtc == null))
            orderby session.EndUtc descending, session.Id
            select new DashboardAttendancePendingItem(
                session.Id,
                session.ClassId,
                academicClass.Title,
                session.Title,
                session.StartUtc,
                session.EndUtc,
                expectedCount,
                recordedCount,
                expectedCount - recordedCount))
        .AsNoTracking()
        .Take(SessionLimit)
        .ToArrayAsync(cancellationToken);

    private async Task<AssignmentGradingQueueSummary> ReadAssignmentQueueAsync(
        Guid organizationId,
        Guid? teacherMembershipId,
        CancellationToken cancellationToken)
    {
        var attemptIds =
            from attempt in _dbContext.SubmissionAttempts
            join assignment in _dbContext.Assignments
                on new { attempt.OrganizationId, Id = attempt.AssignmentId }
                equals new { assignment.OrganizationId, assignment.Id }
            where attempt.OrganizationId == organizationId &&
                  attempt.Status == SubmissionAttemptStatus.Submitted &&
                  (!teacherMembershipId.HasValue || _dbContext.TeacherAssignments.Any(teacher =>
                      teacher.OrganizationId == organizationId &&
                      teacher.ClassId == assignment.ClassId &&
                      teacher.TeacherMembershipId == teacherMembershipId.Value &&
                      teacher.Status == TeacherAssignmentStatus.Active &&
                      teacher.EndedAtUtc == null))
            select attempt.Id;

        var awaitingCount = await attemptIds.CountAsync(attemptId =>
            !_dbContext.EvaluationRevisions.Any(evaluation =>
                evaluation.OrganizationId == organizationId &&
                evaluation.SubmissionAttemptId == attemptId),
            cancellationToken);
        var draftCount = await attemptIds.CountAsync(attemptId =>
            _dbContext.EvaluationRevisions
                .Where(evaluation =>
                    evaluation.OrganizationId == organizationId &&
                    evaluation.SubmissionAttemptId == attemptId)
                .OrderByDescending(evaluation => evaluation.RevisionNumber)
                .Select(evaluation => evaluation.Status)
                .FirstOrDefault() == EvaluationRevisionStatus.Draft,
            cancellationToken);

        return new AssignmentGradingQueueSummary(
            awaitingCount,
            draftCount,
            awaitingCount + draftCount);
    }

    private async Task<ExamGradingQueueSummary> ReadExamQueueAsync(
        Guid organizationId,
        Guid? teacherMembershipId,
        CancellationToken cancellationToken)
    {
        var attemptIds =
            from attempt in _dbContext.ExamAttempts
            join exam in _dbContext.Exams
                on new { attempt.OrganizationId, ExamId = attempt.ExamId }
                equals new { exam.OrganizationId, ExamId = exam.Id }
            where attempt.OrganizationId == organizationId &&
                  attempt.Status == ExamAttemptStatus.Finalized &&
                  _dbContext.ExamAttemptQuestions.Any(mapping =>
                      mapping.OrganizationId == organizationId &&
                      mapping.ExamAttemptId == attempt.Id &&
                      _dbContext.QuestionVersions.Any(question =>
                          question.OrganizationId == organizationId &&
                          question.Id == mapping.QuestionVersionId &&
                          question.Type == ExamQuestionType.Descriptive)) &&
                  (!teacherMembershipId.HasValue || _dbContext.TeacherAssignments.Any(teacher =>
                      teacher.OrganizationId == organizationId &&
                      teacher.ClassId == exam.ClassId &&
                      teacher.TeacherMembershipId == teacherMembershipId.Value &&
                      teacher.Status == TeacherAssignmentStatus.Active &&
                      teacher.EndedAtUtc == null))
            select attempt.Id;

        var awaitingCount = await attemptIds.CountAsync(attemptId =>
            !_dbContext.ExamGradeRevisions.Any(revision =>
                revision.OrganizationId == organizationId &&
                revision.ExamAttemptId == attemptId),
            cancellationToken);
        var manualReviewCount = await attemptIds.CountAsync(attemptId =>
            _dbContext.ExamGradeRevisions
                .Where(revision =>
                    revision.OrganizationId == organizationId &&
                    revision.ExamAttemptId == attemptId)
                .OrderByDescending(revision => revision.RevisionNumber)
                .Select(revision => revision.Status)
                .FirstOrDefault() == ExamGradeRevisionStatus.Draft,
            cancellationToken);
        var readyCount = await attemptIds.CountAsync(attemptId =>
            _dbContext.ExamGradeRevisions
                .Where(revision =>
                    revision.OrganizationId == organizationId &&
                    revision.ExamAttemptId == attemptId)
                .OrderByDescending(revision => revision.RevisionNumber)
                .Select(revision => revision.Status)
                .FirstOrDefault() == ExamGradeRevisionStatus.ReadyForRelease,
            cancellationToken);

        return new ExamGradingQueueSummary(
            awaitingCount,
            manualReviewCount,
            readyCount,
            awaitingCount + manualReviewCount + readyCount);
    }

    private static DashboardAttendanceSummary EmptyAttendance() => new(0, 0, 0, 0, 0, 0, 0);

    private sealed class DashboardAssignmentReadRow
    {
        public Guid AssignmentId { get; init; }
        public Guid AssignmentVersionId { get; init; }
        public Guid ClassId { get; init; }
        public required string ClassTitle { get; init; }
        public required string Title { get; init; }
        public DateTime DueAtUtc { get; init; }
        public bool AllowLateSubmission { get; init; }
        public bool IsGraded { get; init; }
        public bool HasLateSubmission { get; init; }
        public bool HasSubmission { get; init; }
        public bool HasDraft { get; init; }
    }
}
