using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed record CreateAssignmentDraftCommand(
    string Title,
    string Description,
    DateTime DueAtUtc,
    bool AllowLateSubmission,
    int MaxAttempts,
    decimal MaxScore,
    IReadOnlyCollection<Guid>? AttachmentFileIds = null);

public sealed record UpdateAssignmentDraftCommand(
    string Title,
    string Description,
    DateTime DueAtUtc,
    bool AllowLateSubmission,
    int MaxAttempts,
    decimal MaxScore,
    string ExpectedAssignmentRowVersion,
    string ExpectedVersionRowVersion,
    IReadOnlyCollection<Guid>? AttachmentFileIds = null);

public sealed record PublishAssignmentCommand(
    string ExpectedAssignmentRowVersion,
    string ExpectedVersionRowVersion);

public sealed record AssignmentResult(
    Guid Id,
    Guid? OrganizationId,
    Guid ClassId,
    string ClassTitle,
    AssignmentStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PublishedAtUtc,
    Guid VersionId,
    int VersionNumber,
    string Title,
    string Description,
    DateTime DueAtUtc,
    bool AllowLateSubmission,
    int MaxAttempts,
    decimal MaxScore,
    string AssignmentRowVersion,
    string VersionRowVersion)
{
    public AcademicClassRefDto? ClassRef { get; init; }
    public IReadOnlyCollection<Guid> AttachmentFileIds { get; init; } = [];
}

public sealed record PublishAssignmentResult(
    AssignmentResult Assignment,
    int RecipientCount)
{
    public MakanApp.Application.Messaging.AcademicPublicationReceipt? PublicationReceipt { get; init; }
}

public sealed record AssignmentWithVersionRecord(
    Assignment Assignment,
    AssignmentVersion Version,
    string ClassTitle,
    bool ClassIsActive);
