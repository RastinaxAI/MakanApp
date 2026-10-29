using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Academic;

public sealed record CreateClassLessonCommand(Guid ClientOperationId, string Title, string? Body, int DisplayOrder, IReadOnlyCollection<Guid> FileAssetIds);
public sealed record UpdateClassLessonCommand(string Title, string? Body, int DisplayOrder, IReadOnlyCollection<Guid> FileAssetIds, string ExpectedVersion);
public sealed record PublishClassLessonCommand(string ExpectedVersion);
public sealed record LessonAttachmentResult(Guid FileAssetId, string FileName, string ContentType, long SizeBytes,
    FileAssetStatus Status, bool IsAvailable, string? DownloadUrl);
public sealed record ClassLessonResult(Guid Id, Guid OrganizationId, Guid ClassId, Guid CourseId, string Title, string Body, int DisplayOrder,
    LessonStatus Status, int RevisionNumber, string Version, Guid CreatedByUserId, DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc, DateTime? PublishedAtUtc, IReadOnlyCollection<LessonAttachmentResult> Attachments, bool Replayed = false);

public interface IClassLessonStore
{
    Task<IOrganizationTransaction> BeginAsync(CancellationToken ct);
    Task LockClassAsync(Guid organizationId, Guid classId, CancellationToken ct);
    Task<ClassLesson?> FindOperationAsync(Guid organizationId, Guid actorId, Guid operationId, CancellationToken ct);
    Task<ClassLesson?> GetAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken ct);
    Task<SchoolPage<ClassLessonResult>> ListAsync(AccessContext context, Guid classId, SchoolPageQuery query, bool includeDrafts, DateTime now, CancellationToken ct);
    Task<ClassLessonResult> ResultAsync(ClassLesson lesson, bool replayed, CancellationToken ct);
    void Add(ClassLesson lesson);
    Task SaveAsync(CancellationToken ct);
}
