using System.Security.Cryptography;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Knowledge;

public sealed class KnowledgeIntegrationService(KnowledgeAuthorizationService authorization, IEducationalSourceStore sources,
    IAiKnowledgeClient client, IKnowledgeRuntimeAvailability runtime, CommercialSubscriptionService commerce,
    IKnowledgeOperationAudit audit)
{
    public async Task<KnowledgeIngestResult> IngestAsync(Guid user, Guid session, KnowledgeSourceVersion source,
        string correlation, KnowledgeMutationCommand command, CancellationToken ct)
    {
        runtime.EnsureEnabled(); ValidateMutation(command, correlation);
        var context = await authorization.ResolveAsync(user, session, [source], command.Payer, ct);
        EnsureWriter(context.Sources[0], command);
        await RequireEntitlementAsync(context, ct);
        await authorization.PinAsync(context, command.ClientOperationId, new { Action = "ingest", command.ExpectedVersion, command.Reason }, ct);
        var file = await sources.DownloadAsync(context.Actor, source.SourceId, source.Version, ct);
        await using var stream = file.Content;
        // همان سقف فایل Backend اعمال می‌شود؛ هیچ blob یا پیام خصوصی در Job/Log کپی نمی‌شود.
        if (file.SizeBytes is < 1 or > 10 * 1024 * 1024) throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + count > file.SizeBytes) throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
            memory.Write(buffer, 0, count);
        }
        if (memory.Length != file.SizeBytes) throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
        var bytes = memory.ToArray();
        var requestId = Guid.NewGuid().ToString("N");
        var metadata = new KnowledgeIngestMetadata(requestId, correlation, source.SourceId, source.Version,
            Convert.ToHexStringLower(SHA256.HashData(bytes)), file.ContentType, bytes.Length, context.Sources[0].Title,
            Scope(context.Sources[0]), new());
        await authorization.ReauthorizeAsync(context, ct);
        await audit.WriteAsync(new(context.Actor, command.ClientOperationId, source, "ingest", command.Reason, requestId, correlation, "Requested"), ct);
        var result = await client.IngestAsync(metadata, bytes, ct);
        await authorization.ReauthorizeAsync(context, ct);
        await audit.WriteAsync(new(context.Actor, command.ClientOperationId, source, "ingest", command.Reason, requestId, correlation, result.Status.State), ct);
        return result;
    }

    public async Task<KnowledgeProcessingStatus> StatusAsync(Guid user, Guid session, KnowledgeSourceVersion source, bool wait, string correlation, CancellationToken ct)
    {
        runtime.EnsureEnabled();
        if (!ValidCorrelation(correlation)) throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
        var context = await authorization.ResolveAsync(user, session, [source], null, ct);
        var result = wait ? await client.WaitForCompletionAsync(source, ct, correlation) : await client.StatusAsync(source, ct, correlation);
        await authorization.ReauthorizeAsync(context, ct);
        return result;
    }

    public async Task<KnowledgeProcessingStatus> CommandAsync(Guid user, Guid session, KnowledgeSourceVersion source,
        string action, string correlation, KnowledgeMutationCommand command, CancellationToken ct)
    {
        runtime.EnsureEnabled(); ValidateMutation(command, correlation);
        if (action is not ("delete" or "supersede" or "reindex")) throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
        var context = await authorization.ResolveAsync(user, session, [source], command.Payer, ct);
        EnsureWriter(context.Sources[0], command);
        if (action == "reindex") await RequireEntitlementAsync(context, ct);
        await authorization.PinAsync(context, command.ClientOperationId, new { Action = action, command.ExpectedVersion, command.Reason }, ct);
        await authorization.ReauthorizeAsync(context, ct);
        var request = new KnowledgeCommandRequest(Guid.NewGuid().ToString("N"), correlation);
        await audit.WriteAsync(new(context.Actor, command.ClientOperationId, source, action, command.Reason, request.RequestId, correlation, "Requested"), ct);
        var result = action switch
        {
            "delete" => await client.DeleteAsync(source, request, ct),
            "supersede" => await client.SupersedeAsync(source, request, ct),
            _ => await client.ReindexAsync(source, request, ct)
        };
        await authorization.ReauthorizeAsync(context, ct);
        await audit.WriteAsync(new(context.Actor, command.ClientOperationId, source, action, command.Reason, request.RequestId, correlation, result.State), ct);
        return result;
    }

    public async Task<KnowledgeRetrieveResponse> RetrieveAsync(Guid user, Guid session, string correlation, KnowledgeQueryCommand command, CancellationToken ct)
    {
        runtime.EnsureEnabled(); ValidateQuery(command, correlation);
        var context = await authorization.ResolveAsync(user, session, command.Sources, command.Payer, ct);
        await RequireEntitlementAsync(context, ct);
        await authorization.PinAsync(context, command.ClientOperationId, new { Action = "retrieve", command.Query, command.MaxResults, command.IncludeQuotePreview }, ct);
        await EnsureReadyAsync(context, correlation, ct);
        await authorization.ReauthorizeAsync(context, ct);
        var result = await client.RetrieveAsync(new(Guid.NewGuid().ToString("N"), correlation, command.Query,
            context.Versions, command.MaxResults, command.IncludeQuotePreview), ct);
        await authorization.ReauthorizeAsync(context, ct);
        return result;
    }

    public async Task<KnowledgeRagResponse> AnswerAsync(Guid user, Guid session, string correlation, KnowledgeQueryCommand command, CancellationToken ct)
    {
        runtime.EnsureEnabled(); ValidateQuery(command, correlation);
        var context = await authorization.ResolveAsync(user, session, command.Sources, command.Payer, ct);
        var payload = new { Action = "rag", command.Query, command.MaxResults, command.IncludeQuotePreview, command.Language };
        await authorization.PinAsync(context, command.ClientOperationId, payload, ct);
        // همان ledger و واحد SimpleText موجود استفاده می‌شود؛ قیمت/پرداخت یا ledger موازی ساخته نمی‌شود.
        var reservation = await commerce.ReserveForAssistantAsync(user, session,
            new(context.Commerce, command.ClientOperationId, KnowledgeAuthorizationService.Fingerprint(context, payload)), context.Actor.Organization ?? context.Actor.Account, ct);
        if (reservation.Replayed) throw new AiServiceException(reservation.Status switch
        {
            UsageReservationStatus.Consumed => AiInvocationErrorCodes.Completed,
            UsageReservationStatus.Reserved => AiInvocationErrorCodes.InProgress,
            UsageReservationStatus.Unknown => AiInvocationErrorCodes.Unknown,
            _ => AiInvocationErrorCodes.Failed
        }, "این عملیات قبلاً ثبت شده است؛ پاسخ خصوصی ذخیره یا درخواست دوباره اجرا نمی‌شود.");
        var generationStarted = false;
        KnowledgeRagResponse result;
        try
        {
            await EnsureReadyAsync(context, correlation, ct);
            await authorization.ReauthorizeAsync(context, ct);
            var requestId = Guid.NewGuid().ToString("N");
            var evidence = await client.RetrieveAsync(new(requestId, correlation, command.Query, context.Versions,
                command.MaxResults, command.IncludeQuotePreview), ct);
            await authorization.ReauthorizeAsync(context, ct);
            generationStarted = true;
            result = await client.AnswerAsync(new(requestId, correlation, command.Query, command.Language,
                context.Versions, command.MaxResults, command.IncludeQuotePreview, []), evidence, ct);
        }
        catch (Exception error)
        {
            var outcome = !generationStarted || error is AiKnowledgeException { Code: KnowledgeErrorCodes.InvalidRequest or
                KnowledgeErrorCodes.ContextTooLarge or KnowledgeErrorCodes.GenerationUnavailable } ? UsageReservationStatus.Released : UsageReservationStatus.Unknown;
            await Finish(outcome);
            throw;
        }
        await Finish(UsageReservationStatus.Consumed);
        // نتیجهٔ معتبر مصرف شده است؛ revoke هم‌زمان اجازهٔ انتشار متن به Client نمی‌دهد.
        await authorization.ReauthorizeAsync(context, ct);
        return result;

        async Task Finish(UsageReservationStatus status)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await commerce.FinalizeAsync(user, reservation.ReservationId, status, "knowledge_" + status.ToString().ToLowerInvariant(), cleanup.Token);
        }
    }

    private async Task RequireEntitlementAsync(KnowledgeAuthorizedSources context, CancellationToken ct)
    {
        var result = await commerce.PreflightForAssistantAsync(context.Actor.UserId, context.Actor.SessionId, context.Commerce, context.Actor.Organization ?? context.Actor.Account, ct);
        if (!result.Allowed) throw new CommerceException(result.ErrorCode ?? CommerceErrorCodes.EntitlementRequired, "مجوز تجاری معتبر برای استفاده از دانش لازم است.");
    }
    private async Task EnsureReadyAsync(KnowledgeAuthorizedSources context, string correlation, CancellationToken ct)
    {
        foreach (var source in context.Versions)
        {
            var status = await client.StatusAsync(source, ct, correlation);
            if (status.State == "Ready") continue;
            var code = status.State is "NotIngested" or "Deleted" or "Superseded" ? KnowledgeErrorCodes.NotIngested
                : status.State.EndsWith("Failed", StringComparison.Ordinal) ? KnowledgeErrorCodes.ProcessingFailed : KnowledgeErrorCodes.Processing;
            throw new AiKnowledgeException(code, 409, status.FailureCode);
        }
    }
    private static KnowledgeScope Scope(EducationalSourceResult source) => new(source.Scope.ToString(), source.OwnerUserId, source.OrganizationId, source.ClassId);
    private static void EnsureWriter(EducationalSourceResult source, KnowledgeMutationCommand command)
    {
        if (!source.Capabilities.CanArchive) throw new EducationalSourceException(SourceErrorCodes.NotAllowed);
        if (source.Version != command.ExpectedVersion) throw new EducationalSourceException(SourceErrorCodes.Conflict);
    }
    private static bool ValidCorrelation(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value == value.Trim() && !value.Any(char.IsControl);
    private static void ValidateMutation(KnowledgeMutationCommand command, string correlation)
    {
        if (command.ClientOperationId == Guid.Empty || string.IsNullOrWhiteSpace(command.ExpectedVersion) ||
            string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500 || !ValidCorrelation(correlation))
            throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
    }
    private static void ValidateQuery(KnowledgeQueryCommand command, string correlation)
    {
        if (command.ClientOperationId == Guid.Empty || string.IsNullOrWhiteSpace(command.Query) || command.Query.Length > 4000 ||
            command.MaxResults is < 1 or > 20 || command.Language is not ("fa" or "en") || !ValidCorrelation(correlation))
            throw new AiKnowledgeException(KnowledgeErrorCodes.InvalidRequest, 422);
    }
}
