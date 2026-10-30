using MakanApp.Application.Organization;
using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Knowledge;

public sealed class EducationalSourceService(IAccountOperationContextResolver accounts, IAccessContextResolver contexts, IEducationalSourceStore store)
{
    private async Task<SourceActor> Actor(Guid user, Guid session, bool organization, CancellationToken ct)
    {
        var account = await accounts.PersonalAsync(user, session, ct);
        AccessContext? selected = null;
        if (organization)
        {
            try { selected = await contexts.ResolveAsync(user, session, ct); }
            catch (OrganizationException) { selected = null; }
            catch (MakanApp.Application.Guardian.GuardianException) { selected = null; }
        }
        return new(user, session, account, selected);
    }
    // بافت عملیات شخصی انتخاب role یا payer نشست را تغییر نمی‌دهد.
    public async Task<EducationalSourceResult> CreateAsync(Guid user, Guid session, CreateEducationalSourceCommand command, CancellationToken ct)
    {
        if (!Enum.IsDefined(command.Scope) || command.ClientOperationId == Guid.Empty || command.FileAssetId == Guid.Empty) throw new EducationalSourceException(SourceErrorCodes.Invalid);
        return await store.CreateAsync(await Actor(user, session, command.Scope is EducationalSourceScope.Class or EducationalSourceScope.Organization, ct), command, ct);
    }
    public async Task<SourcePage> ListAsync(Guid user, Guid session, SourceCatalogQuery query, bool libraryManagement, CancellationToken ct)
    {
        if (!Enum.IsDefined(query.Scope) || query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100 || query.Search?.Length > 100 ||
            query.Status.HasValue && !Enum.IsDefined(query.Status.Value) || query.SourceType.HasValue && !Enum.IsDefined(query.SourceType.Value) ||
            query.Scope == EducationalSourceScope.Class != query.ClassId.HasValue || query.ClassId == Guid.Empty)
            throw new EducationalSourceException(SourceErrorCodes.Invalid);
        return await store.ListAsync(await Actor(user, session, query.Scope is EducationalSourceScope.Class or EducationalSourceScope.Organization, ct), query, libraryManagement, ct);
    }
    private Task<SourceActor> ResourceActor(Guid user, Guid session, CancellationToken ct) => Actor(user, session, true, ct);
    public async Task<EducationalSourceResult> GetAsync(Guid user, Guid session, Guid id, int? revision, CancellationToken ct) =>
        await store.GetAsync(await ResourceActor(user, session, ct), id, revision, ct);
    public async Task<MakanApp.Application.Storage.FileDownloadResult> DownloadAsync(Guid user, Guid session, Guid id, int? revision, CancellationToken ct) =>
        await store.DownloadAsync(await ResourceActor(user, session, ct), id, revision, ct);
    public async Task<EducationalSourceResult> MutateAsync(Guid user, Guid session, Guid id, UpdateSourceDraftCommand? metadata,
        CreateSourceVersionCommand? version, SourceMutationCommand? publish, SourceMutationCommand? archive, SourceVisibilityCommand? visibility, CancellationToken ct) =>
        await store.MutateAsync(await ResourceActor(user, session, ct), id, metadata, version, publish, archive, visibility, ct);
}
