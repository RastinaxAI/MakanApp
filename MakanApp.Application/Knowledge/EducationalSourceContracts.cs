using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Knowledge;

public sealed record CreateEducationalSourceCommand(Guid ClientOperationId, EducationalSourceScope Scope, Guid? ClassId,
    string Title, string? Description, Guid FileAssetId, string Reason);
public sealed record UpdateSourceDraftCommand(string Title, string? Description, string ExpectedVersion, string Reason);
public sealed record CreateSourceVersionCommand(Guid FileAssetId, string Title, string? Description, string ExpectedVersion, string Reason);
public sealed record SourceMutationCommand(string ExpectedVersion, string Reason);
public sealed record SourceVisibilityCommand(SourceVisibility Visibility, string ExpectedVersion, string Reason);
public sealed record SourceCatalogQuery(EducationalSourceScope Scope = EducationalSourceScope.Personal, Guid? ClassId = null,
    string? Search = null, int Page = 1, int PageSize = 50, EducationalSourceStatus? Status = null, EducationalSourceType? SourceType = null);
public sealed record SourceActor(Guid UserId, Guid SessionId, AccessContext Account, AccessContext? Organization);
public sealed record SourceCapabilities(bool CanRead, bool CanWrite, bool CanPublish, bool CanArchive, bool CanDownload, bool CanUseForAi)
{
    public bool CanCreateVersion { get; init; }
    public bool CanChangeVisibility { get; init; }
}
public sealed record SourceFileMetadata(Guid FileAssetId, string OriginalFileName, string ContentType, long SizeBytes, string Status);
public sealed record EducationalSourceResult(Guid SourceId, EducationalSourceScope Scope, Guid? OwnerUserId, Guid? OrganizationId, Guid? ClassId,
    EducationalSourceStatus Status, SourceVisibility Visibility, int CurrentVersion, int? PublishedVersion, int Revision,
    string Title, string? Description, EducationalSourceType SourceType, SourcePublicationStatus PublicationStatus,
    SourceSafetyStatus SafetyStatus, SourceProcessingStatus ProcessingStatus, string? ProcessingFailureCode,
    bool FileReady, bool SourceReady, bool AiReady, SourceCapabilities Capabilities, SourceFileMetadata? File,
    Guid CreatedByUserId, DateTime CreatedAtUtc, DateTime? PublishedAtUtc, string Version, bool Replayed = false)
{
    public string AiReadiness => "Unavailable";
    public string AiUnavailableReason => Scope == EducationalSourceScope.Library ? "ProductPolicyDisabled"
        : !SourceReady || PublicationStatus != SourcePublicationStatus.Published ||
          ProcessingStatus is SourceProcessingStatus.Pending or SourceProcessingStatus.Processing or SourceProcessingStatus.Failed
            ? "SourceNotReady" : "RetrievalUnavailable";
}
public sealed record SourcePage(int Page, int PageSize, int TotalCount, IReadOnlyList<EducationalSourceResult> Items);

public interface IEducationalSourceStore
{
    Task<EducationalSourceResult> CreateAsync(SourceActor actor, CreateEducationalSourceCommand command, CancellationToken ct);
    Task<EducationalSourceResult> GetAsync(SourceActor actor, Guid id, int? revision, CancellationToken ct);
    Task<SourcePage> ListAsync(SourceActor actor, SourceCatalogQuery query, bool libraryManagement, CancellationToken ct);
    Task<EducationalSourceResult> MutateAsync(SourceActor actor, Guid id, UpdateSourceDraftCommand? metadata,
        CreateSourceVersionCommand? version, SourceMutationCommand? publish, SourceMutationCommand? archive, SourceVisibilityCommand? visibility, CancellationToken ct);
    Task<FileDownloadResult> DownloadAsync(SourceActor actor, Guid id, int? revision, CancellationToken ct);
    Task<bool> CanReadFileAsync(Guid fileId, AccessContext context, CancellationToken ct);
}

public sealed class EducationalSourceException(string code) : Exception("عملیات منبع آموزشی مجاز نیست یا با وضعیت جاری سازگار نیست.")
{
    public string Code { get; } = code;
}
public static class SourceErrorCodes
{
    public const string NotFound = "SOURCE_NOT_FOUND";
    public const string Invalid = "SOURCE_REQUEST_INVALID";
    public const string NotAllowed = "SOURCE_NOT_ALLOWED";
    public const string NotReady = "SOURCE_NOT_READY";
    public const string Conflict = "SOURCE_VERSION_CONFLICT";
    public const string QuotaExceeded = "SOURCE_QUOTA_EXCEEDED";
    public const string IdempotencyConflict = "SOURCE_IDEMPOTENCY_CONFLICT";
}
