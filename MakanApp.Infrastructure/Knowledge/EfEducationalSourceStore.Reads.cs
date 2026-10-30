using MakanApp.Application.Academic;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Knowledge;

public sealed partial class EfEducationalSourceStore
{
    public async Task<EducationalSourceResult> GetAsync(SourceActor actor, Guid id, int? revision, CancellationToken ct)
    {
        if (revision is < 1) throw Error(SourceErrorCodes.Invalid);
        var source = await db.EducationalSources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw Error(SourceErrorCodes.NotFound);
        if (!await Reader(actor, source, ct)) throw Error(SourceErrorCodes.NotFound);
        var writer = await Writer(actor, source, ct);
        var number = revision ?? (writer ? source.CurrentVersion : source.PublishedVersion);
        var version = await db.EducationalSourceVersions.AsNoTracking().SingleOrDefaultAsync(v => v.SourceId == id && v.Number == number, ct) ?? throw Error(SourceErrorCodes.NotFound);
        if (!writer && version.PublicationStatus != SourcePublicationStatus.Published) throw Error(SourceErrorCodes.NotFound);
        return await Result(actor, source, version, writer, ct);
    }
    public async Task<SourcePage> ListAsync(SourceActor actor, SourceCatalogQuery query, bool libraryManagement, CancellationToken ct)
    {
        var scope = query.Scope;
        var queryable = db.EducationalSources.AsNoTracking().Where(s => s.Scope == scope);
        var drafts = scope == EducationalSourceScope.Personal;
        if (scope == EducationalSourceScope.Personal) queryable = queryable.Where(s => s.OwnerUserId == actor.UserId);
        else if (scope == EducationalSourceScope.Library)
        {
            if (libraryManagement && !await LibraryManager(actor.UserId, ct)) throw Error(SourceErrorCodes.NotAllowed);
            drafts = libraryManagement;
            if (!drafts) queryable = queryable.Where(s => s.Visibility == SourceVisibility.Public && s.Status == EducationalSourceStatus.Active && s.PublishedVersion != null);
        }
        else
        {
            var context = actor.Organization;
            if (context?.OrganizationId is not Guid org) throw Error(SourceErrorCodes.NotFound);
            queryable = queryable.Where(s => s.OrganizationId == org && s.ClassId == query.ClassId);
            drafts = scope == EducationalSourceScope.Organization ? await school.CanManageOrganizationAsync(context, ct)
                : await school.CanManageClassAsync(context, query.ClassId!.Value, ct);
            if (!drafts && (scope != EducationalSourceScope.Class || !await school.CanStudyClassAsync(context, query.ClassId!.Value, ct))) throw Error(SourceErrorCodes.NotFound);
            if (!drafts) queryable = queryable.Where(s => s.Status == EducationalSourceStatus.Active && s.PublishedVersion != null);
        }
        if (query.Status.HasValue) queryable = queryable.Where(s => s.Status == query.Status);
        var versions = db.EducationalSourceVersions.AsNoTracking();
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : EducationalSourceVersion.NormalizeTitle(query.Search);
        var list = from source in queryable
                   join version in versions on new { SourceId = source.Id, Number = (int?)(drafts ? source.CurrentVersion : source.PublishedVersion) }
                       equals new { version.SourceId, Number = (int?)version.Number }
                   where (search == null || version.NormalizedTitle.Contains(search)) && (!query.SourceType.HasValue || version.SourceType == query.SourceType)
                   select new { Source = source, Version = version };
        var count = await list.CountAsync(ct);
        var rows = await list.OrderByDescending(x => x.Source.CreatedAtUtc).ThenBy(x => x.Source.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var items = new List<EducationalSourceResult>();
        foreach (var row in rows) items.Add(await Result(actor, row.Source, row.Version, await Writer(actor, row.Source, ct), ct));
        return new(query.Page, query.PageSize, count, items);
    }
    private async Task<EducationalSourceResult> Result(SourceActor actor, EducationalSource source, EducationalSourceVersion revision, bool writer, CancellationToken ct)
    {
        var file = await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == revision.FileAssetId, ct);
        var ready = file.Status == FileAssetStatus.Ready;
        var safety = file.Status == FileAssetStatus.Rejected ? SourceSafetyStatus.Rejected
            : ready && revision.SafetyStatus == SourceSafetyStatus.PendingValidation ? SourceSafetyStatus.FileValidated : revision.SafetyStatus;
        var sourceReady = source.Status == EducationalSourceStatus.Active && ready && safety == SourceSafetyStatus.FileValidated;
        var download = sourceReady && (source.Scope != EducationalSourceScope.Library || writer);
        var current = revision.Number == source.CurrentVersion;
        var editable = writer && current && source.Status == EducationalSourceStatus.Active && revision.PublicationStatus == SourcePublicationStatus.Draft;
        return new(source.Id, source.Scope, source.OwnerUserId, source.OrganizationId, source.ClassId, source.Status, source.Visibility,
            writer ? source.CurrentVersion : source.PublishedVersion ?? revision.Number, source.PublishedVersion, revision.Number, revision.Title, revision.Description, revision.SourceType, revision.PublicationStatus,
            safety, revision.ProcessingStatus, revision.ProcessingFailureCode, ready, sourceReady, false,
            new SourceCapabilities(true, editable, editable && sourceReady, writer && source.Status == EducationalSourceStatus.Active, download, false)
            { CanCreateVersion = writer && source.Status == EducationalSourceStatus.Active,
                CanChangeVisibility = writer && source.Scope == EducationalSourceScope.Library && source.Status == EducationalSourceStatus.Active },
            source.Scope == EducationalSourceScope.Library && !writer ? null : new SourceFileMetadata(file.Id, file.OriginalFileName, file.ContentType, file.SizeBytes, file.Status.ToString()),
            source.CreatedByUserId, source.CreatedAtUtc, revision.PublishedAtUtc, Convert.ToBase64String(source.RowVersion));
    }
    public async Task<FileDownloadResult> DownloadAsync(SourceActor actor, Guid id, int? revision, CancellationToken ct)
    {
        var result = await GetAsync(actor, id, revision, ct);
        if (!result.Capabilities.CanDownload) throw Error(result.Scope == EducationalSourceScope.Library ? SourceErrorCodes.NotAllowed : SourceErrorCodes.NotReady);
        var file = await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == result.File!.FileAssetId, ct);
        var stream = await storage.OpenReadAsync(file.StorageKey, ct);
        return new(stream, file.ContentType, file.OriginalFileName, file.SizeBytes);
    }
    public async Task<bool> CanReadFileAsync(Guid fileId, AccessContext context, CancellationToken ct)
    {
        var versions = await db.EducationalSourceVersions.AsNoTracking().Where(v => v.FileAssetId == fileId).Select(v => new { v.SourceId, v.Number }).ToArrayAsync(ct);
        var actor = new SourceActor(context.UserId, context.SessionId, context, context);
        foreach (var version in versions)
        {
            try { if ((await GetAsync(actor, version.SourceId, version.Number, ct)).Capabilities.CanDownload) return true; }
            catch (EducationalSourceException e) when (e.Code == SourceErrorCodes.NotFound) { }
        }
        return false;
    }
}
