using System.Data;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed class EfSchoolApplicationStore(MakanDbContext db, CommercialActorAccess actors, IPlatformPermissionResolver permissions,
    IFileAssetBoundAccessResolver files, IIdentitySecurity security, AddressBookProtection protection, TimeProvider clock,
    MakanApp.Infrastructure.Notification.ProductNotificationWriter notifications) : ISchoolApplicationStore
{
    public async Task<SchoolApplicationResult> CreateAsync(SchoolApplicationActor actor, CreateSchoolApplicationCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct); var hash = Hash(actor, "create", command);
        var replay = await Receipt(actor, command.ClientOperationId, hash, null, ct);
        if (replay is not null) { var repeated = await ReadAsync(actor, replay.ApplicationId, false, null, ct); await tx.CommitAsync(ct); return repeated; }
        SchoolApplication app;
        try { app = SchoolApplication.Create(actor.UserId, command.Kind, command.Data, Now()); }
        catch (ArgumentException) { throw Error(SchoolApplicationErrorCodes.Invalid); }
        db.SchoolApplications.Add(app); await Revision(actor, app, command.Documents, ct);
        Complete(actor, app, command.ClientOperationId, hash, "school_application.Created");
        await db.SaveChangesAsync(ct); var result = await Map(app, null, ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<SchoolApplicationResult> UpdateAsync(SchoolApplicationActor actor, Guid id, UpdateSchoolApplicationCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct); var app = await Locked(actor, id, false, ct); var hash = Hash(actor, $"update:{id}", command);
        if (await Receipt(actor, command.ClientOperationId, hash, id, ct) is null)
        {
            if (app.Version != command.ExpectedVersion || app.Status is not (SchoolApplicationStatus.Draft or SchoolApplicationStatus.NeedsChanges)) throw Error(SchoolApplicationErrorCodes.Conflict);
            try { app.UpdateDraft(command.Data, Now()); } catch (ArgumentException) { throw Error(SchoolApplicationErrorCodes.Invalid); }
            await Revision(actor, app, command.Documents, ct); Complete(actor, app, command.ClientOperationId, hash, "school_application.Revised");
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(app, null, ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<SchoolApplicationResult> ReviewAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool start, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct); await Reviewer(actor, true, ct);
        var app = await Locked(actor, id, true, ct); var hash = Hash(actor, $"review:{id}:{start}", command);
        if (await Receipt(actor, command.ClientOperationId, hash, id, ct) is null)
        {
            if (app.Version != command.ExpectedVersion || (start ? app.Status != SchoolApplicationStatus.Submitted : app.Status != SchoolApplicationStatus.UnderReview))
                throw Error(SchoolApplicationErrorCodes.Conflict);
            if (start) app.StartReview(Now()); else app.Decide(command.Decision!.Value, Now());
            db.SchoolApplicationDecisions.Add(SchoolApplicationDecision.Create(app, actor.UserId, command.ApplicantFeedback?.Trim() ?? "بررسی آغاز شد", Now()));
            Complete(actor, app, command.ClientOperationId, hash, start ? "school_application.ReviewStarted" : "school_application.Decided");
            // تصمیم بررسی، پرداخت یا ساخت Organization نیست؛ هیچ Membership یا RoleAssignment ساخته نمی‌شود.
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(app, null, ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<SchoolApplicationResult> ReadAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int? revision, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); if (reviewer) await Reviewer(actor, false, ct);
        var app = await db.SchoolApplications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id &&
            (reviewer ? a.Status != SchoolApplicationStatus.Draft : a.ApplicantUserId == actor.UserId), ct) ?? throw Error(SchoolApplicationErrorCodes.NotFound);
        return await Map(app, revision, ct);
    }
    public async Task<SchoolApplicationPage> PageAsync(SchoolApplicationActor actor, bool reviewer, SchoolApplicationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); if (reviewer) await Reviewer(actor, false, ct);
        var query = db.SchoolApplications.AsNoTracking().Where(a => reviewer ? a.Status != SchoolApplicationStatus.Draft : a.ApplicantUserId == actor.UserId);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.UpdatedAtUtc).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var results = new List<SchoolApplicationResult>(); foreach (var app in rows) results.Add(await Map(app, null, ct));
        return new(results, total, page, pageSize);
    }
    private async Task Revision(SchoolApplicationActor actor, SchoolApplication app, IReadOnlyCollection<SchoolDocumentInput>? documents, CancellationToken ct)
    {
        db.SchoolApplicationRevisions.Add(SchoolApplicationRevision.Create(app.Id, app.Revision,
            protection.Protect(app.ApplicantUserId, $"school-revision:{app.Id}:{app.Revision}", JsonSerializer.Serialize(app.DraftData())), Now()));
        var currentDocuments = documents ?? (app.Revision > 1
            ? await db.SchoolApplicationDocuments.AsNoTracking().Where(d => d.ApplicationId == app.Id && d.Revision == app.Revision - 1)
                .Select(d => new SchoolDocumentInput(d.FileAssetId, d.Kind)).ToArrayAsync(ct) : []);
        foreach (var document in currentDocuments)
        {
            var file = await db.FileAssets.FromSqlInterpolated($"SELECT * FROM [storage].[FileAssets] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={document.FileAssetId}").SingleOrDefaultAsync(ct);
            var reused = await db.SchoolApplicationDocuments.AnyAsync(d => d.ApplicationId == app.Id && d.FileAssetId == document.FileAssetId, ct);
            if (file is null || file.UploadedByUserId != actor.UserId || file.OrganizationId.HasValue || file.PersonalClassId.HasValue || file.Status != FileAssetStatus.Ready ||
                !reused && (file.UnattachedExpiresAtUtc <= Now() || await files.IsBoundAsync(file.Id, ct))) throw Error(SchoolApplicationErrorCodes.FileUnavailable);
            file.Retain(Now()); db.SchoolApplicationDocuments.Add(SchoolApplicationDocument.Create(app.Id, app.Revision, file.Id, document.Kind.Trim()));
        }
    }
    private async Task<SchoolApplicationResult> Map(SchoolApplication app, int? requestedRevision, CancellationToken ct)
    {
        var revision = requestedRevision ?? app.Revision;
        SchoolApplicationDraftData data;
        if (revision == app.Revision) data = app.DraftData();
        else
        {
            var history = await db.SchoolApplicationRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.ApplicationId == app.Id && r.Number == revision, ct)
                ?? throw Error(SchoolApplicationErrorCodes.NotFound);
            data = JsonSerializer.Deserialize<SchoolApplicationDraftData>(protection.Unprotect(app.ApplicantUserId, $"school-revision:{app.Id}:{revision}", history.ProtectedSnapshot))!;
        }
        var docs = await db.SchoolApplicationDocuments.AsNoTracking().Where(d => d.ApplicationId == app.Id && d.Revision == revision)
            .Select(d => new SchoolDocumentInput(d.FileAssetId, d.Kind)).ToArrayAsync(ct);
        var decisions = await db.SchoolApplicationDecisions.AsNoTracking().Where(d => d.ApplicationId == app.Id).OrderByDescending(d => d.OccurredAtUtc).ThenBy(d => d.Id).Take(100)
            .Select(d => new SchoolApplicationDecisionResult(d.Id, d.Revision, d.Status, d.ApplicantFeedback, d.OccurredAtUtc)).ToArrayAsync(ct);
        return new(app.Id, app.Kind, app.Status, revision, app.Version, data, docs, decisions, false,
            SchoolApplicationErrorCodes.ProductDecisionRequired, app.CreatedAtUtc, app.UpdatedAtUtc);
    }
    private async Task LockActor(SchoolApplicationActor actor, CancellationToken ct)
    {
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor.UserId}").AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
    }
    private async Task<SchoolApplication> Locked(SchoolApplicationActor actor, Guid id, bool reviewer, CancellationToken ct) =>
        await db.SchoolApplications.FromSqlInterpolated($"SELECT * FROM [organization].[SchoolApplications] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
            .SingleOrDefaultAsync(a => reviewer ? a.Status != SchoolApplicationStatus.Draft : a.ApplicantUserId == actor.UserId, ct) ?? throw Error(SchoolApplicationErrorCodes.NotFound);
    private async Task Reviewer(SchoolApplicationActor actor, bool write, CancellationToken ct)
    {
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.SchoolApplicationsRead, ct) ||
            write && !await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.SchoolApplicationsReview, ct)) throw Error(SchoolApplicationErrorCodes.Forbidden);
    }
    private async Task<SchoolApplicationOperation?> Receipt(SchoolApplicationActor actor, Guid operation, string hash, Guid? app, CancellationToken ct)
    {
        var receipt = await db.SchoolApplicationOperations.AsNoTracking().SingleOrDefaultAsync(o => o.ActorUserId == actor.UserId && o.ClientOperationId == operation, ct);
        if (receipt is not null && (receipt.Fingerprint != hash || app.HasValue && receipt.ApplicationId != app)) throw Error(SchoolApplicationErrorCodes.Conflict);
        return receipt;
    }
    private void Complete(SchoolApplicationActor actor, SchoolApplication app, Guid operation, string hash, string action)
    {
        if ((action is "school_application.ReviewStarted" or "school_application.Decided") && actor.UserId != app.ApplicantUserId)
            notifications.SchoolUpdated(operation, app.Id, app.ApplicantUserId);
        db.SchoolApplicationOperations.Add(SchoolApplicationOperation.Create(actor.UserId, operation, app.Id, hash, Now()));
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, null, actor.SessionId, action, "SchoolApplication", app.Id.ToString(), null,
            "عملیات نسخه‌دار درخواست تأسیس توسط عامل مجاز", Now(), actor.CorrelationId, AdminAuditResult.Succeeded, null, null));
    }
    private string Hash(SchoolApplicationActor actor, string action, object command) => Convert.ToHexString(security.HashSessionToken("school-application:v1:" + actor.UserId + ":" + action + ":" + JsonSerializer.Serialize(command)));
    private static SchoolApplicationException Error(string code) => new(code);
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
