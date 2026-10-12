using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record GetModerationCasesQuery(
    ModerationCaseStatus? Status,
    ModerationPriority? Priority,
    int Page = 1,
    int PageSize = 30);

public sealed record ModerationCaseSummaryResult(
    Guid CaseId,
    Guid ConversationId,
    ModerationCaseSource Source,
    ModerationPriority Priority,
    ModerationCaseStatus Status,
    Guid? AssignedReviewerUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Version);

public sealed record ModerationCasePageResult(
    IReadOnlyCollection<ModerationCaseSummaryResult> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ModerationEvidenceAttachmentResult(
    Guid FileAssetId,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record ModerationActionResult(
    ModerationActionType Type,
    Guid? ActorUserId,
    DateTime CreatedAtUtc);

public sealed record ModerationCaseDetailResult(
    Guid CaseId,
    Guid ConversationId,
    ModerationCaseSource Source,
    ModerationPriority Priority,
    ModerationCaseStatus Status,
    Guid? AssignedReviewerUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ReviewStartedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? DismissedAtUtc,
    Guid ReportId,
    Guid MessageId,
    AbuseReportReason Reason,
    string? Description,
    SafeMessagingIdentityResult Reporter,
    SafeMessagingIdentityResult ReportedUser,
    MessageKind ReportedMessageKind,
    string? ReportedContentSnapshot,
    IReadOnlyCollection<ModerationEvidenceAttachmentResult> Attachments,
    IReadOnlyCollection<ModerationActionResult> Actions,
    string Version);

public sealed record StartModerationReviewCommand(string ExpectedVersion);

public sealed record CompleteModerationCaseCommand(
    string ExpectedVersion,
    string? InternalNote);
