using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Knowledge;

public sealed record KnowledgeAuthorizedSources(SourceActor Actor, IReadOnlyList<EducationalSourceResult> Sources,
    IReadOnlyList<KnowledgeSourceVersion> Versions, AiQuotaPreflightCommand Commerce);

public sealed class KnowledgeAuthorizationService(IAccountOperationContextResolver accounts, IAccessContextResolver contexts,
    IEducationalSourceStore sources, IAiSourceSelectionStore pins)
{
    public async Task<KnowledgeAuthorizedSources> ResolveAsync(Guid user, Guid session,
        IReadOnlyList<KnowledgeSourceVersion>? requested, AiPayerSelection? payer, CancellationToken ct)
    {
        if (requested is null || requested.Count is < 1 or > AiSourceSelection.MaximumSources ||
            requested.Any(x => x is null || x.SourceId == Guid.Empty || x.Version < 1) ||
            requested.Select(x => x.SourceId).Distinct().Count() != requested.Count || payer.HasValue && !Enum.IsDefined(payer.Value))
            throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
        var account = await accounts.PersonalAsync(user, session, ct);
        var current = await contexts.ResolveAsync(user, session, ct);
        var organization = current.WorkspaceType == WorkspaceType.Organization ? current : null;
        var actor = new SourceActor(user, session, account, organization);
        var results = new List<EducationalSourceResult>();
        foreach (var version in requested.OrderBy(x => x.SourceId))
        {
            var source = await sources.GetAsync(actor, version.SourceId, version.Version, ct);
            if (organization?.AssistantClassId is Guid selectedClass && source.Scope == EducationalSourceScope.Class && source.ClassId != selectedClass ||
                source.Scope == EducationalSourceScope.Library || !source.Capabilities.CanRead ||
                source.Scope == EducationalSourceScope.Personal && source.OwnerUserId != user ||
                source.Scope is EducationalSourceScope.Class or EducationalSourceScope.Organization &&
                    (organization?.OrganizationId is null || organization.OrganizationId != source.OrganizationId))
                throw new EducationalSourceException(SourceErrorCodes.NotFound);
            if (source.Status != EducationalSourceStatus.Active || source.PublicationStatus != SourcePublicationStatus.Published ||
                !source.SourceReady || !source.FileReady || source.SafetyStatus != SourceSafetyStatus.FileValidated ||
                source.Revision != version.Version || !source.Capabilities.CanDownload)
                throw new EducationalSourceException(SourceErrorCodes.NotReady);
            results.Add(source);
        }
        var quota = organization is null ? new AiQuotaPreflightCommand(CommerceContextType.Personal, user, payer)
            : new AiQuotaPreflightCommand(CommerceContextType.Organization, organization.OrganizationId, payer);
        return new(actor, results, results.Select(x => new KnowledgeSourceVersion(x.SourceId, x.Revision)).ToArray(), quota);
    }

    public async Task PinAsync(KnowledgeAuthorizedSources authorized, Guid operation, object payload, CancellationToken ct)
    {
        if (operation == Guid.Empty) throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
        var hash = Fingerprint(authorized, payload);
        var old = await pins.FindPinnedAsync(authorized.Actor.UserId, operation, hash, ct);
        if (old is not null && !old.OrderBy(x => x.SourceId).Select(x => new KnowledgeSourceVersion(x.SourceId, x.Number)).SequenceEqual(authorized.Versions))
            throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
        var saved = await pins.PinAsync(authorized.Actor.UserId, operation, hash,
            authorized.Versions.Select(x => new AiSourceVersionIdentity(x.SourceId, x.Version)).ToArray(), ct);
        if (!saved.Pins.OrderBy(x => x.SourceId).Select(x => new KnowledgeSourceVersion(x.SourceId, x.Number)).SequenceEqual(authorized.Versions))
            throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
    }

    public async Task ReauthorizeAsync(KnowledgeAuthorizedSources before, CancellationToken ct)
    {
        var current = await ResolveAsync(before.Actor.UserId, before.Actor.SessionId, before.Versions, before.Commerce.Payer, ct);
        if (before.Actor.Account != current.Actor.Account || before.Actor.Organization != current.Actor.Organization ||
            !before.Sources.Select(x => (x.SourceId, x.Revision, x.Version, x.File?.FileAssetId))
                .SequenceEqual(current.Sources.Select(x => (x.SourceId, x.Revision, x.Version, x.File?.FileAssetId))))
            throw AiSourceSelection.Error(AiSourceErrorCodes.ContextMismatch);
    }

    public static byte[] Fingerprint(KnowledgeAuthorizedSources context, object payload) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Kind = "knowledge-v1", context.Actor.UserId, context.Actor.Organization?.OrganizationId,
        context.Actor.Organization?.MembershipId, context.Actor.Organization?.ActiveRole, context.Commerce,
        context.Versions, Payload = payload
    }));
}
