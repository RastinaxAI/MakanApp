using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Knowledge;

public sealed class KnowledgeProcessingProjection(EducationalSourceService sources, KnowledgeIntegrationService knowledge,
    IKnowledgeRuntimeAvailability runtime, KnowledgeAuthorizationService authorization, CommercialSubscriptionService commerce)
{
    public async Task<EducationalSourceResult> ReadAsync(Guid user, Guid session, Guid id, int? revision, string correlation, CancellationToken ct)
    {
        var source = await sources.GetAsync(user, session, id, revision, ct);
        try { runtime.EnsureEnabled(); }
        catch (AiServiceException error) when (error.Code == AiServiceErrorCodes.FeatureDisabled) { return source; }
        if (source.Scope == EducationalSourceScope.Library || !source.SourceReady || source.PublicationStatus != SourcePublicationStatus.Published)
            return source;
        var identity = new KnowledgeSourceVersion(id, source.Revision);
        var status = await knowledge.StatusAsync(user, session, identity, false, correlation, ct);
        var context = await authorization.ResolveAsync(user, session, [identity], null, ct);
        var quota = await commerce.PreflightForAssistantAsync(user, session, context.Commerce, context.Actor.Organization ?? context.Actor.Account, ct);
        var current = await sources.GetAsync(user, session, id, source.Revision, ct);
        var processing = status.State switch
        {
            "Ready" => SourceProcessingStatus.Ready,
            "NotIngested" or "Deleted" or "Superseded" => SourceProcessingStatus.NotRequested,
            "Queued" or "ReindexRequired" => SourceProcessingStatus.Pending,
            var value when value.EndsWith("Failed", StringComparison.Ordinal) => SourceProcessingStatus.Failed,
            _ => SourceProcessingStatus.Processing
        };
        // این projection، نتیجهٔ status همین درخواست است؛ cache یا authority تازه‌ای در SQL ایجاد نمی‌کند.
        return current with
        {
            ProcessingStatus = processing, ProcessingFailureCode = processing == SourceProcessingStatus.Failed ? "SOURCE_PROCESSING_FAILED" : null,
            AiReady = status.State == "Ready", Capabilities = current.Capabilities with { CanUseForAi = status.State == "Ready" && quota.Allowed }
        };
    }
}
