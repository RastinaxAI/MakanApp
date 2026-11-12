using MakanApp.Application.Commerce;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public static class SchoolApplicationErrorCodes
{
    public const string Invalid = "SCHOOL_APPLICATION_INVALID";
    public const string NotFound = "SCHOOL_APPLICATION_NOT_FOUND";
    public const string Forbidden = "SCHOOL_APPLICATION_PERMISSION_REQUIRED";
    public const string Conflict = "SCHOOL_APPLICATION_CONFLICT";
    public const string FileUnavailable = "SCHOOL_APPLICATION_FILE_UNAVAILABLE";
    public const string ProductDecisionRequired = "SCHOOL_APPLICATION_REQUIREMENTS_UNCONFIRMED";
}
public sealed class SchoolApplicationException(string code) : Exception("عملیات درخواست تأسیس با وضعیت، نسخه یا مجوز جاری سازگار نیست.") { public string Code { get; } = code; }
public sealed record SchoolApplicationActor(Guid UserId, Guid SessionId, string CorrelationId);
public sealed record SchoolDocumentInput(Guid FileAssetId, string Kind);
public sealed record CreateSchoolApplicationCommand(Guid ClientOperationId, SchoolApplicationKind Kind, SchoolApplicationDraftData Data, IReadOnlyCollection<SchoolDocumentInput>? Documents = null);
public sealed record UpdateSchoolApplicationCommand(Guid ClientOperationId, long ExpectedVersion, SchoolApplicationDraftData Data, IReadOnlyCollection<SchoolDocumentInput>? Documents = null);
public sealed record SchoolApplicationActionCommand(Guid ClientOperationId, long ExpectedVersion, string? ApplicantFeedback = null, SchoolApplicationStatus? Decision = null);
public sealed record SchoolApplicationDecisionResult(Guid Id, int Revision, SchoolApplicationStatus Status, string ApplicantFeedback, DateTime OccurredAtUtc);
public sealed record SchoolApplicationResult(Guid Id, SchoolApplicationKind Kind, SchoolApplicationStatus Status, int Revision, long Version,
    SchoolApplicationDraftData Data, IReadOnlyCollection<SchoolDocumentInput> Documents, IReadOnlyCollection<SchoolApplicationDecisionResult> Decisions,
    bool CanSubmit, string? SubmissionLimitation, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record SchoolApplicationPage(IReadOnlyCollection<SchoolApplicationResult> Items, int Total, int Page, int PageSize);
public interface ISchoolApplicationStore
{
    Task<SchoolApplicationResult> CreateAsync(SchoolApplicationActor actor, CreateSchoolApplicationCommand command, CancellationToken ct);
    Task<SchoolApplicationResult> UpdateAsync(SchoolApplicationActor actor, Guid id, UpdateSchoolApplicationCommand command, CancellationToken ct);
    Task<SchoolApplicationResult> ReviewAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool start, CancellationToken ct);
    Task<SchoolApplicationResult> ReadAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int? revision, CancellationToken ct);
    Task<SchoolApplicationPage> PageAsync(SchoolApplicationActor actor, bool reviewer, SchoolApplicationStatus? status, int page, int pageSize, CancellationToken ct);
}
public sealed class SchoolApplicationService(ISchoolApplicationStore store, CommercialActorAccess actors)
{
    public async Task<SchoolApplicationResult> CreateAsync(SchoolApplicationActor actor, CreateSchoolApplicationCommand command, CancellationToken ct)
    {
        await Ensure(actor, ct); if (!Enum.IsDefined(command.Kind) || command.ClientOperationId == Guid.Empty || command.Data is null) throw Invalid();
        Documents(command.Documents); return await store.CreateAsync(actor, command, ct);
    }
    public async Task<SchoolApplicationResult> UpdateAsync(SchoolApplicationActor actor, Guid id, UpdateSchoolApplicationCommand command, CancellationToken ct)
    {
        await Ensure(actor, ct); if (command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1 || command.Data is null) throw Invalid();
        Documents(command.Documents); return await store.UpdateAsync(actor, id, command, ct);
    }
    public async Task<SchoolApplicationResult> SubmitAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, CancellationToken ct)
    {
        await Ensure(actor, ct);
        if (command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1) throw Invalid();
        var request = await store.ReadAsync(actor, id, false, null, ct);
        if (request.Version != command.ExpectedVersion) throw new SchoolApplicationException(SchoolApplicationErrorCodes.Conflict);
        // Requiredness فرم و مدارک هنوز تأیید نشده؛ هیچ شرط پیشنهادی در Runtime به Policy قطعی تبدیل نمی‌شود.
        throw new SchoolApplicationException(SchoolApplicationErrorCodes.ProductDecisionRequired);
    }
    public async Task<SchoolApplicationResult> ReviewAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool start, CancellationToken ct)
    {
        await Ensure(actor, ct);
        if (command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1 || !start &&
            (command.Decision is not (SchoolApplicationStatus.NeedsChanges or SchoolApplicationStatus.Approved or SchoolApplicationStatus.Rejected) ||
             string.IsNullOrWhiteSpace(command.ApplicantFeedback) || command.ApplicantFeedback.Length > 2000)) throw Invalid();
        return await store.ReviewAsync(actor, id, command, start, ct);
    }
    public async Task<SchoolApplicationResult> ReadAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int? revision, CancellationToken ct)
    { await Ensure(actor, ct); if (revision is <= 0) throw Invalid(); return await store.ReadAsync(actor, id, reviewer, revision, ct); }
    public async Task<SchoolApplicationPage> PageAsync(SchoolApplicationActor actor, bool reviewer, SchoolApplicationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        await Ensure(actor, ct); if (page < 1 || page > 100000 || pageSize is < 1 or > 100 || status.HasValue && !Enum.IsDefined(status.Value)) throw Invalid();
        return await store.PageAsync(actor, reviewer, status, page, pageSize, ct);
    }
    private Task Ensure(SchoolApplicationActor actor, CancellationToken ct) => actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
    private static SchoolApplicationException Invalid() => new(SchoolApplicationErrorCodes.Invalid);
    private static void Documents(IReadOnlyCollection<SchoolDocumentInput>? docs)
    {
        if (docs?.Count > 20 || docs?.Any(d => d is null || d.FileAssetId == Guid.Empty || string.IsNullOrWhiteSpace(d.Kind) || d.Kind.Length > 64) == true ||
            docs?.Select(d => d.FileAssetId).Distinct().Count() != docs?.Count) throw Invalid();
    }
}
