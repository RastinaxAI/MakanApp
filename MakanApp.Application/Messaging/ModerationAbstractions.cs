using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Messaging;

public sealed record ModerationCaseListStoreResult(
    IReadOnlyCollection<ModerationCase> Items,
    int TotalCount);

public sealed record ModerationActionStoreRecord(ModerationAction Action);

public sealed record ModerationEvidenceAttachmentStoreRecord(
    ModerationEvidenceAttachment Evidence,
    FileAsset FileAsset);

public sealed record ModerationCaseDetailStoreRecord(
    ModerationCase Case,
    AbuseReport Report,
    SafeMessagingIdentityRecord Reporter,
    SafeMessagingIdentityRecord ReportedUser,
    IReadOnlyCollection<ModerationEvidenceAttachmentStoreRecord> Attachments,
    IReadOnlyCollection<ModerationActionStoreRecord> Actions);

public interface IModerationStore
{
    Task<ModerationCaseListStoreResult> GetCasesAsync(
        Guid reviewerUserId,
        Guid conversationId,
        AccessContext accessContext,
        ModerationCaseStatus? status,
        ModerationPriority? priority,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailStoreRecord> GetCaseAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        AccessContext accessContext,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailStoreRecord> StartReviewAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        AccessContext accessContext,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailStoreRecord> CompleteAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        AccessContext accessContext,
        byte[] expectedVersion,
        string? internalNote,
        bool dismiss,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<FileAsset> GetEvidenceFileAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        Guid fileAssetId,
        AccessContext accessContext,
        CancellationToken cancellationToken);
}

public interface IModerationService
{
    Task<ModerationCasePageResult> GetCasesAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetModerationCasesQuery query,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailResult> GetCaseAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailResult> StartReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        StartModerationReviewCommand command,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailResult> ResolveAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        CompleteModerationCaseCommand command,
        CancellationToken cancellationToken);

    Task<ModerationCaseDetailResult> DismissAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        CompleteModerationCaseCommand command,
        CancellationToken cancellationToken);

    Task<FileDownloadResult> DownloadEvidenceAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        Guid fileAssetId,
        CancellationToken cancellationToken);
}
