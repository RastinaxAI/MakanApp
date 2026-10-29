using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Platform;

public sealed class PlatformCommerceOperationsService(
    IPlatformCommerceOperationsStore store,
    TimeProvider timeProvider) : IPlatformCommerceOperationsService
{
    public Task<PlatformPlanPage> GetPlansAsync(
        PlatformPlanQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        if (query.Search?.Trim().Length > 200)
        {
            throw InvalidQuery();
        }

        return store.GetPlansAsync(query with { Search = Normalize(query.Search) }, cancellationToken);
    }

    public async Task<PlatformPlanDetail> GetPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        await store.GetPlanAsync(RequireId(planId), cancellationToken)
        ?? throw Error(PlatformErrorCodes.PlanNotFound, "پلن پیدا نشد.");

    public Task<PlatformPlanDetail> CreatePlanAsync(
        Guid actorUserId,
        Guid actorSessionId,
        CreatePlatformPlanCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(command.DisplayName) || command.DisplayName.Trim().Length > 200)
        {
            throw InvalidQuery();
        }

        return store.CreatePlanAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            command with
            {
                Code = command.Code.Trim(),
                DisplayName = command.DisplayName.Trim(),
                Reason = command.Reason.Trim()
            },
            cancellationToken);
    }

    public Task<PlatformPlanVersionItem> CreatePlanVersionAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid planId,
        CreatePlatformPlanVersionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        var entitlements = command.Entitlements ?? [];
        var quotas = command.Quotas ?? [];
        if (command.PriceAmount.HasValue && (command.PriceAmount < 0 || decimal.Round(command.PriceAmount.Value, 2) != command.PriceAmount
                || command.PriceAmount == 0 && !command.ExplicitlyFree) ||
            command.Currency != CurrencyCode.Irr ||
            entitlements.Any(item => !Enum.IsDefined(item.Key)) ||
            quotas.Any(item => !Enum.IsDefined(item.Key) || item.Limit < 0) ||
            entitlements.Select(item => item.Key).Distinct().Count() != entitlements.Count ||
            quotas.Select(item => item.Key).Distinct().Count() != quotas.Count)
        {
            throw InvalidQuery();
        }

        return store.CreatePlanVersionAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(planId),
            command with
            {
                Entitlements = entitlements,
                Quotas = quotas,
                Reason = command.Reason.Trim()
            },
            cancellationToken);
    }

    public async Task<PlatformPlanVersionItem> PublishPlanVersionAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid planVersionId,
        PublishPlatformPlanVersionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        var version = DecodeVersion(command.ExpectedVersion);
        return await store.PublishPlanVersionAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(planVersionId),
            version,
            command.Reason.Trim(),
            cancellationToken);
    }

    public Task<PlatformSubscriptionPage> GetSubscriptionsAsync(
        PlatformSubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateOptionalId(query.OrganizationId);
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw InvalidQuery();
        }

        return store.GetSubscriptionsAsync(query, cancellationToken);
    }

    public async Task<PlatformSubscriptionOverrideResult> OverrideSubscriptionAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        OverridePlatformSubscriptionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        if (command.PlanVersionId == Guid.Empty ||
            command.EndsAtUtc is { Kind: not DateTimeKind.Utc } ||
            command.EndsAtUtc.HasValue && command.EndsAtUtc.Value <= UtcNow())
        {
            throw InvalidQuery();
        }

        return await store.OverrideSubscriptionAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(organizationId),
            command with { Reason = command.Reason.Trim() },
            cancellationToken);
    }

    public Task<PlatformOrderPage> GetOrdersAsync(
        PlatformOrderQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateOptionalId(query.OrganizationId);
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw InvalidQuery();
        }

        return store.GetOrdersAsync(query, cancellationToken);
    }

    public async Task<PlatformOrderItem> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await store.GetOrderAsync(RequireId(orderId), cancellationToken)
        ?? throw Error(PlatformErrorCodes.OrderNotFound, "سفارش پیدا نشد.");

    public Task<PlatformNotificationOperations> GetNotificationOperationsAsync(
        CancellationToken cancellationToken) =>
        store.GetNotificationOperationsAsync(UtcNow(), cancellationToken);

    public Task<PlatformJobOperations> GetJobOperationsAsync(CancellationToken cancellationToken) =>
        store.GetJobOperationsAsync(cancellationToken);

    public Task<PlatformSystemHealth> GetSystemHealthAsync(CancellationToken cancellationToken) =>
        store.GetSystemHealthAsync(UtcNow(), cancellationToken);

    private PlatformActorContext Actor(Guid userId, Guid sessionId, string correlationId)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId))
        {
            throw Error(PlatformErrorCodes.AccessDenied, "هویت عامل عملیات معتبر نیست.");
        }

        return new PlatformActorContext(userId, sessionId, UtcNow(), correlationId.Trim());
    }

    private static byte[] DecodeVersion(string value)
    {
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw new FormatException();
        }
        catch (FormatException)
        {
            throw Error(PlatformErrorCodes.PlanConflict, "نسخه هم‌زمانی PlanVersion معتبر نیست.");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw Error(PlatformErrorCodes.ReasonRequired, "دلیل معتبر و حداکثر ۵۰۰ کاراکتر الزامی است.");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is <= 0 or > 100_000 || pageSize is <= 0 or > 100)
        {
            throw InvalidQuery();
        }
    }

    private static void ValidateOptionalId(Guid? id)
    {
        if (id == Guid.Empty)
        {
            throw InvalidQuery();
        }
    }

    private static Guid RequireId(Guid id) => id != Guid.Empty ? id : throw InvalidQuery();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static PlatformException InvalidQuery() => Error(PlatformErrorCodes.QueryInvalid, "پارامترهای درخواست معتبر نیستند.");
    private static PlatformException Error(string code, string message) => new(code, message);
}
