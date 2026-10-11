using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Detailed;

public sealed partial class EfDetailedReportReadService
{
    public Task<bool> HasActiveTeacherAssignmentAsync(AccessContext context, Guid classId, CancellationToken cancellationToken) =>
        _dbContext.TeacherAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.OrganizationId == context.OrganizationId!.Value &&
            assignment.ClassId == classId &&
            assignment.TeacherMembershipId == context.MembershipId!.Value &&
            assignment.Status == TeacherAssignmentStatus.Active &&
            assignment.EndedAtUtc == null,
            cancellationToken);

    public async Task<AttendanceReportPage<TeacherAttendanceReportItem>> ReadTeacherAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var rows =
            from session in _dbContext.AcademicSessions
            join enrollment in _dbContext.Enrollments on new { session.OrganizationId, Id = session.ClassId } equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            join organizationPerson in _dbContext.OrganizationPersons on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId } equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in _dbContext.Persons on organizationPerson.PersonId equals person.Id
            join attendance in _dbContext.Attendance on new { session.OrganizationId, SessionId = session.Id, EnrollmentId = enrollment.Id } equals new { attendance.OrganizationId, attendance.SessionId, attendance.EnrollmentId } into attendances
            from attendance in attendances.DefaultIfEmpty()
            where session.OrganizationId == organizationId &&
                  session.ClassId == classId &&
                  session.Status != SessionStatus.Cancelled &&
                  session.EndUtc <= dataAsOfUtc &&
                  enrollment.EnrolledAtUtc <= session.EndUtc &&
                  (enrollment.EndedAtUtc == null || enrollment.EndedAtUtc >= session.StartUtc) &&
                  (!query.FromUtc.HasValue || session.StartUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || session.StartUtc <= query.ToUtc.Value)
            select new TeacherAttendanceRow
            {
                SessionId = session.Id,
                SessionTitle = session.Title,
                StartUtc = session.StartUtc,
                EndUtc = session.EndUtc,
                EnrollmentId = enrollment.Id,
                LearnerId = organizationPerson.Id,
                LearnerDisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName,
                Status = attendance == null ? AttendanceStatus.NotRecorded : attendance.Status,
                RecordedAtUtc = attendance == null ? null : attendance.RecordedAtUtc
            };

        var summary = await rows.GroupBy(_ => 1).Select(group => new AttendanceReportSummary(
            group.Count(),
            group.Count(row => row.Status != AttendanceStatus.NotRecorded),
            group.Count(row => row.Status == AttendanceStatus.Present),
            group.Count(row => row.Status == AttendanceStatus.Absent),
            group.Count(row => row.Status == AttendanceStatus.Late),
            group.Count(row => row.Status == AttendanceStatus.Excused),
            group.Count(row => row.Status == AttendanceStatus.NotRecorded)))
            .SingleOrDefaultAsync(cancellationToken) ?? EmptyAttendanceSummary;
        var ordered = query.Sort == "occurredasc"
            ? rows.OrderBy(row => row.StartUtc).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId)
            : rows.OrderByDescending(row => row.StartUtc).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId);
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new TeacherAttendanceReportItem(row.SessionId, row.SessionTitle, row.StartUtc, row.EndUtc, row.EnrollmentId, row.LearnerId, row.LearnerDisplayName, row.Status, row.RecordedAtUtc))
            .ToArrayAsync(cancellationToken);
        return AttendancePage(dataAsOfUtc, query, summary, items);
    }

    public async Task<ReportPage<TeacherAssignmentReportItem>> ReadTeacherAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var rows =
            from recipient in _dbContext.AssignmentRecipients
            join enrollment in _dbContext.Enrollments on new { recipient.OrganizationId, Id = recipient.EnrollmentId } equals new { enrollment.OrganizationId, enrollment.Id }
            join organizationPerson in _dbContext.OrganizationPersons on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId } equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in _dbContext.Persons on organizationPerson.PersonId equals person.Id
            join assignment in _dbContext.Assignments on new { recipient.OrganizationId, Id = recipient.AssignmentId } equals new { assignment.OrganizationId, assignment.Id }
            join version in _dbContext.AssignmentVersions on new { recipient.OrganizationId, Id = recipient.AssignmentVersionId } equals new { version.OrganizationId, version.Id }
            let latestAttempt = _dbContext.SubmissionAttempts.Where(attempt => attempt.OrganizationId == organizationId && attempt.AssignmentRecipientId == recipient.Id).OrderByDescending(attempt => attempt.AttemptNumber).FirstOrDefault()
            let latestEvaluation = latestAttempt == null ? null : _dbContext.EvaluationRevisions.Where(evaluation => evaluation.OrganizationId == organizationId && evaluation.SubmissionAttemptId == latestAttempt.Id).OrderByDescending(evaluation => evaluation.RevisionNumber).FirstOrDefault()
            where recipient.OrganizationId == organizationId &&
                  recipient.ClassId == classId &&
                  (assignment.Status == AssignmentStatus.Published || assignment.Status == AssignmentStatus.Closed) &&
                  version.PublishedAtUtc != null &&
                  (!query.FromUtc.HasValue || version.DueAtUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || version.DueAtUtc <= query.ToUtc.Value)
            select new TeacherAssignmentRow
            {
                AssignmentId = assignment.Id,
                VersionId = version.Id,
                Title = version.Title,
                DueAtUtc = version.DueAtUtc,
                MaximumScore = version.MaxScore,
                EnrollmentId = enrollment.Id,
                LearnerId = organizationPerson.Id,
                LearnerDisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName,
                AttemptId = latestAttempt == null ? null : latestAttempt.Id,
                SubmissionStatus = latestAttempt == null ? null : latestAttempt.Status,
                SubmittedAtUtc = latestAttempt == null ? null : latestAttempt.SubmittedAtUtc,
                IsLate = latestAttempt == null ? null : latestAttempt.IsLate,
                CurrentScore = latestEvaluation == null ? null : latestEvaluation.Score,
                EvaluationStatus = latestEvaluation == null ? null : latestEvaluation.Status,
                IsReleased = latestEvaluation != null && latestEvaluation.Status == EvaluationRevisionStatus.Released && _dbContext.GradeReleases.Any(release => release.OrganizationId == organizationId && release.EvaluationRevisionId == latestEvaluation.Id)
            };

        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "dueasc" => rows.OrderBy(row => row.DueAtUtc).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId),
            "titleasc" => rows.OrderBy(row => row.Title).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId),
            _ => rows.OrderByDescending(row => row.DueAtUtc).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId)
        };
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new TeacherAssignmentReportItem(row.AssignmentId, row.VersionId, row.Title, row.DueAtUtc, row.MaximumScore, row.EnrollmentId, row.LearnerId, row.LearnerDisplayName, row.AttemptId, row.SubmissionStatus, row.SubmittedAtUtc, row.IsLate, row.CurrentScore, row.EvaluationStatus, row.IsReleased))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<ReportPage<TeacherExamReportItem>> ReadTeacherExamsAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var rows =
            from exam in _dbContext.Exams
            join version in _dbContext.ExamVersions on new { exam.OrganizationId, ExamId = exam.Id } equals new { version.OrganizationId, version.ExamId }
            join enrollment in _dbContext.Enrollments on new { exam.OrganizationId, Id = exam.ClassId } equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            join organizationPerson in _dbContext.OrganizationPersons on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId } equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in _dbContext.Persons on organizationPerson.PersonId equals person.Id
            let latestAttempt = _dbContext.ExamAttempts.Where(attempt => attempt.OrganizationId == organizationId && attempt.ExamVersionId == version.Id && attempt.EnrollmentId == enrollment.Id).OrderByDescending(attempt => attempt.AttemptNumber).FirstOrDefault()
            let latestGrade = latestAttempt == null ? null : _dbContext.ExamGradeRevisions.Where(grade => grade.OrganizationId == organizationId && grade.ExamAttemptId == latestAttempt.Id).OrderByDescending(grade => grade.RevisionNumber).FirstOrDefault()
            where exam.OrganizationId == organizationId &&
                  exam.ClassId == classId &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  enrollment.EnrolledAtUtc <= version.AvailableUntilUtc &&
                  (enrollment.EndedAtUtc == null || enrollment.EndedAtUtc >= version.AvailableFromUtc) &&
                  (!query.FromUtc.HasValue || version.AvailableFromUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || version.AvailableFromUtc <= query.ToUtc.Value)
            select new TeacherExamRow
            {
                ExamId = exam.Id,
                VersionId = version.Id,
                Title = version.Title,
                AvailableUntilUtc = version.AvailableUntilUtc,
                MaximumScore = version.MaxScore,
                EnrollmentId = enrollment.Id,
                LearnerId = organizationPerson.Id,
                LearnerDisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName,
                AttemptId = latestAttempt == null ? null : latestAttempt.Id,
                AttemptStatus = latestAttempt == null ? null : latestAttempt.Status,
                FinalizedAtUtc = latestAttempt == null ? null : latestAttempt.FinalizedAtUtc,
                CurrentScore = latestGrade == null ? null : latestGrade.TotalScore,
                GradeStatus = latestGrade == null ? null : latestGrade.Status,
                IsReleased = latestGrade != null && latestGrade.Status == ExamGradeRevisionStatus.Released && _dbContext.ExamGradeReleases.Any(release => release.OrganizationId == organizationId && release.ExamGradeRevisionId == latestGrade.Id)
            };

        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "availableasc" => rows.OrderBy(row => row.AvailableUntilUtc).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId),
            "titleasc" => rows.OrderBy(row => row.Title).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId),
            _ => rows.OrderByDescending(row => row.AvailableUntilUtc).ThenBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId)
        };
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new TeacherExamReportItem(row.ExamId, row.VersionId, row.Title, row.AvailableUntilUtc, row.MaximumScore, row.EnrollmentId, row.LearnerId, row.LearnerDisplayName, row.AttemptId, row.AttemptStatus, row.FinalizedAtUtc, row.CurrentScore, row.GradeStatus, row.IsReleased))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<TeacherLearnerOverviewDto?> ReadTeacherLearnerAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, Guid learnerId, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var learner = await (
            from enrollment in _dbContext.Enrollments
            join academicClass in _dbContext.Classes on new { enrollment.OrganizationId, Id = enrollment.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join organizationPerson in _dbContext.OrganizationPersons on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId } equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in _dbContext.Persons on organizationPerson.PersonId equals person.Id
            where enrollment.OrganizationId == organizationId && enrollment.ClassId == classId && organizationPerson.Id == learnerId
            select new { EnrollmentId = enrollment.Id, ClassTitle = academicClass.Title, LearnerId = organizationPerson.Id, DisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName })
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (learner is null)
        {
            return null;
        }

        var attendance = await (
            from session in _dbContext.AcademicSessions
            join record in _dbContext.Attendance.Where(record => record.EnrollmentId == learner.EnrollmentId) on session.Id equals record.SessionId into records
            from record in records.DefaultIfEmpty()
            where session.OrganizationId == organizationId && session.ClassId == classId && session.Status != SessionStatus.Cancelled && session.EndUtc <= dataAsOfUtc
            group record by 1 into groupRows
            select new
            {
                Eligible = groupRows.Count(),
                Present = groupRows.Count(record => record != null && record.Status == AttendanceStatus.Present),
                Absent = groupRows.Count(record => record != null && record.Status == AttendanceStatus.Absent),
                Late = groupRows.Count(record => record != null && record.Status == AttendanceStatus.Late),
                Excused = groupRows.Count(record => record != null && record.Status == AttendanceStatus.Excused),
                Recorded = groupRows.Count(record => record != null)
            }).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        var assignmentCount = await _dbContext.AssignmentRecipients.AsNoTracking().CountAsync(recipient => recipient.OrganizationId == organizationId && recipient.ClassId == classId && recipient.EnrollmentId == learner.EnrollmentId, cancellationToken);
        var submittedAssignmentCount = await _dbContext.SubmissionAttempts.AsNoTracking().Where(attempt => attempt.OrganizationId == organizationId && attempt.EnrollmentId == learner.EnrollmentId && attempt.Status == SubmissionAttemptStatus.Submitted).Select(attempt => attempt.AssignmentId).Distinct().CountAsync(cancellationToken);
        var releasedAssignmentCount = await (from release in _dbContext.GradeReleases join attempt in _dbContext.SubmissionAttempts on new { release.OrganizationId, release.SubmissionAttemptId } equals new { attempt.OrganizationId, SubmissionAttemptId = attempt.Id } where release.OrganizationId == organizationId && attempt.EnrollmentId == learner.EnrollmentId select attempt.AssignmentId).Distinct().CountAsync(cancellationToken);
        var examCount = await (from exam in _dbContext.Exams join version in _dbContext.ExamVersions on new { exam.OrganizationId, ExamId = exam.Id } equals new { version.OrganizationId, version.ExamId } where exam.OrganizationId == organizationId && exam.ClassId == classId && version.Status == ExamVersionStatus.Published select version.Id).CountAsync(cancellationToken);
        var finalizedExamCount = await _dbContext.ExamAttempts.AsNoTracking().CountAsync(attempt => attempt.OrganizationId == organizationId && attempt.ClassId == classId && attempt.EnrollmentId == learner.EnrollmentId && attempt.Status == ExamAttemptStatus.Finalized, cancellationToken);
        var releasedExamCount = await (from release in _dbContext.ExamGradeReleases join attempt in _dbContext.ExamAttempts on new { release.OrganizationId, release.ExamAttemptId } equals new { attempt.OrganizationId, ExamAttemptId = attempt.Id } where release.OrganizationId == organizationId && attempt.ClassId == classId && attempt.EnrollmentId == learner.EnrollmentId select release.Id).CountAsync(cancellationToken);

        return new TeacherLearnerOverviewDto(dataAsOfUtc, classId, learner.ClassTitle, learner.EnrollmentId, learner.LearnerId, learner.DisplayName, attendance?.Eligible ?? 0, attendance?.Present ?? 0, attendance?.Absent ?? 0, attendance?.Late ?? 0, attendance?.Excused ?? 0, Math.Max(0, (attendance?.Eligible ?? 0) - (attendance?.Recorded ?? 0)), assignmentCount, submittedAssignmentCount, releasedAssignmentCount, examCount, finalizedExamCount, releasedExamCount);
    }

    private sealed class TeacherAttendanceRow
    {
        public Guid SessionId { get; init; }
        public string SessionTitle { get; init; } = string.Empty;
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public Guid EnrollmentId { get; init; }
        public Guid LearnerId { get; init; }
        public string LearnerDisplayName { get; init; } = string.Empty;
        public AttendanceStatus Status { get; init; }
        public DateTime? RecordedAtUtc { get; init; }
    }

    private sealed class TeacherAssignmentRow
    {
        public Guid AssignmentId { get; init; }
        public Guid VersionId { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime DueAtUtc { get; init; }
        public decimal MaximumScore { get; init; }
        public Guid EnrollmentId { get; init; }
        public Guid LearnerId { get; init; }
        public string LearnerDisplayName { get; init; } = string.Empty;
        public Guid? AttemptId { get; init; }
        public SubmissionAttemptStatus? SubmissionStatus { get; init; }
        public DateTime? SubmittedAtUtc { get; init; }
        public bool? IsLate { get; init; }
        public decimal? CurrentScore { get; init; }
        public EvaluationRevisionStatus? EvaluationStatus { get; init; }
        public bool IsReleased { get; init; }
    }

    private sealed class TeacherExamRow
    {
        public Guid ExamId { get; init; }
        public Guid VersionId { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime AvailableUntilUtc { get; init; }
        public decimal MaximumScore { get; init; }
        public Guid EnrollmentId { get; init; }
        public Guid LearnerId { get; init; }
        public string LearnerDisplayName { get; init; } = string.Empty;
        public Guid? AttemptId { get; init; }
        public ExamAttemptStatus? AttemptStatus { get; init; }
        public DateTime? FinalizedAtUtc { get; init; }
        public decimal? CurrentScore { get; init; }
        public ExamGradeRevisionStatus? GradeStatus { get; init; }
        public bool IsReleased { get; init; }
    }
}
