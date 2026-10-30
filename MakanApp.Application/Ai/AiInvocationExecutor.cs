using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public sealed class AiInvocationExecutor(CommercialSubscriptionService commerce, IAccessContextResolver contexts,
    IAiRuntimeAvailability runtime) : IAiInvocationExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(Guid userId, Guid sessionId, TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> invoke, Func<CancellationToken, Task> reauthorize,
        AiInvocationOptions? options, CancellationToken ct)
        where TRequest : IAiServiceRequest where TResponse : class, IAiServiceResponse
    {
        runtime.EnsureEnabled();
        var command = AiInvocationOptions.Resolve(options);
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        if (context.WorkspaceType != WorkspaceType.Organization || context.OrganizationId is null || context.MembershipId is null ||
            request.Actor.UserId != userId.ToString("D") || request.Actor.Role != context.ActiveRole?.ToString())
            throw Denied();
        var document = JsonSerializer.SerializeToElement(request, request.GetType(), JsonOptions);
        if (!document.TryGetProperty("scope", out var scope) ||
            !scope.TryGetProperty("organizationId", out var org) || !scope.TryGetProperty("schoolId", out var school) ||
            org.GetString() != context.OrganizationId.Value.ToString("D") || school.GetString() != org.GetString())
            throw Denied();
        var quotaContext = new AiQuotaPreflightCommand(CommerceContextType.Organization, context.OrganizationId, command.Payer);
        var hash = Fingerprint(request, context);
        // Reserve موجود، replay را پیش از بررسی دوبارهٔ مانده تشخیص می‌دهد؛ آخرین سهمیه نیز retry-safe است.
        // بررسی Preflight اتمیکِ نهایی در همان transaction کوتاه Reserve انجام می‌شود.
        var reservation = await commerce.ReserveSimpleTextAsync(userId, sessionId,
            new(quotaContext, command.OperationId!.Value, hash), ct);
        if (reservation.Replayed)
            throw new AiServiceException(reservation.Status switch
            {
                UsageReservationStatus.Consumed => AiInvocationErrorCodes.Completed,
                UsageReservationStatus.Reserved => AiInvocationErrorCodes.InProgress,
                UsageReservationStatus.Unknown => AiInvocationErrorCodes.Unknown,
                _ => AiInvocationErrorCodes.Failed
            }, "این عملیات قبلاً ثبت شده است؛ اجرای تکراری یا برداشت دوباره انجام نمی‌شود.");
        var invocationStarted = false;
        TResponse response;
        try
        {
            await reauthorize(ct);
            var current = await contexts.ResolveAsync(userId, sessionId, ct);
            if (context != current) throw Denied();
            invocationStarted = true;
            response = await invoke(request, ct);
        }
        catch (Exception error)
        {
            var status = !invocationStarted || error is AiServiceException ai &&
                (ai.Code is AiServiceErrorCodes.FeatureDisabled or AiServiceErrorCodes.InvalidRequest or AiServiceErrorCodes.ContextNotAllowed or
                    AiServiceErrorCodes.ServiceUnauthorized or AiServiceErrorCodes.ServiceForbidden or AiServiceErrorCodes.RateLimited ||
                 ai.Code == AiServiceErrorCodes.ServiceUnavailable && ai.UpstreamStatusCode == 503)
                ? UsageReservationStatus.Released : UsageReservationStatus.Unknown;
            await Finish(status, status == UsageReservationStatus.Released ? "ai_not_completed" : "ai_outcome_unknown");
            throw;
        }
        // خطای ثبت نتیجهٔ قطعی نباید به تلاش برای بازنویسی Consumed به Unknown تبدیل شود.
        // قطع اتصال Client نیز ثبت مصرف پاسخ معتبر را لغو نمی‌کند.
        await Finish(UsageReservationStatus.Consumed, "ai_response_validated");
        return response;

        async Task Finish(UsageReservationStatus status, string reason)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await commerce.FinalizeAsync(userId, reservation.ReservationId, status, reason, cleanup.Token);
        }
    }

    public static byte[] Fingerprint(IAiServiceRequest request, AccessContext context)
    {
        var payload = JsonSerializer.SerializeToNode(request, request.GetType(), JsonOptions)!.AsObject();
        // شناسه/Correlation و گزارش‌های متغیر سرور، هویت intent کاربر نیستند؛ در log یا receipt هم ذخیره نمی‌شوند.
        foreach (var key in new[] { "requestId", "correlationId", "studentData", "childData", "classData", "schoolData", "context" }) payload.Remove(key);
        payload["operation"] = request.GetType().Name;
        payload["membershipId"] = context.MembershipId?.ToString("D");
        payload["selectedSourceIds"] = new JsonArray();
        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
    }
    private static AiServiceException Denied() => new(AiServiceErrorCodes.ContextNotAllowed, "زمینهٔ معتبر و جاری برای اجرای AI لازم است.");
}
