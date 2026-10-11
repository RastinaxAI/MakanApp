using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Detailed;

public sealed partial class EfDetailedReportReadService
{
    public async Task<AttendanceReportPage<StudentAttendanceReportItem>> ReadStudentAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var learnerId = await GetStudentLearnerIdAsync(context, cancellationToken);
        return learnerId.HasValue
            ? await ReadStudentAttendanceCoreAsync(context.OrganizationId!.Value, learnerId.Value, dataAsOfUtc, query, cancellationToken)
            : AttendancePage(dataAsOfUtc, query, EmptyAttendanceSummary, Array.Empty<StudentAttendanceReportItem>());
    }

    public async Task<AttendanceReportPage<ParentAttendanceReportItem>> ReadParentAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var learnerId = context.SubjectOrganizationPersonId!.Value;
        var rows = CreateAttendanceRows(organizationId, learnerId, dataAsOfUtc, query);
        var summary = await ReadAttendanceSummaryAsync(rows, cancellationToken);
        var ordered = query.Sort == "occurredasc"
            ? rows.OrderBy(row => row.StartUtc).ThenBy(row => row.SessionId)
            : rows.OrderByDescending(row => row.StartUtc).ThenBy(row => row.SessionId);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ParentAttendanceReportItem(row.SessionId, row.ClassId, row.ClassTitle, row.CourseTitle, row.SessionTitle, row.StartUtc, row.EndUtc, row.Status ?? AttendanceStatus.NotRecorded, row.RecordedAtUtc))
            .ToArrayAsync(cancellationToken);
        return AttendancePage(dataAsOfUtc, query, summary, items);
    }

    private async Task<AttendanceReportPage<StudentAttendanceReportItem>> ReadStudentAttendanceCoreAsync(Guid organizationId, Guid learnerId, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var rows = CreateAttendanceRows(organizationId, learnerId, dataAsOfUtc, query);
        var summary = await ReadAttendanceSummaryAsync(rows, cancellationToken);
        var ordered = query.Sort == "occurredasc"
            ? rows.OrderBy(row => row.StartUtc).ThenBy(row => row.SessionId)
            : rows.OrderByDescending(row => row.StartUtc).ThenBy(row => row.SessionId);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new StudentAttendanceReportItem(row.SessionId, row.ClassId, row.ClassTitle, row.CourseTitle, row.SessionTitle, row.StartUtc, row.EndUtc, row.Status ?? AttendanceStatus.NotRecorded, row.RecordedAtUtc))
            .ToArrayAsync(cancellationToken);
        return AttendancePage(dataAsOfUtc, query, summary, items);
    }

    private static async Task<AttendanceReportSummary> ReadAttendanceSummaryAsync(IQueryable<AttendanceRow> rows, CancellationToken cancellationToken)
    {
        var summary = await rows.GroupBy(_ => 1).Select(group => new AttendanceReportSummary(
            group.Count(),
            group.Count(row => row.Status != null),
            group.Count(row => row.Status == AttendanceStatus.Present),
            group.Count(row => row.Status == AttendanceStatus.Absent),
            group.Count(row => row.Status == AttendanceStatus.Late),
            group.Count(row => row.Status == AttendanceStatus.Excused),
            group.Count(row => row.Status == null)))
            .SingleOrDefaultAsync(cancellationToken);
        return summary ?? EmptyAttendanceSummary;
    }

    private IQueryable<AttendanceRow> CreateAttendanceRows(Guid organizationId, Guid learnerId, DateTime dataAsOfUtc, ReportQuerySpec query)
    {
        var rows =
            from session in _dbContext.AcademicSessions
            join enrollment in _dbContext.Enrollments on new { session.OrganizationId, Id = session.ClassId } equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            join academicClass in _dbContext.Classes on new { session.OrganizationId, Id = session.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join course in _dbContext.Courses on new { academicClass.OrganizationId, Id = academicClass.CourseId } equals new { course.OrganizationId, course.Id }
            join attendance in _dbContext.Attendance on new { session.OrganizationId, SessionId = session.Id, EnrollmentId = enrollment.Id } equals new { attendance.OrganizationId, attendance.SessionId, attendance.EnrollmentId } into attendances
            from attendance in attendances.DefaultIfEmpty()
            where session.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerId &&
                  enrollment.EnrolledAtUtc <= session.EndUtc &&
                  (enrollment.EndedAtUtc == null || enrollment.EndedAtUtc >= session.StartUtc) &&
                  session.Status != SessionStatus.Cancelled &&
                  session.EndUtc <= dataAsOfUtc &&
                  (!query.ClassId.HasValue || session.ClassId == query.ClassId.Value) &&
                  (!query.FromUtc.HasValue || session.StartUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || session.StartUtc <= query.ToUtc.Value)
            select new AttendanceRow
            {
                SessionId = session.Id,
                ClassId = session.ClassId,
                ClassTitle = academicClass.Title,
                CourseTitle = course.Title,
                SessionTitle = session.Title,
                StartUtc = session.StartUtc,
                EndUtc = session.EndUtc,
                Status = attendance == null ? null : attendance.Status,
                RecordedAtUtc = attendance == null ? null : attendance.RecordedAtUtc
            };
        return rows.AsNoTracking();
    }

    public async Task<ReportPage<StudentAssignmentReportItem>> ReadStudentAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var learnerId = await GetStudentLearnerIdAsync(context, cancellationToken);
        if (!learnerId.HasValue)
        {
            return Page(dataAsOfUtc, query, 0, Array.Empty<StudentAssignmentReportItem>());
        }

        var rows = CreateAssignmentRows(context.OrganizationId!.Value, learnerId.Value, query);
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = OrderAssignments(rows, query.Sort);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new StudentAssignmentReportItem(row.AssignmentId, row.AssignmentVersionId, row.ClassId, row.ClassTitle, row.Title, row.AssignmentStatus, row.DueAtUtc, row.MaximumScore, row.SubmissionStatus, row.SubmittedAtUtc, row.IsLate, row.ReleasedScore, row.LearnerFeedback, row.ReleasedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<ReportPage<ParentAssignmentReportItem>> ReadParentAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var rows = CreateAssignmentRows(context.OrganizationId!.Value, context.SubjectOrganizationPersonId!.Value, query);
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = OrderAssignments(rows, query.Sort);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ParentAssignmentReportItem(row.AssignmentId, row.AssignmentVersionId, row.ClassId, row.ClassTitle, row.Title, row.AssignmentStatus, row.DueAtUtc, row.MaximumScore, row.SubmissionStatus, row.SubmittedAtUtc, row.IsLate, row.ReleasedScore, row.GuardianFeedback, row.ReleasedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    private IQueryable<AssignmentReportRow> CreateAssignmentRows(Guid organizationId, Guid learnerId, ReportQuerySpec query)
    {
        var rows =
            from recipient in _dbContext.AssignmentRecipients
            join enrollment in _dbContext.Enrollments on new { recipient.OrganizationId, Id = recipient.EnrollmentId } equals new { enrollment.OrganizationId, enrollment.Id }
            join assignment in _dbContext.Assignments on new { recipient.OrganizationId, Id = recipient.AssignmentId } equals new { assignment.OrganizationId, assignment.Id }
            join version in _dbContext.AssignmentVersions on new { recipient.OrganizationId, Id = recipient.AssignmentVersionId } equals new { version.OrganizationId, version.Id }
            join academicClass in _dbContext.Classes on new { recipient.OrganizationId, Id = recipient.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            let latestAttempt = _dbContext.SubmissionAttempts.Where(attempt => attempt.OrganizationId == organizationId && attempt.AssignmentRecipientId == recipient.Id).OrderByDescending(attempt => attempt.AttemptNumber).FirstOrDefault()
            let releaseRow = (
                from release in _dbContext.GradeReleases
                join evaluation in _dbContext.EvaluationRevisions on new { release.OrganizationId, Id = release.EvaluationRevisionId } equals new { evaluation.OrganizationId, evaluation.Id }
                join attempt in _dbContext.SubmissionAttempts on new { release.OrganizationId, Id = release.SubmissionAttemptId } equals new { attempt.OrganizationId, attempt.Id }
                where release.OrganizationId == organizationId && attempt.AssignmentRecipientId == recipient.Id && evaluation.Status == EvaluationRevisionStatus.Released
                orderby release.ReleasedAtUtc descending
                select new { evaluation.Score, evaluation.LearnerFeedback, GuardianFeedback = evaluation.GuardianVisibleFeedback, release.ReleasedAtUtc }).FirstOrDefault()
            where recipient.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerId &&
                  (assignment.Status == AssignmentStatus.Published || assignment.Status == AssignmentStatus.Closed) &&
                  version.PublishedAtUtc != null &&
                  (!query.ClassId.HasValue || recipient.ClassId == query.ClassId.Value) &&
                  (!query.FromUtc.HasValue || version.DueAtUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || version.DueAtUtc <= query.ToUtc.Value)
            select new AssignmentReportRow
            {
                AssignmentId = assignment.Id,
                AssignmentVersionId = version.Id,
                ClassId = recipient.ClassId,
                ClassTitle = academicClass.Title,
                Title = version.Title,
                AssignmentStatus = assignment.Status,
                DueAtUtc = version.DueAtUtc,
                MaximumScore = version.MaxScore,
                SubmissionStatus = latestAttempt == null ? null : latestAttempt.Status,
                SubmittedAtUtc = latestAttempt == null ? null : latestAttempt.SubmittedAtUtc,
                IsLate = latestAttempt == null ? null : latestAttempt.IsLate,
                ReleasedScore = releaseRow == null ? null : releaseRow.Score,
                LearnerFeedback = releaseRow == null ? null : releaseRow.LearnerFeedback,
                GuardianFeedback = releaseRow == null ? null : releaseRow.GuardianFeedback,
                ReleasedAtUtc = releaseRow == null ? null : releaseRow.ReleasedAtUtc
            };
        return rows.AsNoTracking();
    }

    private static IOrderedQueryable<AssignmentReportRow> OrderAssignments(IQueryable<AssignmentReportRow> rows, string sort) => sort switch
    {
        "dueasc" => rows.OrderBy(row => row.DueAtUtc).ThenBy(row => row.AssignmentId),
        "titleasc" => rows.OrderBy(row => row.Title).ThenBy(row => row.AssignmentId),
        _ => rows.OrderByDescending(row => row.DueAtUtc).ThenBy(row => row.AssignmentId)
    };

    public async Task<ReportPage<StudentExamReportItem>> ReadStudentExamsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var learnerId = await GetStudentLearnerIdAsync(context, cancellationToken);
        if (!learnerId.HasValue)
        {
            return Page(dataAsOfUtc, query, 0, Array.Empty<StudentExamReportItem>());
        }

        var rows = CreateExamRows(context.OrganizationId!.Value, learnerId.Value, query);
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = OrderExams(rows, query.Sort);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new StudentExamReportItem(row.ExamId, row.ExamVersionId, row.ClassId, row.ClassTitle, row.Title, row.AvailableFromUtc, row.AvailableUntilUtc, row.DurationMinutes, row.MaximumAttempts, row.MaximumScore, row.AttemptCount, row.LatestAttemptStatus, row.LatestStartedAtUtc, row.LatestFinalizedAtUtc, row.ReleasedScore, row.LearnerFeedback, row.ReleasedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<ReportPage<ParentExamReportItem>> ReadParentExamsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var rows = CreateExamRows(context.OrganizationId!.Value, context.SubjectOrganizationPersonId!.Value, query);
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = OrderExams(rows, query.Sort);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ParentExamReportItem(row.ExamId, row.ExamVersionId, row.ClassId, row.ClassTitle, row.Title, row.AvailableFromUtc, row.AvailableUntilUtc, row.DurationMinutes, row.MaximumScore, row.AttemptCount, row.LatestAttemptStatus, row.LatestFinalizedAtUtc, row.ReleasedScore, row.GuardianFeedback, row.ReleasedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    private IQueryable<ExamReportRow> CreateExamRows(Guid organizationId, Guid learnerId, ReportQuerySpec query)
    {
        var rows =
            from exam in _dbContext.Exams
            join version in _dbContext.ExamVersions on new { exam.OrganizationId, ExamId = exam.Id } equals new { version.OrganizationId, version.ExamId }
            join academicClass in _dbContext.Classes on new { exam.OrganizationId, Id = exam.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in _dbContext.Enrollments on new { exam.OrganizationId, Id = exam.ClassId } equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            let latestAttempt = _dbContext.ExamAttempts.Where(attempt => attempt.OrganizationId == organizationId && attempt.ExamVersionId == version.Id && attempt.EnrollmentId == enrollment.Id).OrderByDescending(attempt => attempt.AttemptNumber).FirstOrDefault()
            let attemptCount = _dbContext.ExamAttempts.Count(attempt => attempt.OrganizationId == organizationId && attempt.ExamVersionId == version.Id && attempt.EnrollmentId == enrollment.Id)
            let releaseRow = (
                from release in _dbContext.ExamGradeReleases
                join grade in _dbContext.ExamGradeRevisions on new { release.OrganizationId, Id = release.ExamGradeRevisionId } equals new { grade.OrganizationId, grade.Id }
                join attempt in _dbContext.ExamAttempts on new { release.OrganizationId, Id = release.ExamAttemptId } equals new { attempt.OrganizationId, attempt.Id }
                where release.OrganizationId == organizationId && attempt.ExamVersionId == version.Id && attempt.EnrollmentId == enrollment.Id && grade.Status == ExamGradeRevisionStatus.Released
                orderby release.ReleasedAtUtc descending
                select new { Score = grade.TotalScore, grade.LearnerFeedback, GuardianFeedback = grade.GuardianVisibleFeedback, release.ReleasedAtUtc }).FirstOrDefault()
            where exam.OrganizationId == organizationId &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  enrollment.LearnerOrganizationPersonId == learnerId &&
                  enrollment.EnrolledAtUtc <= version.AvailableUntilUtc &&
                  (enrollment.EndedAtUtc == null || enrollment.EndedAtUtc >= version.AvailableFromUtc) &&
                  (!query.ClassId.HasValue || exam.ClassId == query.ClassId.Value) &&
                  (!query.FromUtc.HasValue || version.AvailableFromUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || version.AvailableFromUtc <= query.ToUtc.Value)
            select new ExamReportRow
            {
                ExamId = exam.Id,
                ExamVersionId = version.Id,
                ClassId = exam.ClassId,
                ClassTitle = academicClass.Title,
                Title = version.Title,
                AvailableFromUtc = version.AvailableFromUtc,
                AvailableUntilUtc = version.AvailableUntilUtc,
                DurationMinutes = version.DurationMinutes,
                MaximumAttempts = version.MaxAttempts,
                MaximumScore = version.MaxScore,
                AttemptCount = attemptCount,
                LatestAttemptStatus = latestAttempt == null ? null : latestAttempt.Status,
                LatestStartedAtUtc = latestAttempt == null ? null : latestAttempt.StartedAtUtc,
                LatestFinalizedAtUtc = latestAttempt == null ? null : latestAttempt.FinalizedAtUtc,
                ReleasedScore = releaseRow == null ? null : releaseRow.Score,
                LearnerFeedback = releaseRow == null ? null : releaseRow.LearnerFeedback,
                GuardianFeedback = releaseRow == null ? null : releaseRow.GuardianFeedback,
                ReleasedAtUtc = releaseRow == null ? null : releaseRow.ReleasedAtUtc
            };
        return rows.AsNoTracking();
    }

    private static IOrderedQueryable<ExamReportRow> OrderExams(IQueryable<ExamReportRow> rows, string sort) => sort switch
    {
        "availableasc" => rows.OrderBy(row => row.AvailableFromUtc).ThenBy(row => row.ExamId),
        "titleasc" => rows.OrderBy(row => row.Title).ThenBy(row => row.ExamId),
        _ => rows.OrderByDescending(row => row.AvailableFromUtc).ThenBy(row => row.ExamId)
    };

    public async Task<ReportPage<StudentReleasedGradeReportItem>> ReadStudentGradesAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var learnerId = await GetStudentLearnerIdAsync(context, cancellationToken);
        if (!learnerId.HasValue)
        {
            return Page(dataAsOfUtc, query, 0, Array.Empty<StudentReleasedGradeReportItem>());
        }

        var organizationId = context.OrganizationId!.Value;
        var assignmentGrades =
            from release in _dbContext.GradeReleases
            join evaluation in _dbContext.EvaluationRevisions on new { release.OrganizationId, Id = release.EvaluationRevisionId } equals new { evaluation.OrganizationId, evaluation.Id }
            join attempt in _dbContext.SubmissionAttempts on new { release.OrganizationId, Id = release.SubmissionAttemptId } equals new { attempt.OrganizationId, attempt.Id }
            join enrollment in _dbContext.Enrollments on new { attempt.OrganizationId, Id = attempt.EnrollmentId } equals new { enrollment.OrganizationId, enrollment.Id }
            join version in _dbContext.AssignmentVersions on new { attempt.OrganizationId, Id = attempt.AssignmentVersionId } equals new { version.OrganizationId, version.Id }
            join academicClass in _dbContext.Classes on new { attempt.OrganizationId, Id = version.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            where release.OrganizationId == organizationId && enrollment.LearnerOrganizationPersonId == learnerId && evaluation.Status == EvaluationRevisionStatus.Released && (!query.ClassId.HasValue || version.ClassId == query.ClassId.Value) && (!query.FromUtc.HasValue || release.ReleasedAtUtc >= query.FromUtc.Value) && (!query.ToUtc.HasValue || release.ReleasedAtUtc <= query.ToUtc.Value)
            select new GradeRow { Kind = DetailedReportGradeKind.Assignment, AssessmentId = version.AssignmentId, ClassId = version.ClassId, ClassTitle = academicClass.Title, Title = version.Title, Score = evaluation.Score, MaximumScore = version.MaxScore, ReleasedAtUtc = release.ReleasedAtUtc };

        var examGrades =
            from release in _dbContext.ExamGradeReleases
            join grade in _dbContext.ExamGradeRevisions on new { release.OrganizationId, Id = release.ExamGradeRevisionId } equals new { grade.OrganizationId, grade.Id }
            join attempt in _dbContext.ExamAttempts on new { release.OrganizationId, Id = release.ExamAttemptId } equals new { attempt.OrganizationId, attempt.Id }
            join enrollment in _dbContext.Enrollments on new { attempt.OrganizationId, Id = attempt.EnrollmentId } equals new { enrollment.OrganizationId, enrollment.Id }
            join version in _dbContext.ExamVersions on new { attempt.OrganizationId, Id = attempt.ExamVersionId } equals new { version.OrganizationId, version.Id }
            join academicClass in _dbContext.Classes on new { attempt.OrganizationId, Id = attempt.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            where release.OrganizationId == organizationId && enrollment.LearnerOrganizationPersonId == learnerId && grade.Status == ExamGradeRevisionStatus.Released && (!query.ClassId.HasValue || attempt.ClassId == query.ClassId.Value) && (!query.FromUtc.HasValue || release.ReleasedAtUtc >= query.FromUtc.Value) && (!query.ToUtc.HasValue || release.ReleasedAtUtc <= query.ToUtc.Value)
            select new GradeRow { Kind = DetailedReportGradeKind.Exam, AssessmentId = attempt.ExamId, ClassId = attempt.ClassId, ClassTitle = academicClass.Title, Title = version.Title, Score = grade.TotalScore, MaximumScore = version.MaxScore, ReleasedAtUtc = release.ReleasedAtUtc };

        var rows = assignmentGrades.Concat(examGrades);
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort == "releasedasc"
            ? rows.OrderBy(row => row.ReleasedAtUtc).ThenBy(row => row.AssessmentId)
            : rows.OrderByDescending(row => row.ReleasedAtUtc).ThenBy(row => row.AssessmentId);
        var items = await ordered.Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new StudentReleasedGradeReportItem(row.Kind, row.AssessmentId, row.ClassId, row.ClassTitle, row.Title, row.Score, row.MaximumScore, row.Score * 100m / row.MaximumScore, row.ReleasedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    private sealed class AttendanceRow
    {
        public Guid SessionId { get; init; }
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
        public string SessionTitle { get; init; } = string.Empty;
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public AttendanceStatus? Status { get; init; }
        public DateTime? RecordedAtUtc { get; init; }
    }

    private sealed class AssignmentReportRow
    {
        public Guid AssignmentId { get; init; }
        public Guid AssignmentVersionId { get; init; }
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public AssignmentStatus AssignmentStatus { get; init; }
        public DateTime DueAtUtc { get; init; }
        public decimal MaximumScore { get; init; }
        public SubmissionAttemptStatus? SubmissionStatus { get; init; }
        public DateTime? SubmittedAtUtc { get; init; }
        public bool? IsLate { get; init; }
        public decimal? ReleasedScore { get; init; }
        public string? LearnerFeedback { get; init; }
        public string? GuardianFeedback { get; init; }
        public DateTime? ReleasedAtUtc { get; init; }
    }

    private sealed class ExamReportRow
    {
        public Guid ExamId { get; init; }
        public Guid ExamVersionId { get; init; }
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime AvailableFromUtc { get; init; }
        public DateTime AvailableUntilUtc { get; init; }
        public int DurationMinutes { get; init; }
        public int MaximumAttempts { get; init; }
        public decimal MaximumScore { get; init; }
        public int AttemptCount { get; init; }
        public ExamAttemptStatus? LatestAttemptStatus { get; init; }
        public DateTime? LatestStartedAtUtc { get; init; }
        public DateTime? LatestFinalizedAtUtc { get; init; }
        public decimal? ReleasedScore { get; init; }
        public string? LearnerFeedback { get; init; }
        public string? GuardianFeedback { get; init; }
        public DateTime? ReleasedAtUtc { get; init; }
    }

    private sealed class GradeRow
    {
        public DetailedReportGradeKind Kind { get; init; }
        public Guid AssessmentId { get; init; }
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public decimal Score { get; init; }
        public decimal MaximumScore { get; init; }
        public DateTime ReleasedAtUtc { get; init; }
    }
}
