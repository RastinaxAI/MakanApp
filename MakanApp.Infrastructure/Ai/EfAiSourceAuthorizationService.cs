using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class EfAiSourceAuthorizationService(IEducationalSourceStore sources, MakanDbContext db) : IAiSourceAuthorizationService
{
    public async Task<IReadOnlyList<AuthorizedAiSourceManifest>> AuthorizeAsync(AiSourceContext context,
        IReadOnlyList<Guid> sourceIds, IReadOnlyList<AiSourceVersionIdentity>? pinned, CancellationToken ct)
    {
        var result = new List<AuthorizedAiSourceManifest>();
        foreach (var id in sourceIds)
        {
            EducationalSourceResult source;
            try
            {
                source = await sources.GetAsync(context.Actor, id, null, ct);
                AiSourcePolicy.EnsureContext(source.Scope, source.OwnerUserId, source.OrganizationId, source.ClassId, context);
                var number = pinned is null ? source.PublishedVersion : pinned.SingleOrDefault(p => p.SourceId == id)?.Number;
                if (number is null) throw AiSourceSelection.Error(AiSourceErrorCodes.NotReady);
                source = await sources.GetAsync(context.Actor, id, number, ct);
            }
            catch (EducationalSourceException e) when (e.Code == SourceErrorCodes.NotFound)
            { throw AiSourceSelection.Error(AiSourceErrorCodes.NotFound); }
            AiSourcePolicy.EnsureReady(source.Status, source.PublicationStatus, source.SourceReady, source.ProcessingStatus);
            var revision = await db.EducationalSourceVersions.AsNoTracking().Where(v => v.SourceId == id && v.Number == source.Revision)
                .Select(v => v.RowVersion).SingleAsync(ct);
            result.Add(new(new(id, source.Revision), source.Scope, source.OwnerUserId ?? source.OrganizationId, source.Title,
                "RetrievalUnavailable", source.Version + ":" + Convert.ToBase64String(revision)));
        }
        return result;
    }
}
