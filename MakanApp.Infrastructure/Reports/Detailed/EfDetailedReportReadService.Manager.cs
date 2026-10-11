using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Detailed;

public sealed partial class EfDetailedReportReadService
{
    public async Task<ReportPage<ManagerEnrollmentReportItem>> ReadManagerEnrollmentsAsync(AccessContext context, DateTime dataAsOfUtc, ManagerEnrollmentReportQuery filters, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var rows =
            from enrollment in _dbContext.Enrollments
            join organizationPerson in _dbContext.OrganizationPersons on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId } equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in _dbContext.Persons on organizationPerson.PersonId equals person.Id
            join academicClass in _dbContext.Classes on new { enrollment.OrganizationId, Id = enrollment.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join course in _dbContext.Courses on new { academicClass.OrganizationId, Id = academicClass.CourseId } equals new { course.OrganizationId, course.Id }
            join period in _dbContext.AcademicPeriods on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId } equals new { period.OrganizationId, period.Id }
            where enrollment.OrganizationId == organizationId &&
                  (!filters.ClassId.HasValue || enrollment.ClassId == filters.ClassId.Value) &&
                  (!filters.CourseId.HasValue || course.Id == filters.CourseId.Value) &&
                  (!filters.AcademicPeriodId.HasValue || period.Id == filters.AcademicPeriodId.Value) &&
                  (!filters.Status.HasValue || enrollment.Status == filters.Status.Value) &&
                  (!query.FromUtc.HasValue || enrollment.EnrolledAtUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || enrollment.EnrolledAtUtc <= query.ToUtc.Value)
            select new ManagerEnrollmentRow
            {
                EnrollmentId = enrollment.Id,
                LearnerId = organizationPerson.Id,
                LearnerDisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName,
                ClassId = academicClass.Id,
                ClassTitle = academicClass.Title,
                CourseId = course.Id,
                CourseTitle = course.Title,
                PeriodId = period.Id,
                PeriodTitle = period.Title,
                Status = enrollment.Status,
                EnrolledAtUtc = enrollment.EnrolledAtUtc,
                EndedAtUtc = enrollment.EndedAtUtc
            };
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "enrolledasc" => rows.OrderBy(row => row.EnrolledAtUtc).ThenBy(row => row.EnrollmentId),
            "learnerasc" => rows.OrderBy(row => row.LearnerDisplayName).ThenBy(row => row.EnrollmentId),
            _ => rows.OrderByDescending(row => row.EnrolledAtUtc).ThenBy(row => row.EnrollmentId)
        };
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ManagerEnrollmentReportItem(row.EnrollmentId, row.LearnerId, row.LearnerDisplayName, row.ClassId, row.ClassTitle, row.CourseId, row.CourseTitle, row.PeriodId, row.PeriodTitle, row.Status, row.EnrolledAtUtc, row.EndedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<ReportPage<ManagerAttendanceReportItem>> ReadManagerAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var eligible =
            from session in _dbContext.AcademicSessions
            join enrollment in _dbContext.Enrollments on new { session.OrganizationId, Id = session.ClassId } equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            join academicClass in _dbContext.Classes on new { session.OrganizationId, Id = session.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join course in _dbContext.Courses on new { academicClass.OrganizationId, Id = academicClass.CourseId } equals new { course.OrganizationId, course.Id }
            join attendance in _dbContext.Attendance on new { session.OrganizationId, SessionId = session.Id, EnrollmentId = enrollment.Id } equals new { attendance.OrganizationId, attendance.SessionId, attendance.EnrollmentId } into attendances
            from attendance in attendances.DefaultIfEmpty()
            where session.OrganizationId == organizationId && session.Status != SessionStatus.Cancelled && session.EndUtc <= dataAsOfUtc && enrollment.EnrolledAtUtc <= session.EndUtc && (enrollment.EndedAtUtc == null || enrollment.EndedAtUtc >= session.StartUtc) && (!query.ClassId.HasValue || session.ClassId == query.ClassId.Value) && (!query.FromUtc.HasValue || session.StartUtc >= query.FromUtc.Value) && (!query.ToUtc.HasValue || session.StartUtc <= query.ToUtc.Value)
            select new { ClassId = academicClass.Id, ClassTitle = academicClass.Title, CourseTitle = course.Title, Status = attendance == null ? (AttendanceStatus?)null : attendance.Status };
        var rows = eligible.GroupBy(row => new { row.ClassId, row.ClassTitle, row.CourseTitle }).Select(group => new ManagerAttendanceRow
        {
            ClassId = group.Key.ClassId,
            ClassTitle = group.Key.ClassTitle,
            CourseTitle = group.Key.CourseTitle,
            EligibleCount = group.Count(),
            RecordedCount = group.Count(row => row.Status != null),
            PresentCount = group.Count(row => row.Status == AttendanceStatus.Present),
            AbsentCount = group.Count(row => row.Status == AttendanceStatus.Absent),
            LateCount = group.Count(row => row.Status == AttendanceStatus.Late),
            ExcusedCount = group.Count(row => row.Status == AttendanceStatus.Excused),
            NotRecordedCount = group.Count(row => row.Status == null),
            AbsencePercentage = group.Count(row => row.Status != null) == 0 ? null : (decimal?)group.Count(row => row.Status == AttendanceStatus.Absent) * 100m / group.Count(row => row.Status != null)
        });
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "eligibledesc" => rows.OrderByDescending(row => row.EligibleCount).ThenBy(row => row.ClassTitle).ThenBy(row => row.ClassId),
            "absenceratedesc" => rows.OrderByDescending(row => row.AbsencePercentage).ThenBy(row => row.ClassTitle).ThenBy(row => row.ClassId),
            _ => rows.OrderBy(row => row.ClassTitle).ThenBy(row => row.ClassId)
        };
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ManagerAttendanceReportItem(row.ClassId, row.ClassTitle, row.CourseTitle, row.EligibleCount, row.RecordedCount, row.PresentCount, row.AbsentCount, row.LateCount, row.ExcusedCount, row.NotRecordedCount, row.AbsencePercentage))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<ReportPage<ManagerAssessmentReportItem>> ReadManagerAssessmentsAsync(AccessContext context, DateTime dataAsOfUtc, ManagerAssessmentReportQuery filters, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var assignments =
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions on new { assignment.OrganizationId, AssignmentId = assignment.Id, VersionNumber = assignment.CurrentVersionNumber } equals new { version.OrganizationId, version.AssignmentId, version.VersionNumber }
            join academicClass in _dbContext.Classes on new { assignment.OrganizationId, Id = assignment.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join course in _dbContext.Courses on new { academicClass.OrganizationId, Id = academicClass.CourseId } equals new { course.OrganizationId, course.Id }
            where assignment.OrganizationId == organizationId && (assignment.Status == AssignmentStatus.Published || assignment.Status == AssignmentStatus.Closed) && version.PublishedAtUtc != null && (!filters.ClassId.HasValue || academicClass.Id == filters.ClassId.Value) && (!filters.CourseId.HasValue || course.Id == filters.CourseId.Value) && (!filters.AcademicPeriodId.HasValue || academicClass.AcademicPeriodId == filters.AcademicPeriodId.Value) && (!query.FromUtc.HasValue || version.DueAtUtc >= query.FromUtc.Value) && (!query.ToUtc.HasValue || version.DueAtUtc <= query.ToUtc.Value)
            let recipients = _dbContext.AssignmentRecipients.Count(recipient => recipient.OrganizationId == organizationId && recipient.AssignmentVersionId == version.Id)
            let submitted = _dbContext.SubmissionAttempts.Where(attempt => attempt.OrganizationId == organizationId && attempt.AssignmentVersionId == version.Id && attempt.Status == SubmissionAttemptStatus.Submitted).Select(attempt => attempt.EnrollmentId).Distinct().Count()
            let released = (from release in _dbContext.GradeReleases join attempt in _dbContext.SubmissionAttempts on new { release.OrganizationId, release.SubmissionAttemptId } equals new { attempt.OrganizationId, SubmissionAttemptId = attempt.Id } where release.OrganizationId == organizationId && attempt.AssignmentVersionId == version.Id select attempt.EnrollmentId).Distinct().Count()
            select new ManagerAssessmentRow { Kind = DetailedReportAssessmentKind.Assignment, AssessmentId = assignment.Id, VersionId = version.Id, ClassId = academicClass.Id, ClassTitle = academicClass.Title, CourseTitle = course.Title, Title = version.Title, DeadlineUtc = version.DueAtUtc, MaximumScore = version.MaxScore, RecipientCount = recipients, CompletedCount = submitted, ReleasedCount = released, PendingCount = submitted - released };

        var exams =
            from exam in _dbContext.Exams
            join version in _dbContext.ExamVersions on new { exam.OrganizationId, ExamId = exam.Id } equals new { version.OrganizationId, version.ExamId }
            join academicClass in _dbContext.Classes on new { exam.OrganizationId, Id = exam.ClassId } equals new { academicClass.OrganizationId, academicClass.Id }
            join course in _dbContext.Courses on new { academicClass.OrganizationId, Id = academicClass.CourseId } equals new { course.OrganizationId, course.Id }
            where exam.OrganizationId == organizationId && exam.Status == ExamStatus.Published && version.Status == ExamVersionStatus.Published && (!filters.ClassId.HasValue || academicClass.Id == filters.ClassId.Value) && (!filters.CourseId.HasValue || course.Id == filters.CourseId.Value) && (!filters.AcademicPeriodId.HasValue || academicClass.AcademicPeriodId == filters.AcademicPeriodId.Value) && (!query.FromUtc.HasValue || version.AvailableUntilUtc >= query.FromUtc.Value) && (!query.ToUtc.HasValue || version.AvailableUntilUtc <= query.ToUtc.Value)
            let recipients = _dbContext.Enrollments.Count(enrollment => enrollment.OrganizationId == organizationId && enrollment.ClassId == exam.ClassId && enrollment.EnrolledAtUtc <= version.AvailableUntilUtc && (enrollment.EndedAtUtc == null || enrollment.EndedAtUtc >= version.AvailableFromUtc))
            let finalized = _dbContext.ExamAttempts.Where(attempt => attempt.OrganizationId == organizationId && attempt.ExamVersionId == version.Id && attempt.Status == ExamAttemptStatus.Finalized).Select(attempt => attempt.EnrollmentId).Distinct().Count()
            let released = (from release in _dbContext.ExamGradeReleases join attempt in _dbContext.ExamAttempts on new { release.OrganizationId, release.ExamAttemptId } equals new { attempt.OrganizationId, ExamAttemptId = attempt.Id } where release.OrganizationId == organizationId && attempt.ExamVersionId == version.Id select attempt.EnrollmentId).Distinct().Count()
            select new ManagerAssessmentRow { Kind = DetailedReportAssessmentKind.Exam, AssessmentId = exam.Id, VersionId = version.Id, ClassId = academicClass.Id, ClassTitle = academicClass.Title, CourseTitle = course.Title, Title = version.Title, DeadlineUtc = version.AvailableUntilUtc, MaximumScore = version.MaxScore, RecipientCount = recipients, CompletedCount = finalized, ReleasedCount = released, PendingCount = finalized - released };

        IQueryable<ManagerAssessmentRow> rows = filters.Kind switch
        {
            DetailedReportAssessmentKind.Assignment => assignments,
            DetailedReportAssessmentKind.Exam => exams,
            _ => assignments.Concat(exams)
        };
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "deadlineasc" => rows.OrderBy(row => row.DeadlineUtc).ThenBy(row => row.AssessmentId),
            "titleasc" => rows.OrderBy(row => row.Title).ThenBy(row => row.AssessmentId),
            "pendingdesc" => rows.OrderByDescending(row => row.PendingCount).ThenBy(row => row.Title).ThenBy(row => row.AssessmentId),
            _ => rows.OrderByDescending(row => row.DeadlineUtc).ThenBy(row => row.AssessmentId)
        };
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ManagerAssessmentReportItem(row.Kind, row.AssessmentId, row.VersionId, row.ClassId, row.ClassTitle, row.CourseTitle, row.Title, row.DeadlineUtc, row.MaximumScore, row.RecipientCount, row.CompletedCount, row.ReleasedCount, row.PendingCount))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    public async Task<ManagerClassPerformanceDto?> ReadManagerClassAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var classRow = await (from academicClass in _dbContext.Classes join course in _dbContext.Courses on new { academicClass.OrganizationId, Id = academicClass.CourseId } equals new { course.OrganizationId, course.Id } where academicClass.OrganizationId == organizationId && academicClass.Id == classId select new { academicClass.Id, ClassTitle = academicClass.Title, CourseTitle = course.Title }).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (classRow is null)
        {
            return null;
        }

        var activeLearners = await _dbContext.Enrollments.AsNoTracking().CountAsync(enrollment => enrollment.OrganizationId == organizationId && enrollment.ClassId == classId && enrollment.Status == EnrollmentStatus.Active && enrollment.EndedAtUtc == null, cancellationToken);
        var assignmentPercentages = from release in _dbContext.GradeReleases join evaluation in _dbContext.EvaluationRevisions on new { release.OrganizationId, Id = release.EvaluationRevisionId } equals new { evaluation.OrganizationId, evaluation.Id } join attempt in _dbContext.SubmissionAttempts on new { release.OrganizationId, Id = release.SubmissionAttemptId } equals new { attempt.OrganizationId, attempt.Id } join version in _dbContext.AssignmentVersions on new { attempt.OrganizationId, Id = attempt.AssignmentVersionId } equals new { version.OrganizationId, version.Id } where release.OrganizationId == organizationId && version.ClassId == classId && evaluation.Status == EvaluationRevisionStatus.Released select evaluation.Score * 100m / version.MaxScore;
        var assignmentCount = await assignmentPercentages.CountAsync(cancellationToken);
        decimal? assignmentAverage = assignmentCount == 0 ? null : await assignmentPercentages.AverageAsync(cancellationToken);
        var examPercentages = from release in _dbContext.ExamGradeReleases join grade in _dbContext.ExamGradeRevisions on new { release.OrganizationId, Id = release.ExamGradeRevisionId } equals new { grade.OrganizationId, grade.Id } join attempt in _dbContext.ExamAttempts on new { release.OrganizationId, Id = release.ExamAttemptId } equals new { attempt.OrganizationId, attempt.Id } join version in _dbContext.ExamVersions on new { attempt.OrganizationId, Id = attempt.ExamVersionId } equals new { version.OrganizationId, version.Id } where release.OrganizationId == organizationId && attempt.ClassId == classId && grade.Status == ExamGradeRevisionStatus.Released select grade.TotalScore * 100m / version.MaxScore;
        var examCount = await examPercentages.CountAsync(cancellationToken);
        decimal? examAverage = examCount == 0 ? null : await examPercentages.AverageAsync(cancellationToken);
        return new ManagerClassPerformanceDto(dataAsOfUtc, classRow.Id, classRow.ClassTitle, classRow.CourseTitle, activeLearners, assignmentCount, assignmentAverage, examCount, examAverage);
    }

    public async Task<ReportPage<ManagerTeacherWorkloadReportItem>> ReadManagerTeacherWorkloadAsync(AccessContext context, DateTime dataAsOfUtc, TeacherWorkloadReportQuery filters, ReportQuerySpec query, CancellationToken cancellationToken)
    {
        var organizationId = context.OrganizationId!.Value;
        var rows =
            from membership in _dbContext.Memberships
            join user in _dbContext.Users on membership.UserId equals user.Id
            join person in _dbContext.Persons on user.PersonId equals person.Id
            where membership.OrganizationId == organizationId && membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null && _dbContext.RoleAssignments.Any(role => role.MembershipId == membership.Id && role.Role == OrganizationRole.Teacher && role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null)
            let classCount = _dbContext.TeacherAssignments.Count(teacher => teacher.OrganizationId == organizationId && teacher.TeacherMembershipId == membership.Id && teacher.Status == TeacherAssignmentStatus.Active && teacher.EndedAtUtc == null && (!filters.ClassId.HasValue || teacher.ClassId == filters.ClassId.Value) && (!filters.CourseId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.CourseId == filters.CourseId.Value)) && (!filters.AcademicPeriodId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.AcademicPeriodId == filters.AcademicPeriodId.Value)))
            let completedSessions = _dbContext.AcademicSessions.Count(session => session.OrganizationId == organizationId && session.Status != SessionStatus.Cancelled && session.EndUtc <= dataAsOfUtc && (!query.FromUtc.HasValue || session.StartUtc >= query.FromUtc.Value) && (!query.ToUtc.HasValue || session.StartUtc <= query.ToUtc.Value) && _dbContext.TeacherAssignments.Any(teacher => teacher.OrganizationId == organizationId && teacher.TeacherMembershipId == membership.Id && teacher.ClassId == session.ClassId && teacher.Status == TeacherAssignmentStatus.Active && teacher.EndedAtUtc == null && (!filters.ClassId.HasValue || teacher.ClassId == filters.ClassId.Value) && (!filters.CourseId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.CourseId == filters.CourseId.Value)) && (!filters.AcademicPeriodId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.AcademicPeriodId == filters.AcademicPeriodId.Value))))
            let attendanceMissing = _dbContext.AcademicSessions.Count(session => session.OrganizationId == organizationId && session.Status != SessionStatus.Cancelled && session.EndUtc <= dataAsOfUtc && _dbContext.TeacherAssignments.Any(teacher => teacher.OrganizationId == organizationId && teacher.TeacherMembershipId == membership.Id && teacher.ClassId == session.ClassId && teacher.Status == TeacherAssignmentStatus.Active && teacher.EndedAtUtc == null && (!filters.ClassId.HasValue || teacher.ClassId == filters.ClassId.Value) && (!filters.CourseId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.CourseId == filters.CourseId.Value)) && (!filters.AcademicPeriodId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.AcademicPeriodId == filters.AcademicPeriodId.Value))) && _dbContext.Enrollments.Count(enrollment => enrollment.OrganizationId == organizationId && enrollment.ClassId == session.ClassId && enrollment.Status == EnrollmentStatus.Active && enrollment.EndedAtUtc == null) > _dbContext.Attendance.Count(attendance => attendance.OrganizationId == organizationId && attendance.SessionId == session.Id))
            let assignmentPending = _dbContext.SubmissionAttempts.Count(attempt => attempt.OrganizationId == organizationId && attempt.Status == SubmissionAttemptStatus.Submitted && _dbContext.Assignments.Any(assignment => assignment.OrganizationId == organizationId && assignment.Id == attempt.AssignmentId && _dbContext.TeacherAssignments.Any(teacher => teacher.OrganizationId == organizationId && teacher.TeacherMembershipId == membership.Id && teacher.ClassId == assignment.ClassId && teacher.Status == TeacherAssignmentStatus.Active && teacher.EndedAtUtc == null && (!filters.ClassId.HasValue || teacher.ClassId == filters.ClassId.Value) && (!filters.CourseId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.CourseId == filters.CourseId.Value)) && (!filters.AcademicPeriodId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.AcademicPeriodId == filters.AcademicPeriodId.Value)))) && !_dbContext.GradeReleases.Any(release => release.OrganizationId == organizationId && release.SubmissionAttemptId == attempt.Id))
            let examPending = _dbContext.ExamAttempts.Count(attempt => attempt.OrganizationId == organizationId && attempt.Status == ExamAttemptStatus.Finalized && _dbContext.TeacherAssignments.Any(teacher => teacher.OrganizationId == organizationId && teacher.TeacherMembershipId == membership.Id && teacher.ClassId == attempt.ClassId && teacher.Status == TeacherAssignmentStatus.Active && teacher.EndedAtUtc == null && (!filters.ClassId.HasValue || teacher.ClassId == filters.ClassId.Value) && (!filters.CourseId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.CourseId == filters.CourseId.Value)) && (!filters.AcademicPeriodId.HasValue || _dbContext.Classes.Any(c => c.OrganizationId == organizationId && c.Id == teacher.ClassId && c.AcademicPeriodId == filters.AcademicPeriodId.Value))) && !_dbContext.ExamGradeReleases.Any(release => release.OrganizationId == organizationId && release.ExamAttemptId == attempt.Id))
            select new ManagerTeacherWorkloadRow { MembershipId = membership.Id, UserId = user.Id, DisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName, ClassCount = classCount, SessionCount = completedSessions, AttendanceMissingCount = attendanceMissing, AssignmentPendingCount = assignmentPending, ExamPendingCount = examPending };
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "classesdesc" => rows.OrderByDescending(row => row.ClassCount).ThenBy(row => row.DisplayName).ThenBy(row => row.MembershipId),
            "pendingdesc" => rows.OrderByDescending(row => row.AssignmentPendingCount + row.ExamPendingCount).ThenBy(row => row.DisplayName).ThenBy(row => row.MembershipId),
            _ => rows.OrderBy(row => row.DisplayName).ThenBy(row => row.MembershipId)
        };
        var items = await ordered.AsNoTracking().Skip(Skip(query)).Take(query.PageSize)
            .Select(row => new ManagerTeacherWorkloadReportItem(row.MembershipId, row.UserId, row.DisplayName, row.ClassCount, row.SessionCount, row.AttendanceMissingCount, row.AssignmentPendingCount, row.ExamPendingCount))
            .ToArrayAsync(cancellationToken);
        return Page(dataAsOfUtc, query, totalCount, items);
    }

    private sealed class ManagerEnrollmentRow
    {
        public Guid EnrollmentId { get; init; }
        public Guid LearnerId { get; init; }
        public string LearnerDisplayName { get; init; } = string.Empty;
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public Guid CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public Guid PeriodId { get; init; }
        public string PeriodTitle { get; init; } = string.Empty;
        public EnrollmentStatus Status { get; init; }
        public DateTime EnrolledAtUtc { get; init; }
        public DateTime? EndedAtUtc { get; init; }
    }

    private sealed class ManagerAttendanceRow
    {
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
        public int EligibleCount { get; init; }
        public int RecordedCount { get; init; }
        public int PresentCount { get; init; }
        public int AbsentCount { get; init; }
        public int LateCount { get; init; }
        public int ExcusedCount { get; init; }
        public int NotRecordedCount { get; init; }
        public decimal? AbsencePercentage { get; init; }
    }

    private sealed class ManagerAssessmentRow
    {
        public DetailedReportAssessmentKind Kind { get; init; }
        public Guid AssessmentId { get; init; }
        public Guid VersionId { get; init; }
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime DeadlineUtc { get; init; }
        public decimal MaximumScore { get; init; }
        public int RecipientCount { get; init; }
        public int CompletedCount { get; init; }
        public int ReleasedCount { get; init; }
        public int PendingCount { get; init; }
    }

    private sealed class ManagerTeacherWorkloadRow
    {
        public Guid MembershipId { get; init; }
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public int ClassCount { get; init; }
        public int SessionCount { get; init; }
        public int AttendanceMissingCount { get; init; }
        public int AssignmentPendingCount { get; init; }
        public int ExamPendingCount { get; init; }
    }
}
