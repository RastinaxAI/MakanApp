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
    public const string SubmissionIncomplete = "SCHOOL_APPLICATION_SUBMISSION_INCOMPLETE";
    public const string CommercialTermsUnavailable = "SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE";
    public const string NotApproved = "SCHOOL_APPLICATION_NOT_APPROVED";
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
    bool CanSubmit, string? SubmissionLimitation, DateTime CreatedAtUtc, DateTime UpdatedAtUtc)
{
    public bool CanEdit { get; init; }
    public bool CanCancel { get; init; }
    public IReadOnlyList<string> MissingRequirements { get; init; } = [];
    public string NextAction { get; init; } = "None";
    public Guid? ApplicantUserId { get; init; }
    public SchoolApplicationProgress? Progress { get; init; }
}
public sealed record SchoolApplicationProgress(string CommercialState, Guid? OrderId, string? PaymentState,
    string ProvisioningState, Guid? ProvisioningRequestId, Guid? OrganizationId, Guid? SubscriptionId);
public sealed record SchoolApplicationHistoryResult(Guid Id, string Action, int Revision, long Version,
    SchoolApplicationStatus Status, string? Reason, DateTime OccurredAtUtc, Guid? ActorUserId);
public sealed record SchoolApplicationHistoryPage(IReadOnlyList<SchoolApplicationHistoryResult> Items, int Total, int Page, int PageSize);
public sealed record SchoolApplicationCommerceCommand(Guid ClientOperationId, long ExpectedVersion);
public sealed record SchoolOfferingCommand(Guid ClientOperationId, long ExpectedVersion, Guid PlanVersionId, string Reason);
public sealed record SchoolOfferingResult(SchoolApplicationKind Kind, Guid PlanVersionId, long Version);
public sealed record SchoolApplicationPage(IReadOnlyCollection<SchoolApplicationResult> Items, int Total, int Page, int PageSize);
public interface ISchoolApplicationStore
{
    Task<SchoolApplicationResult> CreateAsync(SchoolApplicationActor actor, CreateSchoolApplicationCommand command, CancellationToken ct);
    Task<SchoolApplicationResult> UpdateAsync(SchoolApplicationActor actor, Guid id, UpdateSchoolApplicationCommand command, CancellationToken ct);
    Task<SchoolApplicationResult> ChangeStatusAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool cancel, CancellationToken ct);
    Task<SchoolApplicationResult> ReviewAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool start, CancellationToken ct);
    Task<SchoolApplicationResult> ReadAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int? revision, CancellationToken ct);
    Task<SchoolApplicationPage> PageAsync(SchoolApplicationActor actor, bool reviewer, SchoolApplicationStatus? status, int page, int pageSize, CancellationToken ct);
    Task<SchoolApplicationHistoryPage> HistoryAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int page, int pageSize, CancellationToken ct);
    Task<SchoolOfferingResult> ConfigureOfferingAsync(SchoolApplicationActor actor, SchoolApplicationKind kind, SchoolOfferingCommand command, CancellationToken ct);
    Task<IReadOnlyList<SchoolOfferingResult>> OfferingsAsync(SchoolApplicationActor actor, CancellationToken ct);
}
public sealed class SchoolApplicationService(ISchoolApplicationStore store, CommercialActorAccess actors, IOrderPaymentService orders)
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
        Action(command);
        return await store.ChangeStatusAsync(actor, id, command, false, ct);
    }
    public async Task<SchoolApplicationResult> CancelAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, CancellationToken ct)
    {
        await Ensure(actor, ct); Action(command); return await store.ChangeStatusAsync(actor, id, command, true, ct);
    }
    public async Task<SchoolApplicationResult> ReviewAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool start, CancellationToken ct)
    {
        await Ensure(actor, ct);
        Action(command);
        if (string.IsNullOrWhiteSpace(command.ApplicantFeedback) || command.ApplicantFeedback.Length > 2000 ||
            !start && command.Decision is not (SchoolApplicationStatus.NeedsChanges or SchoolApplicationStatus.Approved or SchoolApplicationStatus.Rejected)) throw Invalid();
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
    public async Task<IReadOnlyList<SchoolOfferingResult>> OfferingsAsync(SchoolApplicationActor actor, CancellationToken ct)
    { await Ensure(actor, ct); return await store.OfferingsAsync(actor, ct); }
    public async Task<SchoolApplicationHistoryPage> HistoryAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int page, int pageSize, CancellationToken ct)
    {
        await Ensure(actor, ct); if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw Invalid();
        return await store.HistoryAsync(actor, id, reviewer, page, pageSize, ct);
    }
    public async Task<SchoolOfferingResult> ConfigureOfferingAsync(SchoolApplicationActor actor, SchoolApplicationKind kind, SchoolOfferingCommand command, CancellationToken ct)
    {
        await Ensure(actor, ct);
        if (!Enum.IsDefined(kind) || command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 0 || command.PlanVersionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500) throw Invalid();
        return await store.ConfigureOfferingAsync(actor, kind, command, ct);
    }
    public Task<CommercialQuoteDto> QuoteAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationCommerceCommand command, CancellationToken ct) =>
        orders.QuoteAsync(actor.UserId, actor.SessionId, CommerceCommand(id, command), ct, MakanApp.Domain.Commerce.SubscriptionOwnerType.Individual);
    public Task<CreateOrderResult> OrderAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationCommerceCommand command, CancellationToken ct) =>
        orders.CreateOrderAsync(actor.UserId, actor.SessionId, CommerceCommand(id, command), ct, MakanApp.Domain.Commerce.SubscriptionOwnerType.Individual);
    private static CreateOrderCommand CommerceCommand(Guid id, SchoolApplicationCommerceCommand command)
    {
        if (id == Guid.Empty || command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1) throw Invalid();
        return new(Guid.Empty, command.ClientOperationId) { SchoolApplicationId = id, SchoolApplicationVersion = command.ExpectedVersion };
    }
    private static void Action(SchoolApplicationActionCommand command)
    {
        if (command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1 || command.ApplicantFeedback?.Length > 2000) throw Invalid();
    }
    private static SchoolApplicationException Invalid() => new(SchoolApplicationErrorCodes.Invalid);
    private static void Documents(IReadOnlyCollection<SchoolDocumentInput>? docs)
    {
        if (docs?.Count > 20 || docs?.Any(d => d is null || d.FileAssetId == Guid.Empty || string.IsNullOrWhiteSpace(d.Kind) || d.Kind.Length > 64) == true ||
            docs?.Select(d => d.FileAssetId).Distinct().Count() != docs?.Count) throw Invalid();
    }
}
