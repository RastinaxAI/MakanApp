using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Platform;

public interface IPlatformEducationSafetyStore
{
    Task<PlatformModerationCaseDetail> ReadModerationContextAsync(PlatformActorContext actor, Guid caseId, bool expanded, string reason, CancellationToken ct, byte[]? expectedVersion = null);
    Task<PlatformModerationCaseDetail> ApplySafetyActionAsync(PlatformActorContext actor, Guid caseId, byte[] expectedVersion, string reason, MakanApp.Domain.Messaging.ModerationActionType action, DateTime? restrictedUntilUtc, CancellationToken ct);
    Task<PlatformClassPage> GetClassesAsync(PlatformClassQuery query, CancellationToken cancellationToken);
    Task<PlatformClassDetail?> GetClassAsync(Guid classId, CancellationToken cancellationToken);
    Task<PlatformTeacherAssignmentPage> GetTeachersAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformEnrollmentPage> GetEnrollmentsAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformAssignmentPage> GetAssignmentsAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformExamPage> GetExamsAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformConversationMetadataPage> GetConversationsAsync(PlatformConversationMetadataQuery query, CancellationToken cancellationToken);
    Task<PlatformModerationCasePage> GetModerationCasesAsync(PlatformModerationCaseQuery query, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail?> GetModerationCaseAsync(Guid caseId, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> StartModerationReviewAsync(PlatformActorContext actor, Guid caseId, byte[] expectedVersion, string reason, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> CompleteModerationAsync(PlatformActorContext actor, Guid caseId, byte[] expectedVersion, string reason, string? internalNote, bool dismiss, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> EnforceModerationAsync(PlatformActorContext actor, Guid caseId, byte[] expectedVersion, string reason, CancellationToken cancellationToken);
    Task<FileAsset?> GetModerationEvidenceFileAsync(Guid caseId, Guid fileAssetId, CancellationToken cancellationToken);
}

public interface IPlatformEducationSafetyService
{
    Task<PlatformClassPage> GetClassesAsync(PlatformClassQuery query, CancellationToken cancellationToken);
    Task<PlatformClassDetail> GetClassAsync(Guid classId, CancellationToken cancellationToken);
    Task<PlatformTeacherAssignmentPage> GetTeachersAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformEnrollmentPage> GetEnrollmentsAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformAssignmentPage> GetAssignmentsAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformExamPage> GetExamsAsync(PlatformEducationQuery query, CancellationToken cancellationToken);
    Task<PlatformConversationMetadataPage> GetConversationsAsync(PlatformConversationMetadataQuery query, CancellationToken cancellationToken);
    Task<PlatformModerationCasePage> GetModerationCasesAsync(PlatformModerationCaseQuery query, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> GetModerationCaseAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, string correlationId, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> GetExpandedModerationContextAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, PlatformModerationVersionCommand command, string correlationId, CancellationToken ct);
    Task<PlatformModerationCaseDetail> ApplySafetyActionAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, PlatformModerationActionCommand command, string correlationId, CancellationToken ct);
    Task<PlatformModerationCaseDetail> StartModerationReviewAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, PlatformModerationVersionCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> ResolveModerationAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, PlatformCompleteModerationCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> DismissModerationAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, PlatformCompleteModerationCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformModerationCaseDetail> EnforceModerationAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, PlatformModerationVersionCommand command, string correlationId, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadModerationEvidenceAsync(Guid actorUserId, Guid actorSessionId, Guid caseId, Guid fileAssetId, string correlationId, CancellationToken cancellationToken);
}
