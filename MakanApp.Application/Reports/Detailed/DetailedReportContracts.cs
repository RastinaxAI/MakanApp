using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Reports.Detailed;

public class DetailedReportQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public Guid? ClassId { get; init; }
    public string? Sort { get; init; }
}

public sealed class ManagerEnrollmentReportQuery : DetailedReportQuery
{
    public Guid? CourseId { get; init; }
    public Guid? AcademicPeriodId { get; init; }
    public EnrollmentStatus? Status { get; init; }
}

public sealed class ManagerAssessmentReportQuery : DetailedReportQuery
{
    public Guid? CourseId { get; init; }
    public Guid? AcademicPeriodId { get; init; }
    public DetailedReportAssessmentKind? Kind { get; init; }
}

public sealed class TeacherWorkloadReportQuery : DetailedReportQuery
{
    public Guid? CourseId { get; init; }
    public Guid? AcademicPeriodId { get; init; }
}

public sealed record ReportPage<T>(
    DateTime DataAsOfUtc,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<T> Items);

public sealed record AttendanceReportSummary(
    int EligibleCount,
    int RecordedCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int NotRecordedCount);

public sealed record AttendanceReportPage<T>(
    DateTime DataAsOfUtc,
    int PageNumber,
    int PageSize,
    int TotalCount,
    AttendanceReportSummary Summary,
    IReadOnlyCollection<T> Items);

public sealed record ReportQuerySpec(
    int PageNumber,
    int PageSize,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? ClassId,
    string Sort);

public enum DetailedReportAssessmentKind
{
    Assignment = 1,
    Exam = 2
}

public enum DetailedReportGradeKind
{
    Assignment = 1,
    Exam = 2
}

public sealed record StudentAttendanceReportItem(
    Guid SessionId,
    Guid ClassId,
    string ClassTitle,
    string CourseTitle,
    string SessionTitle,
    DateTime StartUtc,
    DateTime EndUtc,
    AttendanceStatus Status,
    DateTime? RecordedAtUtc);

public sealed record ParentAttendanceReportItem(
    Guid SessionId,
    Guid ClassId,
    string ClassTitle,
    string CourseTitle,
    string SessionTitle,
    DateTime StartUtc,
    DateTime EndUtc,
    AttendanceStatus Status,
    DateTime? RecordedAtUtc);

public sealed record StudentAssignmentReportItem(
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    AssignmentStatus AssignmentStatus,
    DateTime DueAtUtc,
    decimal MaximumScore,
    SubmissionAttemptStatus? SubmissionStatus,
    DateTime? SubmittedAtUtc,
    bool? IsLate,
    decimal? ReleasedScore,
    string? LearnerFeedback,
    DateTime? ReleasedAtUtc);

public sealed record ParentAssignmentReportItem(
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    AssignmentStatus AssignmentStatus,
    DateTime DueAtUtc,
    decimal MaximumScore,
    SubmissionAttemptStatus? SubmissionStatus,
    DateTime? SubmittedAtUtc,
    bool? IsLate,
    decimal? ReleasedScore,
    string? GuardianVisibleFeedback,
    DateTime? ReleasedAtUtc);

public sealed record StudentExamReportItem(
    Guid ExamId,
    Guid ExamVersionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    int MaximumAttempts,
    decimal MaximumScore,
    int AttemptCount,
    ExamAttemptStatus? LatestAttemptStatus,
    DateTime? LatestStartedAtUtc,
    DateTime? LatestFinalizedAtUtc,
    decimal? ReleasedScore,
    string? LearnerFeedback,
    DateTime? ReleasedAtUtc);

public sealed record ParentExamReportItem(
    Guid ExamId,
    Guid ExamVersionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    decimal MaximumScore,
    int AttemptCount,
    ExamAttemptStatus? LatestAttemptStatus,
    DateTime? LatestFinalizedAtUtc,
    decimal? ReleasedScore,
    string? GuardianVisibleFeedback,
    DateTime? ReleasedAtUtc);

public sealed record StudentReleasedGradeReportItem(
    DetailedReportGradeKind Kind,
    Guid AssessmentId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    decimal Score,
    decimal MaximumScore,
    decimal Percentage,
    DateTime ReleasedAtUtc);

public sealed record TeacherAttendanceReportItem(
    Guid SessionId,
    string SessionTitle,
    DateTime StartUtc,
    DateTime EndUtc,
    Guid EnrollmentId,
    Guid LearnerOrganizationPersonId,
    string LearnerDisplayName,
    AttendanceStatus Status,
    DateTime? RecordedAtUtc);

public sealed record TeacherAssignmentReportItem(
    Guid AssignmentId,
    Guid AssignmentVersionId,
    string Title,
    DateTime DueAtUtc,
    decimal MaximumScore,
    Guid EnrollmentId,
    Guid LearnerOrganizationPersonId,
    string LearnerDisplayName,
    Guid? SubmissionAttemptId,
    SubmissionAttemptStatus? SubmissionStatus,
    DateTime? SubmittedAtUtc,
    bool? IsLate,
    decimal? CurrentScore,
    EvaluationRevisionStatus? EvaluationStatus,
    bool IsReleased);

public sealed record TeacherExamReportItem(
    Guid ExamId,
    Guid ExamVersionId,
    string Title,
    DateTime AvailableUntilUtc,
    decimal MaximumScore,
    Guid EnrollmentId,
    Guid LearnerOrganizationPersonId,
    string LearnerDisplayName,
    Guid? ExamAttemptId,
    ExamAttemptStatus? AttemptStatus,
    DateTime? FinalizedAtUtc,
    decimal? CurrentScore,
    ExamGradeRevisionStatus? GradeStatus,
    bool IsReleased);

public sealed record TeacherLearnerOverviewDto(
    DateTime DataAsOfUtc,
    Guid ClassId,
    string ClassTitle,
    Guid EnrollmentId,
    Guid LearnerOrganizationPersonId,
    string LearnerDisplayName,
    int EligibleSessionCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int NotRecordedCount,
    int AssignmentCount,
    int SubmittedAssignmentCount,
    int ReleasedAssignmentCount,
    int ExamCount,
    int FinalizedExamAttemptCount,
    int ReleasedExamCount);

public sealed record ManagerEnrollmentReportItem(
    Guid EnrollmentId,
    Guid LearnerOrganizationPersonId,
    string LearnerDisplayName,
    Guid ClassId,
    string ClassTitle,
    Guid CourseId,
    string CourseTitle,
    Guid AcademicPeriodId,
    string AcademicPeriodTitle,
    EnrollmentStatus Status,
    DateTime EnrolledAtUtc,
    DateTime? EndedAtUtc);

public sealed record ManagerAttendanceReportItem(
    Guid ClassId,
    string ClassTitle,
    string CourseTitle,
    int EligibleRecordCount,
    int RecordedCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int NotRecordedCount,
    decimal? RecordedAbsencePercentage);

public sealed record ManagerAssessmentReportItem(
    DetailedReportAssessmentKind Kind,
    Guid AssessmentId,
    Guid VersionId,
    Guid ClassId,
    string ClassTitle,
    string CourseTitle,
    string Title,
    DateTime DeadlineUtc,
    decimal MaximumScore,
    int RecipientCount,
    int SubmittedOrFinalizedCount,
    int ReleasedResultCount,
    int PendingGradingCount);

public sealed record ManagerClassPerformanceDto(
    DateTime DataAsOfUtc,
    Guid ClassId,
    string ClassTitle,
    string CourseTitle,
    int ActiveLearnerCount,
    int AssignmentReleasedResultCount,
    decimal? AssignmentAveragePercentage,
    int ExamReleasedResultCount,
    decimal? ExamAveragePercentage);

public sealed record ManagerTeacherWorkloadReportItem(
    Guid TeacherMembershipId,
    Guid TeacherUserId,
    string TeacherDisplayName,
    int ActiveClassCount,
    int CompletedSessionCount,
    int AttendanceMissingCount,
    int AssignmentPendingGradingCount,
    int ExamPendingGradingCount);
