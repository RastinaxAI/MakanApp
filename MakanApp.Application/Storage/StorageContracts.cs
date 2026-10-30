using MakanApp.Domain.Storage;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Storage;

public sealed record UploadFileCommand(
    string OriginalFileName,
    string ContentType,
    long DeclaredSizeBytes,
    Stream Content,
    WorkspaceType? WorkspaceScope = null);

public sealed record FileAssetResult(
    Guid Id,
    Guid? OrganizationId,
    Guid UploadedByUserId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Sha256Hash,
    FileAssetStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? DeletedAtUtc,
    DateTime? RetainedAtUtc,
    DateTime UnattachedExpiresAtUtc,
    string RowVersion);

public sealed record FileDownloadResult(
    Stream Content,
    string ContentType,
    string DownloadFileName,
    long SizeBytes);

public sealed record StoredFileResult(long SizeBytes, string Sha256Hash);
