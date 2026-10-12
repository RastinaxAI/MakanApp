using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Commerce;

public sealed class CommerceService(
    IAccessContextResolver accessContextResolver,
    ICommerceStore store,
    TimeProvider timeProvider) : ICommerceService, IEntitlementService
{
    public async Task<ManagerCommerceSummaryDto> GetManagerSummaryAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != OrganizationRole.Manager)
        {
            throw new CommerceException(
                CommerceErrorCodes.SummaryNotAllowed,
                "خلاصه اشتراک فقط در فضای سازمانی و برای مدیر فعال قابل مشاهده است.");
        }

        var utcNow = UtcNow();
        var snapshot = await store.GetLatestSubscriptionAsync(
            context.OrganizationId.Value,
            utcNow,
            cancellationToken);
        if (snapshot is null)
        {
            return new ManagerCommerceSummaryDto(
                context.OrganizationId.Value,
                CommerceSummaryState.NoSubscription,
                null,
                [],
                [],
                utcNow);
        }

        var subscription = MapSubscription(snapshot);
        if (!snapshot.IsEffective)
        {
            return new ManagerCommerceSummaryDto(
                context.OrganizationId.Value,
                CommerceSummaryState.Inactive,
                subscription,
                [],
                [],
                utcNow);
        }

        var usage = await store.GetCurrentUsageAsync(
            snapshot.OrganizationId,
            snapshot.SubscriptionId,
            snapshot.Quotas.Keys.ToArray(),
            cancellationToken);

        return new ManagerCommerceSummaryDto(
            context.OrganizationId.Value,
            CommerceSummaryState.Active,
            subscription,
            snapshot.Entitlements
                .OrderBy(item => item.Key)
                .Select(item => new CommerceEntitlementDto(item.Key, item.Value))
                .ToArray(),
            snapshot.Quotas
                .OrderBy(item => item.Key)
                .Select(item => MapQuota(item.Key, item.Value, usage.GetValueOrDefault(item.Key)))
                .ToArray(),
            utcNow);
    }

    public async Task<EffectiveSubscriptionResult?> GetEffectiveSubscriptionAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetEffectiveSnapshotAsync(organizationId, cancellationToken);
        return snapshot is null
            ? null
            : new EffectiveSubscriptionResult(
                snapshot.SubscriptionId,
                snapshot.OrganizationId,
                snapshot.PlanVersionId,
                snapshot.PlanVersionNumber,
                snapshot.StartsAtUtc,
                snapshot.EndsAtUtc);
    }

    public async Task<bool> IsEnabledAsync(
        Guid organizationId,
        EntitlementKey key,
        CancellationToken cancellationToken)
    {
        EnsureOrganizationAndEnum(organizationId, key);
        var snapshot = await GetEffectiveSnapshotAsync(organizationId, cancellationToken);
        return snapshot is not null && snapshot.Entitlements.GetValueOrDefault(key);
    }

    public async Task<EffectiveQuotaResult?> GetQuotaAsync(
        Guid organizationId,
        QuotaKey key,
        CancellationToken cancellationToken)
    {
        EnsureOrganizationAndEnum(organizationId, key);
        var snapshot = await GetEffectiveSnapshotAsync(organizationId, cancellationToken);
        if (snapshot is null || !snapshot.Quotas.TryGetValue(key, out var limit))
        {
            return null;
        }

        var currentUsage = await GetUsageAsync(snapshot, key, cancellationToken);
        return new EffectiveQuotaResult(
            snapshot.SubscriptionId,
            key,
            limit,
            currentUsage,
            !limit.HasValue,
            limit.HasValue && currentUsage > limit.Value,
            CommercePolicy.GetUsageSource(key));
    }

    public async Task<CapacityCheckResult> CheckCapacityAsync(
        Guid organizationId,
        QuotaKey key,
        long requestedAmount,
        CancellationToken cancellationToken)
    {
        EnsureOrganizationAndEnum(organizationId, key);
        if (requestedAmount <= 0)
        {
            throw new CommerceException(
                CommerceErrorCodes.UsageInvalid,
                "مقدار درخواستی ظرفیت باید مثبت باشد.");
        }

        var snapshot = await store.GetLatestSubscriptionAsync(
            organizationId,
            UtcNow(),
            cancellationToken);
        if (snapshot is null)
        {
            return Denied(key, requestedAmount, CommerceErrorCodes.SubscriptionRequired);
        }

        if (!snapshot.IsEffective)
        {
            return Denied(key, requestedAmount, CommerceErrorCodes.SubscriptionInactive, snapshot.SubscriptionId);
        }

        if (!snapshot.Quotas.TryGetValue(key, out var limit))
        {
            return Denied(key, requestedAmount, CommerceErrorCodes.QuotaRequired, snapshot.SubscriptionId);
        }

        var currentUsage = await GetUsageAsync(snapshot, key, cancellationToken);
        if (!limit.HasValue)
        {
            return new CapacityCheckResult(
                true,
                null,
                snapshot.SubscriptionId,
                key,
                requestedAmount,
                currentUsage,
                null,
                null,
                true,
                false);
        }

        var overQuota = currentUsage > limit.Value;
        var allowed = !overQuota && requestedAmount <= limit.Value - currentUsage;
        return new CapacityCheckResult(
            allowed,
            allowed ? null : CommerceErrorCodes.QuotaExceeded,
            snapshot.SubscriptionId,
            key,
            requestedAmount,
            currentUsage,
            limit,
            Math.Max(0, limit.Value - currentUsage - (allowed ? requestedAmount : 0)),
            false,
            overQuota);
    }

    public async Task<UsageRecordResult> RecordUsageAsync(
        Guid organizationId,
        QuotaKey key,
        long amount,
        Guid sourceOperationId,
        CancellationToken cancellationToken)
    {
        EnsureOrganizationAndEnum(organizationId, key);
        if (!CommercePolicy.IsLedgerMetered(key) || amount <= 0 || sourceOperationId == Guid.Empty)
        {
            throw new CommerceException(
                CommerceErrorCodes.UsageInvalid,
                "فقط سهمیه metered با مقدار و شناسه عملیات معتبر در دفتر مصرف ثبت می‌شود.");
        }

        var result = await store.RecordMeteredUsageAsync(
            organizationId,
            key,
            amount,
            sourceOperationId,
            UtcNow(),
            cancellationToken);
        return result.Status switch
        {
            UsageRecordStoreStatus.Recorded or UsageRecordStoreStatus.AlreadyRecorded =>
                new UsageRecordResult(
                    result.EntryId!.Value,
                    result.SubscriptionId!.Value,
                    key,
                    amount,
                    result.CurrentUsage,
                    result.Limit,
                    result.Status == UsageRecordStoreStatus.AlreadyRecorded),
            UsageRecordStoreStatus.SubscriptionRequired => throw Error(
                CommerceErrorCodes.SubscriptionRequired,
                "سازمان اشتراک ندارد."),
            UsageRecordStoreStatus.SubscriptionInactive => throw Error(
                CommerceErrorCodes.SubscriptionInactive,
                "اشتراک سازمان فعال نیست."),
            UsageRecordStoreStatus.QuotaRequired => throw Error(
                CommerceErrorCodes.QuotaRequired,
                "سهمیه در نسخه پلن تعریف نشده است."),
            UsageRecordStoreStatus.QuotaExceeded => throw Error(
                CommerceErrorCodes.QuotaExceeded,
                "سقف سهمیه سازمان تکمیل شده است."),
            UsageRecordStoreStatus.Conflict => throw Error(
                CommerceErrorCodes.UsageConflict,
                "شناسه عملیات قبلاً با مقدار دیگری ثبت شده است."),
            _ => throw Error(
                CommerceErrorCodes.UsageConflict,
                "ثبت مصرف با وضعیت نامعتبر روبه‌رو شد.")
        };
    }

    private async Task<CommerceSubscriptionSnapshot?> GetEffectiveSnapshotAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new CommerceException(CommerceErrorCodes.UsageInvalid, "شناسه سازمان معتبر نیست.");
        }

        var snapshot = await store.GetLatestSubscriptionAsync(
            organizationId,
            UtcNow(),
            cancellationToken);
        return snapshot is { IsEffective: true } ? snapshot : null;
    }

    private async Task<long> GetUsageAsync(
        CommerceSubscriptionSnapshot snapshot,
        QuotaKey key,
        CancellationToken cancellationToken)
    {
        var usage = await store.GetCurrentUsageAsync(
            snapshot.OrganizationId,
            snapshot.SubscriptionId,
            [key],
            cancellationToken);
        return usage.GetValueOrDefault(key);
    }

    private static CommerceSubscriptionDto MapSubscription(CommerceSubscriptionSnapshot snapshot) =>
        new(
            snapshot.SubscriptionId,
            snapshot.PlanId,
            snapshot.PlanVersionId,
            snapshot.PlanCode,
            snapshot.PlanDisplayName,
            snapshot.PlanVersionNumber,
            snapshot.Status,
            snapshot.StartsAtUtc,
            snapshot.EndsAtUtc);

    private static CommerceQuotaDto MapQuota(QuotaKey key, long? limit, long currentUsage) =>
        new(
            key,
            limit,
            currentUsage,
            limit.HasValue ? Math.Max(0, limit.Value - currentUsage) : null,
            !limit.HasValue,
            limit.HasValue && currentUsage > limit.Value,
            CommercePolicy.GetUsageSource(key));

    private static CapacityCheckResult Denied(
        QuotaKey key,
        long amount,
        string code,
        Guid? subscriptionId = null) =>
        new(false, code, subscriptionId, key, amount, 0, null, null, false, false);

    private static CommerceException Error(string code, string message) => new(code, message);

    private static void EnsureOrganizationAndEnum<T>(Guid organizationId, T value)
        where T : struct, Enum
    {
        if (organizationId == Guid.Empty || !Enum.IsDefined(value))
        {
            throw new CommerceException(CommerceErrorCodes.UsageInvalid, "درخواست Commerce معتبر نیست.");
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
