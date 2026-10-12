using System.Globalization;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;

namespace MakanApp.Application.Reports.Detailed;

public sealed partial class DetailedReportService
{
    public async Task<ReportExportPreparation> PrepareAsync(
        Guid userId,
        Guid sessionId,
        ReportExportType reportType,
        ReportExportFilters filters,
        CancellationToken cancellationToken)
    {
        ValidateFilterShape(reportType, filters);
        var role = RequiredRole(reportType);
        var context = role == OrganizationRole.Parent
            ? await ResolveParentAsync(userId, sessionId, cancellationToken)
            : await ResolveAsync(userId, sessionId, role, cancellationToken);
        if (IsTeacher(reportType))
        {
            await RequireTeacherClassAsync(context, filters.ClassId!.Value, cancellationToken);
        }

        var normalized = NormalizeForExport(reportType, filters, 1, 1);
        var dataAsOfUtc = UtcNow();
        var firstPage = await ReadAuthorizedExportPageAsync(
            context,
            reportType,
            filters,
            normalized,
            dataAsOfUtc,
            cancellationToken);

        return new ReportExportPreparation(
            context.OrganizationId!.Value,
            context.UserId,
            context.SessionId,
            context.MembershipId!.Value,
            role,
            context.SubjectOrganizationPersonId,
            dataAsOfUtc,
            normalized.FromUtc,
            normalized.ToUtc,
            IsTeacher(reportType) ? filters.ClassId : normalized.ClassId,
            filters.CourseId,
            filters.AcademicPeriodId,
            filters.EnrollmentStatus,
            filters.AssessmentKind,
            normalized.Sort,
            firstPage.TotalCount);
    }

    public async Task EnsureCurrentAccessAsync(
        Guid userId,
        Guid sessionId,
        ReportExportJob job,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.UserId != job.RequestedByUserId ||
            context.WorkspaceType != WorkspaceType.Organization ||
            context.OrganizationId != job.OrganizationId ||
            context.MembershipId != job.MembershipId ||
            context.ActiveRole != job.Audience ||
            context.SubjectOrganizationPersonId != job.SubjectOrganizationPersonId)
        {
            throw new ReportExportException(
                ReportExportErrorCodes.NotFound,
                "درخواست export در محدوده دسترسی فعلی پیدا نشد.");
        }

        if (IsTeacher(job.ReportType) &&
            (!job.ClassId.HasValue ||
             !await _readService.HasActiveTeacherAssignmentAsync(context, job.ClassId.Value, cancellationToken)))
        {
            throw new ReportExportException(
                ReportExportErrorCodes.NotFound,
                "درخواست export در محدوده دسترسی فعلی پیدا نشد.");
        }
    }

    public async Task<ReportExportDataPage> ReadPageAsync(
        ReportExportJob job,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureCurrentAccessAsync(
            job.RequestedByUserId,
            job.RequestedBySessionId,
            job,
            cancellationToken);
        var context = await _accessContextResolver.ResolveAsync(
            job.RequestedByUserId,
            job.RequestedBySessionId,
            cancellationToken);
        var filters = new ReportExportFilters(
            job.FromUtc,
            job.ToUtc,
            job.ClassId,
            job.Sort,
            job.CourseId,
            job.AcademicPeriodId,
            job.EnrollmentStatus,
            job.AssessmentKind);
        var normalized = new ReportQuerySpec(
            pageNumber,
            pageSize,
            job.FromUtc,
            job.ToUtc,
            job.ClassId,
            job.Sort);
        return await ReadAuthorizedExportPageAsync(
            context,
            job.ReportType,
            filters,
            normalized,
            job.DataAsOfUtc,
            cancellationToken);
    }

    private async Task<ReportExportDataPage> ReadAuthorizedExportPageAsync(
        AccessContext context,
        ReportExportType reportType,
        ReportExportFilters filters,
        ReportQuerySpec query,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken)
    {
        switch (reportType)
        {
            case ReportExportType.StudentAttendance:
            {
                var page = await _readService.ReadStudentAttendanceAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, StudentAttendanceColumns, page.Items.Select(MapStudentAttendance));
            }
            case ReportExportType.StudentAssignments:
            {
                var page = await _readService.ReadStudentAssignmentsAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, StudentAssignmentColumns, page.Items.Select(MapStudentAssignment));
            }
            case ReportExportType.StudentExams:
            {
                var page = await _readService.ReadStudentExamsAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, StudentExamColumns, page.Items.Select(MapStudentExam));
            }
            case ReportExportType.StudentGrades:
            {
                var page = await _readService.ReadStudentGradesAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, StudentGradeColumns, page.Items.Select(MapStudentGrade));
            }
            case ReportExportType.ParentChildAttendance:
            {
                var page = await _readService.ReadParentAttendanceAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, StudentAttendanceColumns, page.Items.Select(MapParentAttendance));
            }
            case ReportExportType.ParentChildAssignments:
            {
                var page = await _readService.ReadParentAssignmentsAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, ParentAssignmentColumns, page.Items.Select(MapParentAssignment));
            }
            case ReportExportType.ParentChildExams:
            {
                var page = await _readService.ReadParentExamsAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, ParentExamColumns, page.Items.Select(MapParentExam));
            }
            case ReportExportType.TeacherClassAttendance:
            {
                var page = await _readService.ReadTeacherAttendanceAsync(context, dataAsOfUtc, filters.ClassId!.Value, query, cancellationToken);
                return Page(page.TotalCount, TeacherAttendanceColumns, page.Items.Select(MapTeacherAttendance));
            }
            case ReportExportType.TeacherAssignmentGrading:
            {
                var page = await _readService.ReadTeacherAssignmentsAsync(context, dataAsOfUtc, filters.ClassId!.Value, query, cancellationToken);
                return Page(page.TotalCount, TeacherAssignmentColumns, page.Items.Select(MapTeacherAssignment));
            }
            case ReportExportType.TeacherExamGrading:
            {
                var page = await _readService.ReadTeacherExamsAsync(context, dataAsOfUtc, filters.ClassId!.Value, query, cancellationToken);
                return Page(page.TotalCount, TeacherExamColumns, page.Items.Select(MapTeacherExam));
            }
            case ReportExportType.ManagerEnrollments:
            {
                var typed = ManagerEnrollmentQuery(filters, query);
                var page = await _readService.ReadManagerEnrollmentsAsync(context, dataAsOfUtc, typed, query, cancellationToken);
                return Page(page.TotalCount, ManagerEnrollmentColumns, page.Items.Select(MapManagerEnrollment));
            }
            case ReportExportType.ManagerAttendance:
            {
                var page = await _readService.ReadManagerAttendanceAsync(context, dataAsOfUtc, query, cancellationToken);
                return Page(page.TotalCount, ManagerAttendanceColumns, page.Items.Select(MapManagerAttendance));
            }
            case ReportExportType.ManagerAssessments:
            {
                var typed = ManagerAssessmentQuery(filters, query);
                var page = await _readService.ReadManagerAssessmentsAsync(context, dataAsOfUtc, typed, query, cancellationToken);
                return Page(page.TotalCount, ManagerAssessmentColumns, page.Items.Select(MapManagerAssessment));
            }
            case ReportExportType.ManagerTeacherWorkload:
            {
                var typed = TeacherWorkloadQuery(filters, query);
                var page = await _readService.ReadManagerTeacherWorkloadAsync(context, dataAsOfUtc, typed, query, cancellationToken);
                return Page(page.TotalCount, ManagerTeacherWorkloadColumns, page.Items.Select(MapManagerTeacherWorkload));
            }
            default:
                throw new ReportExportException(ReportExportErrorCodes.InvalidRequest, "نوع گزارش export معتبر نیست.");
        }
    }

    private static ReportQuerySpec NormalizeForExport(
        ReportExportType reportType,
        ReportExportFilters filters,
        int pageNumber,
        int pageSize)
    {
        var query = new DetailedReportQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            FromUtc = filters.FromUtc,
            ToUtc = filters.ToUtc,
            ClassId = filters.ClassId,
            Sort = filters.Sort
        };
        var (sorts, defaultSort) = reportType switch
        {
            ReportExportType.StudentAttendance or ReportExportType.ParentChildAttendance or ReportExportType.TeacherClassAttendance => (AttendanceSorts, "occurreddesc"),
            ReportExportType.StudentAssignments or ReportExportType.ParentChildAssignments or ReportExportType.TeacherAssignmentGrading => (AssignmentSorts, "duedesc"),
            ReportExportType.StudentExams or ReportExportType.ParentChildExams or ReportExportType.TeacherExamGrading => (ExamSorts, "availabledesc"),
            ReportExportType.StudentGrades => (GradeSorts, "releaseddesc"),
            ReportExportType.ManagerEnrollments => (EnrollmentSorts, "enrolleddesc"),
            ReportExportType.ManagerAttendance => (ManagerAttendanceSorts, "classtitleasc"),
            ReportExportType.ManagerAssessments => (ManagerAssessmentSorts, "deadlinedesc"),
            ReportExportType.ManagerTeacherWorkload => (WorkloadSorts, "teacherasc"),
            _ => throw new ReportExportException(ReportExportErrorCodes.InvalidRequest, "نوع گزارش export معتبر نیست.")
        };
        return Normalize(query, sorts, defaultSort);
    }

    private static void ValidateFilterShape(ReportExportType reportType, ReportExportFilters filters)
    {
        if ((filters.ClassId.HasValue && filters.ClassId == Guid.Empty) ||
            (filters.CourseId.HasValue && filters.CourseId == Guid.Empty) ||
            (filters.AcademicPeriodId.HasValue && filters.AcademicPeriodId == Guid.Empty) ||
            (filters.EnrollmentStatus.HasValue && !Enum.IsDefined(filters.EnrollmentStatus.Value)) ||
            (filters.AssessmentKind.HasValue && !Enum.IsDefined(filters.AssessmentKind.Value)))
        {
            throw new ReportExportException(ReportExportErrorCodes.InvalidRequest, "فیلتر export معتبر نیست.");
        }

        if (IsTeacher(reportType) && !filters.ClassId.HasValue)
        {
            throw new ReportExportException(ReportExportErrorCodes.InvalidRequest, "classId برای گزارش معلم الزامی است.");
        }

        var allowsCourseAndPeriod = reportType is ReportExportType.ManagerEnrollments or
            ReportExportType.ManagerAssessments or ReportExportType.ManagerTeacherWorkload;
        if (!allowsCourseAndPeriod && (filters.CourseId.HasValue || filters.AcademicPeriodId.HasValue) ||
            reportType != ReportExportType.ManagerEnrollments && filters.EnrollmentStatus.HasValue ||
            reportType != ReportExportType.ManagerAssessments && filters.AssessmentKind.HasValue)
        {
            throw new ReportExportException(ReportExportErrorCodes.InvalidRequest, "فیلترهای ارسال‌شده برای این نوع گزارش مجاز نیستند.");
        }
    }

    private static OrganizationRole RequiredRole(ReportExportType reportType) => reportType switch
    {
        ReportExportType.StudentAttendance or ReportExportType.StudentAssignments or ReportExportType.StudentExams or ReportExportType.StudentGrades => OrganizationRole.Student,
        ReportExportType.ParentChildAttendance or ReportExportType.ParentChildAssignments or ReportExportType.ParentChildExams => OrganizationRole.Parent,
        ReportExportType.TeacherClassAttendance or ReportExportType.TeacherAssignmentGrading or ReportExportType.TeacherExamGrading => OrganizationRole.Teacher,
        ReportExportType.ManagerEnrollments or ReportExportType.ManagerAttendance or ReportExportType.ManagerAssessments or ReportExportType.ManagerTeacherWorkload => OrganizationRole.Manager,
        _ => throw new ReportExportException(ReportExportErrorCodes.InvalidRequest, "نوع گزارش export معتبر نیست.")
    };

    private static bool IsTeacher(ReportExportType reportType) =>
        reportType is ReportExportType.TeacherClassAttendance or
            ReportExportType.TeacherAssignmentGrading or
            ReportExportType.TeacherExamGrading;

    private static ManagerEnrollmentReportQuery ManagerEnrollmentQuery(ReportExportFilters filters, ReportQuerySpec query) => new()
    {
        PageNumber = query.PageNumber,
        PageSize = query.PageSize,
        FromUtc = query.FromUtc,
        ToUtc = query.ToUtc,
        ClassId = query.ClassId,
        Sort = query.Sort,
        CourseId = filters.CourseId,
        AcademicPeriodId = filters.AcademicPeriodId,
        Status = filters.EnrollmentStatus
    };

    private static ManagerAssessmentReportQuery ManagerAssessmentQuery(ReportExportFilters filters, ReportQuerySpec query) => new()
    {
        PageNumber = query.PageNumber,
        PageSize = query.PageSize,
        FromUtc = query.FromUtc,
        ToUtc = query.ToUtc,
        ClassId = query.ClassId,
        Sort = query.Sort,
        CourseId = filters.CourseId,
        AcademicPeriodId = filters.AcademicPeriodId,
        Kind = filters.AssessmentKind switch
        {
            ReportExportAssessmentKind.Assignment => DetailedReportAssessmentKind.Assignment,
            ReportExportAssessmentKind.Exam => DetailedReportAssessmentKind.Exam,
            _ => null
        }
    };

    private static TeacherWorkloadReportQuery TeacherWorkloadQuery(ReportExportFilters filters, ReportQuerySpec query) => new()
    {
        PageNumber = query.PageNumber,
        PageSize = query.PageSize,
        FromUtc = query.FromUtc,
        ToUtc = query.ToUtc,
        ClassId = query.ClassId,
        Sort = query.Sort,
        CourseId = filters.CourseId,
        AcademicPeriodId = filters.AcademicPeriodId
    };

    private static ReportExportDataPage Page(
        int totalCount,
        IReadOnlyList<string> columns,
        IEnumerable<IReadOnlyList<ReportExportCell>> rows) =>
        new(totalCount, columns, rows.ToArray());

    private static ReportExportCell Text(string? value) => new(value, true);
    private static ReportExportCell Value(object? value) => new(value switch
    {
        null => null,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    });

    private static readonly string[] StudentAttendanceColumns = ["SessionId", "ClassId", "ClassTitle", "CourseTitle", "SessionTitle", "StartUtc", "EndUtc", "Status", "RecordedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapStudentAttendance(StudentAttendanceReportItem item) => [Value(item.SessionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.CourseTitle), Text(item.SessionTitle), Value(item.StartUtc), Value(item.EndUtc), Value(item.Status), Value(item.RecordedAtUtc)];
    private static IReadOnlyList<ReportExportCell> MapParentAttendance(ParentAttendanceReportItem item) => [Value(item.SessionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.CourseTitle), Text(item.SessionTitle), Value(item.StartUtc), Value(item.EndUtc), Value(item.Status), Value(item.RecordedAtUtc)];

    private static readonly string[] StudentAssignmentColumns = ["AssignmentId", "AssignmentVersionId", "ClassId", "ClassTitle", "Title", "AssignmentStatus", "DueAtUtc", "MaximumScore", "SubmissionStatus", "SubmittedAtUtc", "IsLate", "ReleasedScore", "LearnerFeedback", "ReleasedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapStudentAssignment(StudentAssignmentReportItem item) => [Value(item.AssignmentId), Value(item.AssignmentVersionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.Title), Value(item.AssignmentStatus), Value(item.DueAtUtc), Value(item.MaximumScore), Value(item.SubmissionStatus), Value(item.SubmittedAtUtc), Value(item.IsLate), Value(item.ReleasedScore), Text(item.LearnerFeedback), Value(item.ReleasedAtUtc)];

    private static readonly string[] ParentAssignmentColumns = ["AssignmentId", "AssignmentVersionId", "ClassId", "ClassTitle", "Title", "AssignmentStatus", "DueAtUtc", "MaximumScore", "SubmissionStatus", "SubmittedAtUtc", "IsLate", "ReleasedScore", "GuardianVisibleFeedback", "ReleasedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapParentAssignment(ParentAssignmentReportItem item) => [Value(item.AssignmentId), Value(item.AssignmentVersionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.Title), Value(item.AssignmentStatus), Value(item.DueAtUtc), Value(item.MaximumScore), Value(item.SubmissionStatus), Value(item.SubmittedAtUtc), Value(item.IsLate), Value(item.ReleasedScore), Text(item.GuardianVisibleFeedback), Value(item.ReleasedAtUtc)];

    private static readonly string[] StudentExamColumns = ["ExamId", "ExamVersionId", "ClassId", "ClassTitle", "Title", "AvailableFromUtc", "AvailableUntilUtc", "DurationMinutes", "MaximumAttempts", "MaximumScore", "AttemptCount", "LatestAttemptStatus", "LatestStartedAtUtc", "LatestFinalizedAtUtc", "ReleasedScore", "LearnerFeedback", "ReleasedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapStudentExam(StudentExamReportItem item) => [Value(item.ExamId), Value(item.ExamVersionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.Title), Value(item.AvailableFromUtc), Value(item.AvailableUntilUtc), Value(item.DurationMinutes), Value(item.MaximumAttempts), Value(item.MaximumScore), Value(item.AttemptCount), Value(item.LatestAttemptStatus), Value(item.LatestStartedAtUtc), Value(item.LatestFinalizedAtUtc), Value(item.ReleasedScore), Text(item.LearnerFeedback), Value(item.ReleasedAtUtc)];

    private static readonly string[] ParentExamColumns = ["ExamId", "ExamVersionId", "ClassId", "ClassTitle", "Title", "AvailableFromUtc", "AvailableUntilUtc", "DurationMinutes", "MaximumScore", "AttemptCount", "LatestAttemptStatus", "LatestFinalizedAtUtc", "ReleasedScore", "GuardianVisibleFeedback", "ReleasedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapParentExam(ParentExamReportItem item) => [Value(item.ExamId), Value(item.ExamVersionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.Title), Value(item.AvailableFromUtc), Value(item.AvailableUntilUtc), Value(item.DurationMinutes), Value(item.MaximumScore), Value(item.AttemptCount), Value(item.LatestAttemptStatus), Value(item.LatestFinalizedAtUtc), Value(item.ReleasedScore), Text(item.GuardianVisibleFeedback), Value(item.ReleasedAtUtc)];

    private static readonly string[] StudentGradeColumns = ["Kind", "AssessmentId", "ClassId", "ClassTitle", "Title", "Score", "MaximumScore", "Percentage", "ReleasedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapStudentGrade(StudentReleasedGradeReportItem item) => [Value(item.Kind), Value(item.AssessmentId), Value(item.ClassId), Text(item.ClassTitle), Text(item.Title), Value(item.Score), Value(item.MaximumScore), Value(item.Percentage), Value(item.ReleasedAtUtc)];

    private static readonly string[] TeacherAttendanceColumns = ["SessionId", "SessionTitle", "StartUtc", "EndUtc", "EnrollmentId", "LearnerOrganizationPersonId", "LearnerDisplayName", "Status", "RecordedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapTeacherAttendance(TeacherAttendanceReportItem item) => [Value(item.SessionId), Text(item.SessionTitle), Value(item.StartUtc), Value(item.EndUtc), Value(item.EnrollmentId), Value(item.LearnerOrganizationPersonId), Text(item.LearnerDisplayName), Value(item.Status), Value(item.RecordedAtUtc)];

    private static readonly string[] TeacherAssignmentColumns = ["AssignmentId", "AssignmentVersionId", "Title", "DueAtUtc", "MaximumScore", "EnrollmentId", "LearnerOrganizationPersonId", "LearnerDisplayName", "SubmissionAttemptId", "SubmissionStatus", "SubmittedAtUtc", "IsLate", "CurrentScore", "EvaluationStatus", "IsReleased"];
    private static IReadOnlyList<ReportExportCell> MapTeacherAssignment(TeacherAssignmentReportItem item) => [Value(item.AssignmentId), Value(item.AssignmentVersionId), Text(item.Title), Value(item.DueAtUtc), Value(item.MaximumScore), Value(item.EnrollmentId), Value(item.LearnerOrganizationPersonId), Text(item.LearnerDisplayName), Value(item.SubmissionAttemptId), Value(item.SubmissionStatus), Value(item.SubmittedAtUtc), Value(item.IsLate), Value(item.CurrentScore), Value(item.EvaluationStatus), Value(item.IsReleased)];

    private static readonly string[] TeacherExamColumns = ["ExamId", "ExamVersionId", "Title", "AvailableUntilUtc", "MaximumScore", "EnrollmentId", "LearnerOrganizationPersonId", "LearnerDisplayName", "ExamAttemptId", "AttemptStatus", "FinalizedAtUtc", "CurrentScore", "GradeStatus", "IsReleased"];
    private static IReadOnlyList<ReportExportCell> MapTeacherExam(TeacherExamReportItem item) => [Value(item.ExamId), Value(item.ExamVersionId), Text(item.Title), Value(item.AvailableUntilUtc), Value(item.MaximumScore), Value(item.EnrollmentId), Value(item.LearnerOrganizationPersonId), Text(item.LearnerDisplayName), Value(item.ExamAttemptId), Value(item.AttemptStatus), Value(item.FinalizedAtUtc), Value(item.CurrentScore), Value(item.GradeStatus), Value(item.IsReleased)];

    private static readonly string[] ManagerEnrollmentColumns = ["EnrollmentId", "LearnerOrganizationPersonId", "LearnerDisplayName", "ClassId", "ClassTitle", "CourseId", "CourseTitle", "AcademicPeriodId", "AcademicPeriodTitle", "Status", "EnrolledAtUtc", "EndedAtUtc"];
    private static IReadOnlyList<ReportExportCell> MapManagerEnrollment(ManagerEnrollmentReportItem item) => [Value(item.EnrollmentId), Value(item.LearnerOrganizationPersonId), Text(item.LearnerDisplayName), Value(item.ClassId), Text(item.ClassTitle), Value(item.CourseId), Text(item.CourseTitle), Value(item.AcademicPeriodId), Text(item.AcademicPeriodTitle), Value(item.Status), Value(item.EnrolledAtUtc), Value(item.EndedAtUtc)];

    private static readonly string[] ManagerAttendanceColumns = ["ClassId", "ClassTitle", "CourseTitle", "EligibleRecordCount", "RecordedCount", "PresentCount", "AbsentCount", "LateCount", "ExcusedCount", "NotRecordedCount", "RecordedAbsencePercentage"];
    private static IReadOnlyList<ReportExportCell> MapManagerAttendance(ManagerAttendanceReportItem item) => [Value(item.ClassId), Text(item.ClassTitle), Text(item.CourseTitle), Value(item.EligibleRecordCount), Value(item.RecordedCount), Value(item.PresentCount), Value(item.AbsentCount), Value(item.LateCount), Value(item.ExcusedCount), Value(item.NotRecordedCount), Value(item.RecordedAbsencePercentage)];

    private static readonly string[] ManagerAssessmentColumns = ["Kind", "AssessmentId", "VersionId", "ClassId", "ClassTitle", "CourseTitle", "Title", "DeadlineUtc", "MaximumScore", "RecipientCount", "SubmittedOrFinalizedCount", "ReleasedResultCount", "PendingGradingCount"];
    private static IReadOnlyList<ReportExportCell> MapManagerAssessment(ManagerAssessmentReportItem item) => [Value(item.Kind), Value(item.AssessmentId), Value(item.VersionId), Value(item.ClassId), Text(item.ClassTitle), Text(item.CourseTitle), Text(item.Title), Value(item.DeadlineUtc), Value(item.MaximumScore), Value(item.RecipientCount), Value(item.SubmittedOrFinalizedCount), Value(item.ReleasedResultCount), Value(item.PendingGradingCount)];

    private static readonly string[] ManagerTeacherWorkloadColumns = ["TeacherMembershipId", "TeacherUserId", "TeacherDisplayName", "ActiveClassCount", "CompletedSessionCount", "AttendanceMissingCount", "AssignmentPendingGradingCount", "ExamPendingGradingCount"];
    private static IReadOnlyList<ReportExportCell> MapManagerTeacherWorkload(ManagerTeacherWorkloadReportItem item) => [Value(item.TeacherMembershipId), Value(item.TeacherUserId), Text(item.TeacherDisplayName), Value(item.ActiveClassCount), Value(item.CompletedSessionCount), Value(item.AttendanceMissingCount), Value(item.AssignmentPendingGradingCount), Value(item.ExamPendingGradingCount)];
}
