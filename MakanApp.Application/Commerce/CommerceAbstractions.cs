using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public interface ICommerceService
{
    Task<ManagerCommerceSummaryDto> GetManagerSummaryAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IEntitlementService
{
    Task<EffectiveSubscriptionResult?> GetEffectiveSubscriptionAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<bool> IsEnabledAsync(
        Guid organizationId,
        EntitlementKey key,
        CancellationToken cancellationToken);

    Task<EffectiveQuotaResult?> GetQuotaAsync(
        Guid organizationId,
        QuotaKey key,
        CancellationToken cancellationToken);

    Task<CapacityCheckResult> CheckCapacityAsync(
        Guid organizationId,
        QuotaKey key,
        long requestedAmount,
        CancellationToken cancellationToken);

    Task<UsageRecordResult> RecordUsageAsync(
        Guid organizationId,
        QuotaKey key,
        long amount,
        Guid sourceOperationId,
        CancellationToken cancellationToken);
}

public sealed record CommerceSubscriptionSnapshot(
    Guid SubscriptionId,
    Guid OrganizationId,
    Guid PlanId,
    Guid PlanVersionId,
    string PlanCode,
    string PlanDisplayName,
    int PlanVersionNumber,
    SubscriptionStatus Status,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsEffective,
    IReadOnlyDictionary<EntitlementKey, bool> Entitlements,
    IReadOnlyDictionary<QuotaKey, long?> Quotas);

public enum UsageRecordStoreStatus
{
    Recorded = 1,
    AlreadyRecorded = 2,
    SubscriptionRequired = 3,
    SubscriptionInactive = 4,
    QuotaRequired = 5,
    QuotaExceeded = 6,
    Conflict = 7
}

public sealed record UsageRecordStoreResult(
    UsageRecordStoreStatus Status,
    Guid? EntryId,
    Guid? SubscriptionId,
    long CurrentUsage,
    long? Limit);

public interface ICommerceStore
{
    Task<CommerceSubscriptionSnapshot?> GetLatestSubscriptionAsync(
        Guid organizationId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<QuotaKey, long>> GetCurrentUsageAsync(
        Guid organizationId,
        Guid subscriptionId,
        IReadOnlyCollection<QuotaKey> quotaKeys,
        CancellationToken cancellationToken);

    Task<UsageRecordStoreResult> RecordMeteredUsageAsync(
        Guid organizationId,
        QuotaKey key,
        long amount,
        Guid sourceOperationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken);
}
