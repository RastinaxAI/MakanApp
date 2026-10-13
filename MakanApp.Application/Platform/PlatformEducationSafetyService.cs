using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Platform;

public sealed class PlatformEducationSafetyService(
    IPlatformEducationSafetyStore store,
    IFileStorage fileStorage,
    TimeProvider timeProvider) : IPlatformEducationSafetyService
{
    public Task<PlatformClassPage> GetClassesAsync(
        PlatformClassQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateOptionalId(query.OrganizationId);
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value) || query.Search?.Trim().Length > 200)
        {
            throw InvalidQuery();
        }

        return store.GetClassesAsync(query with { Search = Normalize(query.Search) }, cancellationToken);
    }

    public async Task<PlatformClassDetail> GetClassAsync(Guid classId, CancellationToken cancellationToken) =>
        await store.GetClassAsync(RequireId(classId), cancellationToken)
        ?? throw Error(PlatformErrorCodes.EducationResourceNotFound, "کلاس آموزشی پیدا نشد.");

    public Task<PlatformTeacherAssignmentPage> GetTeachersAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        ValidateEducationQuery(query);
        return store.GetTeachersAsync(query with { Search = Normalize(query.Search) }, cancellationToken);
    }

    public Task<PlatformEnrollmentPage> GetEnrollmentsAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        ValidateEducationQuery(query);
        return store.GetEnrollmentsAsync(query with { Search = Normalize(query.Search) }, cancellationToken);
    }

    public Task<PlatformAssignmentPage> GetAssignmentsAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        ValidateEducationQuery(query);
        return store.GetAssignmentsAsync(query with { Search = Normalize(query.Search) }, cancellationToken);
    }

    public Task<PlatformExamPage> GetExamsAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        ValidateEducationQuery(query);
        return store.GetExamsAsync(query with { Search = Normalize(query.Search) }, cancellationToken);
    }

    public Task<PlatformConversationMetadataPage> GetConversationsAsync(
        PlatformConversationMetadataQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateOptionalId(query.OrganizationId);
        if (query.Type.HasValue && !Enum.IsDefined(query.Type.Value) ||
            query.Scope.HasValue && !Enum.IsDefined(query.Scope.Value) ||
            query.Visibility.HasValue && !Enum.IsDefined(query.Visibility.Value) ||
            query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw InvalidQuery();
        }

        return store.GetConversationsAsync(query, cancellationToken);
    }

    public Task<PlatformModerationCasePage> GetModerationCasesAsync(
        PlatformModerationCaseQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value) ||
            query.Priority.HasValue && !Enum.IsDefined(query.Priority.Value))
        {
            throw InvalidQuery();
        }

        return store.GetModerationCasesAsync(query, cancellationToken);
    }

    public async Task<PlatformModerationCaseDetail> GetModerationCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        await store.GetModerationCaseAsync(RequireId(caseId), cancellationToken)
        ?? throw ModerationNotFound();

    public Task<PlatformModerationCaseDetail> StartModerationReviewAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid caseId,
        PlatformModerationVersionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateMutation(command.ExpectedVersion, command.Reason);
        return store.StartModerationReviewAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(caseId),
            DecodeVersion(command.ExpectedVersion),
            command.Reason.Trim(),
            cancellationToken);
    }

    public Task<PlatformModerationCaseDetail> ResolveModerationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid caseId,
        PlatformCompleteModerationCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        CompleteAsync(actorUserId, actorSessionId, caseId, command, correlationId, false, cancellationToken);

    public Task<PlatformModerationCaseDetail> DismissModerationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid caseId,
        PlatformCompleteModerationCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        CompleteAsync(actorUserId, actorSessionId, caseId, command, correlationId, true, cancellationToken);

    public Task<PlatformModerationCaseDetail> EnforceModerationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid caseId,
        PlatformModerationVersionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateMutation(command.ExpectedVersion, command.Reason);
        return store.EnforceModerationAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(caseId),
            DecodeVersion(command.ExpectedVersion),
            command.Reason.Trim(),
            cancellationToken);
    }

    public async Task<FileDownloadResult> DownloadModerationEvidenceAsync(
        Guid caseId,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        var file = await store.GetModerationEvidenceFileAsync(
            RequireId(caseId),
            RequireId(fileAssetId),
            cancellationToken) ?? throw ModerationNotFound();
        if (file.Status != FileAssetStatus.Ready)
        {
            throw ModerationNotFound();
        }

        var stream = await fileStorage.OpenReadAsync(file.StorageKey, cancellationToken);
        return new FileDownloadResult(stream, file.ContentType, file.OriginalFileName, file.SizeBytes);
    }

    private Task<PlatformModerationCaseDetail> CompleteAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid caseId,
        PlatformCompleteModerationCommand command,
        string correlationId,
        bool dismiss,
        CancellationToken cancellationToken)
    {
        ValidateMutation(command.ExpectedVersion, command.Reason);
        if (command.InternalNote?.Trim().Length > ModerationCase.MaximumInternalNoteLength)
        {
            throw InvalidQuery();
        }

        return store.CompleteModerationAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(caseId),
            DecodeVersion(command.ExpectedVersion),
            command.Reason.Trim(),
            Normalize(command.InternalNote),
            dismiss,
            cancellationToken);
    }

    private void ValidateEducationQuery(PlatformEducationQuery query)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateOptionalId(query.OrganizationId);
        ValidateOptionalId(query.ClassId);
        if (query.Search?.Trim().Length > 200)
        {
            throw InvalidQuery();
        }
    }

    private static void ValidateMutation(string expectedVersion, string reason)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            throw Error(PlatformErrorCodes.ModerationStateConflict, "نسخه پرونده تعدیل معتبر نیست.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw Error(PlatformErrorCodes.ReasonRequired, "دلیل معتبر و حداکثر ۵۰۰ کاراکتر الزامی است.");
        }
    }

    private PlatformActorContext Actor(Guid userId, Guid sessionId, string correlationId)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId))
        {
            throw Error(PlatformErrorCodes.AccessDenied, "هویت عامل عملیات معتبر نیست.");
        }

        return new PlatformActorContext(
            userId,
            sessionId,
            timeProvider.GetUtcNow().UtcDateTime,
            correlationId.Trim());
    }

    private static byte[] DecodeVersion(string value)
    {
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw new FormatException();
        }
        catch (FormatException)
        {
            throw Error(PlatformErrorCodes.ModerationStateConflict, "نسخه پرونده تعدیل معتبر نیست.");
        }
    }

    private static Guid RequireId(Guid value) => value != Guid.Empty ? value : throw InvalidQuery();

    private static void ValidateOptionalId(Guid? value)
    {
        if (value == Guid.Empty)
        {
            throw InvalidQuery();
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is <= 0 or > 100_000 || pageSize is <= 0 or > 100)
        {
            throw InvalidQuery();
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PlatformException InvalidQuery() =>
        Error(PlatformErrorCodes.QueryInvalid, "پارامترهای درخواست معتبر نیستند.");

    private static PlatformException ModerationNotFound() =>
        Error(PlatformErrorCodes.ModerationCaseNotFound, "پرونده تعدیل پیدا نشد.");

    private static PlatformException Error(string code, string message) => new(code, message);
}
