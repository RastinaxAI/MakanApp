using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Platform;

public sealed record PlatformOrganizationContext(Guid Id, string Name);

public sealed record PlatformEducationQuery(
    Guid? OrganizationId,
    Guid? ClassId,
    string? Search,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformClassQuery(
    Guid? OrganizationId,
    string? Search,
    ClassStatus? Status,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformClassItem(
    Guid ClassId,
    PlatformOrganizationContext Organization,
    Guid AcademicPeriodId,
    string AcademicPeriodTitle,
    Guid CourseId,
    string CourseTitle,
    string Title,
    int Capacity,
    ClassStatus Status,
    int ActiveTeacherCount,
    int ActiveLearnerCount,
    DateTime CreatedAtUtc);

public sealed record PlatformClassPage(
    IReadOnlyCollection<PlatformClassItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformClassPerson(
    Guid ReferenceId,
    Guid PersonId,
    Guid? UserId,
    string DisplayName);

public sealed record PlatformClassSessionSummary(
    int Total,
    int Scheduled,
    int Completed,
    int Cancelled);

public sealed record PlatformClassAssessmentSummary(
    int TotalAssignments,
    int PublishedAssignments,
    int TotalExams,
    int PublishedExams);

public sealed record PlatformClassAttendanceSummary(
    int TotalRecords,
    int Present,
    int Absent,
    int Late,
    int Excused);

public sealed record PlatformClassDetail(
    PlatformClassItem Class,
    IReadOnlyCollection<PlatformClassPerson> ActiveTeachers,
    IReadOnlyCollection<PlatformClassPerson> ActiveLearners,
    bool ActiveTeachersTruncated,
    bool ActiveLearnersTruncated,
    PlatformClassSessionSummary Sessions,
    PlatformClassAssessmentSummary Assessments,
    PlatformClassAttendanceSummary Attendance);

public sealed record PlatformTeacherAssignmentItem(
    Guid TeacherAssignmentId,
    PlatformOrganizationContext Organization,
    Guid ClassId,
    string ClassTitle,
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    TeacherAssignmentStatus Status,
    DateTime AssignedAtUtc,
    DateTime? EndedAtUtc);

public sealed record PlatformTeacherAssignmentPage(
    IReadOnlyCollection<PlatformTeacherAssignmentItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformEnrollmentItem(
    Guid EnrollmentId,
    PlatformOrganizationContext Organization,
    Guid ClassId,
    string ClassTitle,
    Guid OrganizationPersonId,
    Guid PersonId,
    Guid? UserId,
    string DisplayName,
    EnrollmentStatus Status,
    DateTime EnrolledAtUtc,
    DateTime? EndedAtUtc);

public sealed record PlatformEnrollmentPage(
    IReadOnlyCollection<PlatformEnrollmentItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformAssignmentItem(
    Guid AssignmentId,
    PlatformOrganizationContext Organization,
    Guid ClassId,
    string ClassTitle,
    AssignmentStatus Status,
    int CurrentVersionNumber,
    string? CurrentTitle,
    decimal? CurrentMaxScore,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc);

public sealed record PlatformAssignmentPage(
    IReadOnlyCollection<PlatformAssignmentItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformExamItem(
    Guid ExamId,
    PlatformOrganizationContext Organization,
    Guid ClassId,
    string ClassTitle,
    ExamStatus Status,
    int CurrentVersionNumber,
    string? CurrentTitle,
    decimal? CurrentMaxScore,
    DateTime CreatedAtUtc);

public sealed record PlatformExamPage(
    IReadOnlyCollection<PlatformExamItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformConversationMetadataQuery(
    Guid? OrganizationId,
    ConversationType? Type,
    ConversationScope? Scope,
    ConversationVisibility? Visibility,
    ConversationStatus? Status,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformConversationMetadataItem(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    PlatformOrganizationContext? Organization,
    ConversationVisibility Visibility,
    ConversationStatus Status,
    int ActiveMemberCount,
    int ModerationCaseCount,
    DateTime CreatedAtUtc);

public sealed record PlatformConversationMetadataPage(
    IReadOnlyCollection<PlatformConversationMetadataItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformModerationCaseQuery(
    ModerationCaseStatus? Status,
    ModerationPriority? Priority,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformModerationCaseItem(
    Guid CaseId,
    Guid ConversationId,
    PlatformOrganizationContext? Organization,
    ModerationCaseSource Source,
    ModerationPriority Priority,
    ModerationCaseStatus Status,
    Guid? AssignedReviewerUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Version);

public sealed record PlatformModerationCasePage(
    IReadOnlyCollection<PlatformModerationCaseItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformModerationEvidenceItem(
    Guid FileAssetId,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record PlatformModerationActionItem(
    ModerationActionType Type,
    Guid? ActorUserId,
    DateTime CreatedAtUtc);

public sealed record PlatformAbuseReportEvidence(
    Guid ReportId,
    Guid MessageId,
    Guid ReporterUserId,
    Guid ReportedUserId,
    AbuseReportReason Reason,
    string? Description,
    MessageKind MessageKind,
    string? ContentSnapshot);

public sealed record PlatformSafetyIncidentEvidence(
    Guid IncidentId,
    Guid ActorUserId,
    Guid ClientMessageId,
    MessageKind ContentKind,
    ContentSafetyDecision Decision,
    string? RiskLevel,
    string? PolicyCodes,
    string? ContentSnapshot);

public sealed record PlatformModerationCaseDetail(
    PlatformModerationCaseItem Case,
    DateTime? ReviewStartedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? DismissedAtUtc,
    PlatformAbuseReportEvidence? AbuseReport,
    PlatformSafetyIncidentEvidence? SafetyIncident,
    IReadOnlyCollection<PlatformModerationEvidenceItem> Attachments,
    IReadOnlyCollection<PlatformModerationActionItem> Actions,
    IReadOnlyCollection<PlatformModerationContextMessage>? ContextMessages = null);

public sealed record PlatformModerationContextMessage(Guid MessageId, Guid SenderUserId, long Sequence, MessageKind Kind, string? Text, DateTime SentAtUtc);
public sealed record PlatformModerationActionCommand(string ExpectedVersion, string Reason, ModerationActionType Action, DateTime? RestrictedUntilUtc = null);

public sealed record PlatformModerationVersionCommand(string ExpectedVersion, string Reason);

public sealed record PlatformCompleteModerationCommand(
    string ExpectedVersion,
    string Reason,
    string? InternalNote);
