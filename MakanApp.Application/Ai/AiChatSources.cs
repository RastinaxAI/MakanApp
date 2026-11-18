using MakanApp.Application.Knowledge;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Ai;

public sealed record AiChatSourcePlan(AiChatMode Mode, AiSourceIntent Intent, KnowledgeAuthorizedSources? Authorized, string? FallbackReason = null)
{
    public IReadOnlyList<KnowledgeSourceVersion> Versions => Authorized?.Versions ?? [];
    public bool AllowsFallback => Mode == AiChatMode.Auto && Intent == AiSourceIntent.General;
}
public sealed record AiChatSourceAnswer(KnowledgeRagResponse? Answer, AiChatProvenance Provenance);

public interface IAiChatSources
{
    Task<AiChatSourcePlan> PrepareAsync(Guid user, Guid session, Guid operation, byte[] hash,
        SendAiChatCommand command, AiPayerSelection? payer, CancellationToken ct);
    Task ReauthorizeHistoryAsync(Guid user, Guid session, IReadOnlyList<AiConversationSourcePin> versions,
        AiPayerSelection? payer, CancellationToken ct);
    Task ReauthorizeSelectionAsync(AiChatSourcePlan plan, CancellationToken ct);
    Task<AiChatSourceAnswer> AnswerAsync(AiChatSourcePlan plan, string query, string requestId, string correlation,
        bool streaming, CancellationToken ct);
}

public sealed class AiChatSources(KnowledgeAuthorizationService authorization, IAiSourceSelectionStore pins,
    IAiKnowledgeClient knowledge, IKnowledgeRuntimeAvailability runtime) : IAiChatSources
{
    public async Task<AiChatSourcePlan> PrepareAsync(Guid user, Guid session, Guid operation, byte[] hash,
        SendAiChatCommand command, AiPayerSelection? payer, CancellationToken ct)
    {
        var ids = AiSourceSelection.Canonicalize(command.SelectedSourceIds);
        var exact = command.SelectedSources?.OrderBy(x => x.SourceId).ToArray();
        if (exact is not null && (exact.Length > AiSourceSelection.MaximumSources ||
            exact.Any(x => x is null || x.SourceId == Guid.Empty || x.Version < 1) || exact.Select(x => x.SourceId).Distinct().Count() != exact.Length))
            throw Invalid();
        if (ids.Length != 0 && exact is { Length: > 0 } && !ids.SequenceEqual(exact.Select(x => x.SourceId))) throw Invalid();
        if (exact is { Length: 0 }) exact = null;
        var selected = exact is { Length: > 0 } ? exact.Select(x => x.SourceId).ToArray() : ids;
        var mode = command.Mode ?? (selected.Length == 0 ? AiChatMode.General : AiChatMode.Auto);
        var intent = command.SourceIntent ?? (selected.Length == 0 ? AiSourceIntent.General : AiSourceIntent.SourceFocused);
        if (!Enum.IsDefined(mode) || !Enum.IsDefined(intent)) throw Invalid();
        if (mode == AiChatMode.General || selected.Length == 0)
        {
            if (mode == AiChatMode.SourcesOnly || mode == AiChatMode.Auto && intent == AiSourceIntent.SourceFocused) throw Invalid();
            await pins.EnsureUnselectedAsync(user, operation, ct);
            return new(mode, intent, null);
        }
        var old = await pins.FindPinnedAsync(user, operation, hash, ct);
        var versions = old?.Select(x => new KnowledgeSourceVersion(x.SourceId, x.Number)).ToArray()
            ?? exact ?? (await authorization.PublishedVersionsAsync(user, session, selected, ct)).ToArray();
        if (!versions.Select(x => x.SourceId).SequenceEqual(selected) || exact is { Length: > 0 } && !versions.SequenceEqual(exact))
            throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
        var allowed = await authorization.ResolveForChatAsync(user, session, versions, payer, ct);
        var saved = await pins.PinAsync(user, operation, hash, versions.Select(x => new AiSourceVersionIdentity(x.SourceId, x.Version)).ToArray(), ct);
        if (!saved.Pins.Select(x => new KnowledgeSourceVersion(x.SourceId, x.Number)).SequenceEqual(versions))
            throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
        // پس از Pin نیز ACL جاری بررسی می‌شود؛ Pin هیچ مجوزی اعطا نمی‌کند.
        allowed = await authorization.ResolveForChatAsync(user, session, versions, payer, ct);
        var plan = new AiChatSourcePlan(mode, intent, allowed);
        if (!LocallyReady(allowed))
        {
            if (!plan.AllowsFallback) throw AiSourceSelection.Error(AiSourceErrorCodes.NotReady);
            return plan with { FallbackReason = AiSourceErrorCodes.NotReady };
        }
        try { runtime.EnsureEnabled(); }
        catch (AiServiceException error) when (error.Code == AiServiceErrorCodes.FeatureDisabled)
        {
            if (!plan.AllowsFallback) throw AiSourceSelection.Error(AiSourceErrorCodes.RetrievalUnavailable);
            return plan with { FallbackReason = AiSourceErrorCodes.RetrievalUnavailable };
        }
        return plan;
    }

    public async Task ReauthorizeHistoryAsync(Guid user, Guid session, IReadOnlyList<AiConversationSourcePin> versions,
        AiPayerSelection? payer, CancellationToken ct)
    {
        if (versions.Count != 0)
            await authorization.ResolveAsync(user, session, versions.Select(x => new KnowledgeSourceVersion(x.SourceId, x.Version)).ToArray(), payer, ct);
    }

    public async Task ReauthorizeSelectionAsync(AiChatSourcePlan plan, CancellationToken ct)
    {
        if (plan.Authorized is not { } before) return;
        var current = await authorization.ResolveForChatAsync(before.Actor.UserId, before.Actor.SessionId, before.Versions, before.Commerce.Payer, ct);
        if (before.Actor != current.Actor) throw AiSourceSelection.Error(AiSourceErrorCodes.ContextMismatch);
    }

    public async Task<AiChatSourceAnswer> AnswerAsync(AiChatSourcePlan plan, string query, string requestId,
        string correlation, bool streaming, CancellationToken ct)
    {
        if (plan.Authorized is null) return new(null, new(AiChatProvenanceKind.General));
        if (plan.FallbackReason is not null) return new(null, new(AiChatProvenanceKind.GeneralFallback, plan.FallbackReason));
        var context = plan.Authorized;
        if (!LocallyReady(context)) return FallbackOrThrow(plan, AiSourceErrorCodes.NotReady);
        runtime.EnsureEnabled();
        foreach (var version in context.Versions)
        {
            var status = await knowledge.StatusAsync(version, ct, correlation);
            if (status.State != "Ready")
            {
                var code = status.State.EndsWith("Failed", StringComparison.Ordinal) ? KnowledgeErrorCodes.ProcessingFailed : KnowledgeErrorCodes.Processing;
                if (plan.AllowsFallback) return new(null, new(AiChatProvenanceKind.GeneralFallback, code));
                throw new AiKnowledgeException(code, 409, status.FailureCode);
            }
        }
        await authorization.ReauthorizeAsync(context, ct);
        var evidence = await knowledge.RetrieveAsync(new(requestId, correlation, query, context.Versions), ct);
        await authorization.ReauthorizeAsync(context, ct);
        if (evidence.Status != "Ok" || evidence.Results.Count == 0)
            return FallbackOrThrow(plan, AiConversationErrorCodes.InsufficientEvidence);
        if (streaming)
            throw new AiServiceException(AiConversationErrorCodes.StreamingNotSupported, "پاسخ مبتنی بر منبع فقط از مسیر chat غیرجریانی قابل دریافت است.");
        var answer = await knowledge.AnswerAsync(new(requestId, correlation, query, "fa", context.Versions, 8, false, []), evidence, ct);
        await authorization.ReauthorizeAsync(context, ct);
        if (answer.Status != "Ok") return FallbackOrThrow(plan, AiConversationErrorCodes.InsufficientEvidence);
        return new(answer, new(AiChatProvenanceKind.KnowledgeRag));
    }

    private static AiChatSourceAnswer FallbackOrThrow(AiChatSourcePlan plan, string reason) => plan.AllowsFallback
        ? new(null, new(AiChatProvenanceKind.GeneralFallback, reason))
        : throw new AiServiceException(reason, "منابع انتخاب‌شده هنوز آماده نیستند یا شواهد کافی ندارند.");
    private static bool LocallyReady(KnowledgeAuthorizedSources context) => context.Sources.All(x =>
        x.PublicationStatus == SourcePublicationStatus.Published && x.SourceReady && x.FileReady &&
        x.SafetyStatus == SourceSafetyStatus.FileValidated && x.Capabilities.CanDownload &&
        x.ProcessingStatus is not (SourceProcessingStatus.Pending or SourceProcessingStatus.Processing or SourceProcessingStatus.Failed));
    private static AiServiceException Invalid() => new(AiServiceErrorCodes.InvalidRequest, "انتخاب حالت یا نسخهٔ منبع معتبر نیست.");
}
