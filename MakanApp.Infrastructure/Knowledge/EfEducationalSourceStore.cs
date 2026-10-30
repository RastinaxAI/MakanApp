using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Application.Outbox;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Knowledge;

public sealed partial class EfEducationalSourceStore(MakanDbContext db, EfCommercialStore commerce, ISchoolReadStore school,
    IPlatformPermissionResolver platform, IFileStorage storage, IOutbox outbox, TimeProvider clock) : IEducationalSourceStore
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static EducationalSourceException Error(string code) => new(code);
    private Task<bool> LibraryManager(Guid user, CancellationToken ct) => platform.HasPermissionAsync(user, PlatformPermissionKeys.LibrarySourcesManage, ct);
    private async Task<bool> Writer(SourceActor actor, EducationalSource source, CancellationToken ct) => source.Scope switch
    {
        EducationalSourceScope.Personal => source.OwnerUserId == actor.UserId,
        EducationalSourceScope.Library => await LibraryManager(actor.UserId, ct),
        EducationalSourceScope.Organization => actor.Organization is { } organizationContext && organizationContext.OrganizationId == source.OrganizationId && await school.CanManageOrganizationAsync(organizationContext, ct),
        EducationalSourceScope.Class => actor.Organization is { } classContext && classContext.OrganizationId == source.OrganizationId && await school.CanManageClassAsync(classContext, source.ClassId!.Value, ct),
        _ => false
    };
    private async Task<bool> Reader(SourceActor actor, EducationalSource source, CancellationToken ct)
    {
        if (await Writer(actor, source, ct)) return true;
        if (source.Status != EducationalSourceStatus.Active || !source.PublishedVersion.HasValue) return false;
        if (source.Scope == EducationalSourceScope.Library) return true;
        return source.Scope == EducationalSourceScope.Class && actor.Organization is { } context && context.OrganizationId == source.OrganizationId &&
            await school.CanStudyClassAsync(context, source.ClassId!.Value, ct);
    }

    public async Task<EducationalSourceResult> CreateAsync(SourceActor actor, CreateEducationalSourceCommand command, CancellationToken ct)
    {
        Reason(command.Reason);
        if (command.Scope == EducationalSourceScope.Class != command.ClassId.HasValue || command.ClassId == Guid.Empty) throw Error(SourceErrorCodes.Invalid);
        var org = command.Scope is EducationalSourceScope.Class or EducationalSourceScope.Organization ? actor.Organization?.OrganizationId : null;
        EducationalSource source;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { command.Scope, OrganizationId = org, command.ClassId,
            Title = command.Title?.Trim(), Description = command.Description?.Trim(), command.FileAssetId, Reason = command.Reason.Trim() })));
        try { source = EducationalSource.Create(command.Scope, actor.UserId, org, command.ClassId, command.ClientOperationId, hash, Now); }
        catch (ArgumentException) { throw Error(SourceErrorCodes.Invalid); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockOwner(actor.UserId, source, ct);
        if (!await Writer(actor, source, ct)) throw Error(SourceErrorCodes.NotFound);
        var old = await db.EducationalSources.SingleOrDefaultAsync(s => s.CreatedByUserId == actor.UserId && s.ClientOperationId == command.ClientOperationId, ct);
        if (old is not null)
        {
            if (old.CreationRequestHash != hash) throw Error(SourceErrorCodes.IdempotencyConflict);
            if (!await Reader(actor, old, ct)) throw Error(SourceErrorCodes.NotFound);
            await tx.CommitAsync(ct); return (await GetAsync(actor, old.Id, null, ct)) with { Replayed = true };
        }
        var file = await ValidateFile(actor, source, command.FileAssetId, ct);
        var version = NewVersion(source, file, actor.UserId, command.Title!, command.Description);
        if (source.Scope != EducationalSourceScope.Library)
        {
            var owner = source.Scope == EducationalSourceScope.Personal ? SubscriptionOwner.Individual(actor.UserId) : SubscriptionOwner.Organization(source.OrganizationId!.Value);
            try { await commerce.RequireCapacityAsync(owner, source.Scope == EducationalSourceScope.Personal ? EntitlementKey.PersonalSources : EntitlementKey.Academic, QuotaKey.SourceCount, 1, Now, ct); }
            catch (CommerceException ex) when (ex.Code == CommerceErrorCodes.QuotaExceeded) { throw Error(SourceErrorCodes.QuotaExceeded); }
        }
        db.EducationalSources.Add(source); db.EducationalSourceVersions.Add(version);
        Audit(actor, source, "create", command.Reason, null); Invalidate(source, true);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, source.Id, null, ct);
    }

    public async Task<EducationalSourceResult> MutateAsync(SourceActor actor, Guid id, UpdateSourceDraftCommand? metadata,
        CreateSourceVersionCommand? version, SourceMutationCommand? publish, SourceMutationCommand? archive, SourceVisibilityCommand? visibility, CancellationToken ct)
    {
        var initial = await db.EducationalSources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw Error(SourceErrorCodes.NotFound);
        if (!await Writer(actor, initial, ct)) throw Error(SourceErrorCodes.NotFound);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockOwner(actor.UserId, initial, ct);
        var source = await db.EducationalSources.FromSqlInterpolated($"SELECT * FROM [knowledge].[EducationalSources] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleAsync(ct);
        if (!await Writer(actor, source, ct)) throw Error(SourceErrorCodes.NotFound);
        var expected = metadata?.ExpectedVersion ?? version?.ExpectedVersion ?? publish?.ExpectedVersion ?? archive?.ExpectedVersion ?? visibility?.ExpectedVersion;
        var reason = metadata?.Reason ?? version?.Reason ?? publish?.Reason ?? archive?.Reason ?? visibility?.Reason;
        Reason(reason);
        var revision = await db.EducationalSourceVersions.SingleAsync(v => v.SourceId == id && v.Number == source.CurrentVersion, ct);
        if (!source.RowVersion.SequenceEqual(Decode(expected))) throw Error(SourceErrorCodes.Conflict);
        if (publish is not null && source.Status == EducationalSourceStatus.Active && source.PublishedVersion == source.CurrentVersion || archive is not null && source.Status == EducationalSourceStatus.Archived)
        { Decode(expected); await tx.CommitAsync(ct); return (await GetAsync(actor, id, null, ct)) with { Replayed = true }; }
        var before = $"{source.Status}/{source.CurrentVersion}/{source.PublishedVersion}/{source.Visibility}";
        try
        {
            if (metadata is not null) { source.Touch(Now); revision.UpdateDraft(metadata.Title, metadata.Description); }
            else if (version is not null)
            {
                source.AdvanceVersion(Now);
                var file = await ValidateFile(actor, source, version.FileAssetId, ct);
                db.EducationalSourceVersions.Add(NewVersion(source, file, actor.UserId, version.Title, version.Description));
            }
            else if (publish is not null)
            {
                var file = await db.FileAssets.SingleAsync(f => f.Id == revision.FileAssetId, ct);
                if (file.Status != FileAssetStatus.Ready) throw Error(SourceErrorCodes.NotReady);
                if (revision.SafetyStatus == SourceSafetyStatus.PendingValidation) revision.RecordFileValidation(SourceSafetyStatus.FileValidated);
                if (!revision.IsSourceReady(true)) throw Error(SourceErrorCodes.NotReady);
                file.Retain(Now);
                revision.Publish(true, Now); source.Publish(revision, Now);
            }
            else if (archive is not null) source.Archive(Now);
            else if (visibility is not null) source.SetVisibility(visibility.Visibility, Now);
            else throw Error(SourceErrorCodes.Invalid);
        }
        catch (ArgumentException) { throw Error(SourceErrorCodes.Invalid); }
        catch (InvalidOperationException) { throw Error(SourceErrorCodes.Conflict); }
        Audit(actor, source, metadata is not null ? "update-draft" : version is not null ? "version" : publish is not null ? "publish" : archive is not null ? "archive" : "visibility", reason!, before);
        Invalidate(source, source.PublishedVersion is null);
        db.Entry(source).Property(s => s.UpdatedAtUtc).IsModified = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Error(SourceErrorCodes.Conflict); }
        await tx.CommitAsync(ct); return await GetAsync(actor, id, null, ct);
    }

    private async Task LockOwner(Guid actor, EducationalSource source, CancellationToken ct)
    {
        // قفل Actor، یک OperationId یکسان را حتی میان دو Scope سریال می‌کند.
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(actor), ct);
        // مانند Academic: ابتدا Class، سپس مالک تجاری؛ هیچ شبکه یا پردازش فایل زیر این قفل نیست.
        if (source.ClassId is Guid classId)
            _ = await db.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={classId} AND [OrganizationId]={source.OrganizationId}").AsNoTracking().SingleOrDefaultAsync(ct)
                ?? throw Error(SourceErrorCodes.NotFound);
        await commerce.LockOwnerAsync(source.OrganizationId is Guid org ? SubscriptionOwner.Organization(org) : SubscriptionOwner.Individual(source.OwnerUserId ?? actor), ct);
    }
    private async Task<FileAsset> ValidateFile(SourceActor actor, EducationalSource source, Guid fileId, CancellationToken ct)
    {
        var file = await db.FileAssets.FromSqlInterpolated($"SELECT * FROM [storage].[FileAssets] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={fileId}").SingleOrDefaultAsync(ct);
        if (file is null || file.UploadedByUserId != actor.UserId || file.OrganizationId != source.OrganizationId ||
            file.Status is FileAssetStatus.Deleted or FileAssetStatus.Rejected ||
            file.ContentType is not ("application/pdf" or "image/jpeg" or "image/png" or "text/plain")) throw Error(SourceErrorCodes.NotAllowed);
        if (await db.EducationalSourceVersions.AnyAsync(v => v.FileAssetId == fileId && v.SourceId != source.Id, ct) ||
            await db.EducationalSourceVersions.AnyAsync(v => v.FileAssetId == fileId &&
                (v.SafetyStatus == SourceSafetyStatus.ReviewRequired || v.SafetyStatus == SourceSafetyStatus.Rejected), ct) ||
            await db.ClassLessonAttachments.AnyAsync(x => x.FileAssetId == fileId, ct) ||
            await db.SubmissionAttachments.AnyAsync(x => x.FileAssetId == fileId, ct) || await db.AnswerRevisionAttachments.AnyAsync(x => x.FileAssetId == fileId, ct) ||
            await db.MessageAttachments.AnyAsync(x => x.FileAssetId == fileId, ct) || await db.ModerationEvidenceAttachments.AnyAsync(x => x.FileAssetId == fileId, ct) ||
            await db.Users.AnyAsync(x => x.ProfilePhotoFileAssetId == fileId, ct) || await db.ReportExportJobs.AnyAsync(x => x.OutputFileAssetId == fileId || x.Id == fileId, ct))
            throw Error(SourceErrorCodes.NotAllowed);
        // نگهداری مرجع از حذف blob پس از Archive یا تعویض نسخه جلوگیری می‌کند.
        if (file.Status == FileAssetStatus.Ready) file.Retain(Now);
        db.Entry(file).Property(f => f.Status).IsModified = true;
        return file;
    }
    private EducationalSourceVersion NewVersion(EducationalSource source, FileAsset file, Guid actor, string title, string? description)
    {
        try
        {
            var version = EducationalSourceVersion.Create(source.Id, source.CurrentVersion, file.Id, actor, title, description,
                file.ContentType.StartsWith("image/", StringComparison.Ordinal) ? EducationalSourceType.Image : EducationalSourceType.Document, Now);
            if (file.Status == FileAssetStatus.Ready) version.RecordFileValidation(SourceSafetyStatus.FileValidated);
            return version;
        }
        catch (ArgumentException) { throw Error(SourceErrorCodes.Invalid); }
    }
    private static void Reason(string? reason)
    { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw Error(SourceErrorCodes.Invalid); }
    private static byte[] Decode(string? value)
    {
        try { var result = Convert.FromBase64String(value ?? ""); if (result.Length == 8) return result; } catch (FormatException) { }
        throw Error(SourceErrorCodes.Invalid);
    }
    private void Audit(SourceActor actor, EducationalSource source, string action, string reason, string? before) =>
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, null, actor.SessionId, "knowledge.source." + action, "EducationalSource", source.Id.ToString(),
            source.OrganizationId, reason.Trim(), Now, Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"), AdminAuditResult.Succeeded,
            before, $"{source.Status}/{source.CurrentVersion}/{source.PublishedVersion}/{source.Visibility}"));
    private void Invalidate(EducationalSource source, bool staffOnly)
    {
        var envelope = source.Scope switch
        {
            EducationalSourceScope.Personal => new InvalidationEnvelope("knowledge.source", ResourceId: source.Id, TargetUserId: source.OwnerUserId),
            EducationalSourceScope.Library => staffOnly
                ? new InvalidationEnvelope("knowledge.library", TargetUserId: source.CreatedByUserId)
                : new InvalidationEnvelope("knowledge.library"),
            _ => new InvalidationEnvelope("knowledge.source", source.OrganizationId, source.ClassId, source.Id, StaffOnly: staffOnly)
        };
        outbox.Enqueue(new(Guid.NewGuid(), RealtimeInvalidationWriter.MessageType,
            JsonSerializer.Serialize(envelope with { TraceId = Activity.Current?.TraceId.ToString() }), Now));
    }
}
