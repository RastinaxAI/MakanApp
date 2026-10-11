using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Reports.Dashboards;

public enum DashboardAudience
{
    Student = 1,
    Parent = 2,
    Teacher = 3,
    Manager = 4
}

public enum StudentAssignmentActionStatus
{
    NotStarted = 1,
    Draft = 2,
    Submitted = 3,
    SubmittedLate = 4,
    Graded = 5
}

public sealed record DashboardSessionItem(
    Guid SessionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    string TimeZoneId,
    string? MeetingUrl,
    SessionStatus Status);

public sealed record DashboardAttendanceSummary(
    int EligibleSessionCount,
    int RecordedCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int NotRecordedCount);

public sealed record DashboardAssignmentItem(
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    DateTime DueAtUtc,
    bool AllowLateSubmission,
    StudentAssignmentActionStatus Status);

public sealed record StudentReleasedAssignmentItem(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    string AssignmentTitle,
    decimal Score,
    decimal MaximumScore,
    string? LearnerFeedback,
    DateTime ReleasedAtUtc);

public sealed record ParentReleasedAssignmentItem(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    string AssignmentTitle,
    decimal Score,
    decimal MaximumScore,
    string? GuardianVisibleFeedback,
    DateTime ReleasedAtUtc);

public sealed record DashboardExamItem(
    Guid ExamId,
    Guid ExamVersionId,
    Guid ClassId,
    string ClassTitle,
    string Title,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    ExamAttemptStatus? LatestAttemptStatus,
    ExamResultReleaseStatus? ResultReleaseStatus,
    decimal? ReleasedScore,
    decimal? MaximumScore,
    DateTime? ReleasedAtUtc);

public sealed record DashboardLearner(
    Guid LearnerOrganizationPersonId,
    string DisplayName);

public sealed record StudentDashboardDto(
    DateTime DataAsOfUtc,
    Guid OrganizationId,
    DashboardAudience Audience,
    DashboardSessionItem? NextSession,
    DashboardAssignmentItem? NearestAssignmentDeadline,
    IReadOnlyCollection<StudentReleasedAssignmentItem> RecentReleasedFeedback,
    IReadOnlyCollection<DashboardExamItem> UpcomingExams,
    DashboardAttendanceSummary AttendanceSummary,
    int PendingAssignmentCount);

public sealed record ParentDashboardDto(
    DateTime DataAsOfUtc,
    Guid OrganizationId,
    DashboardAudience Audience,
    DashboardLearner? SelectedLearner,
    IReadOnlyCollection<DashboardSessionItem> UpcomingSessions,
    IReadOnlyCollection<DashboardAssignmentItem> Assignments,
    IReadOnlyCollection<ParentReleasedAssignmentItem> ReleasedAssignmentResults,
    IReadOnlyCollection<DashboardExamItem> Exams,
    DashboardAttendanceSummary AttendanceSummary,
    int AttentionItemCount);

public sealed record DashboardClassItem(
    Guid ClassId,
    string ClassTitle,
    string CourseTitle,
    string AcademicPeriodTitle,
    int ActiveLearnerCount);

public sealed record DashboardAttendancePendingItem(
    Guid SessionId,
    Guid ClassId,
    string ClassTitle,
    string SessionTitle,
    DateTime StartUtc,
    DateTime EndUtc,
    int ExpectedEnrollmentCount,
    int RecordedCount,
    int MissingCount);

public sealed record AssignmentGradingQueueSummary(
    int AwaitingEvaluationCount,
    int DraftEvaluationCount,
    int TotalPendingCount);

public sealed record ExamGradingQueueSummary(
    int AwaitingGradingCount,
    int ManualReviewCount,
    int ReadyForReleaseCount,
    int TotalPendingCount);

public sealed record TeacherDashboardDto(
    DateTime DataAsOfUtc,
    Guid OrganizationId,
    DashboardAudience Audience,
    IReadOnlyCollection<DashboardClassItem> AssignedClasses,
    IReadOnlyCollection<DashboardSessionItem> UpcomingSessions,
    IReadOnlyCollection<DashboardAttendancePendingItem> AttendancePending,
    AssignmentGradingQueueSummary AssignmentGradingQueue,
    ExamGradingQueueSummary ExamGradingQueue);

public sealed record ManagerOperationalCounts(
    int ActiveClassCount,
    int EnrolledLearnerCount,
    int TeacherCount);

public sealed record ManagerOnboardingChecklist(
    bool OrganizationBasicDataAvailable,
    bool HasAcademicPeriod,
    bool HasCourse,
    bool HasTeacher,
    bool HasLearner,
    bool HasClass);

public sealed record ManagerDashboardDto(
    DateTime DataAsOfUtc,
    Guid OrganizationId,
    DashboardAudience Audience,
    ManagerOperationalCounts OperationalCounts,
    ManagerOnboardingChecklist Onboarding,
    IReadOnlyCollection<DashboardSessionItem> UpcomingSessions,
    IReadOnlyCollection<DashboardSessionItem> SessionsMissingMeetingLink,
    IReadOnlyCollection<DashboardAttendancePendingItem> AttendancePending,
    int PendingInvitationCount,
    AssignmentGradingQueueSummary AssignmentGradingQueue,
    ExamGradingQueueSummary ExamGradingQueue);
