using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public enum CommerceSummaryState
{
    NoSubscription = 1,
    Active = 2,
    Inactive = 3
}

public enum QuotaUsageSource
{
    AuthoritativeModule = 1,
    UsageLedger = 2
}

public sealed record CommerceSubscriptionDto(
    Guid SubscriptionId,
    Guid PlanId,
    Guid PlanVersionId,
    string PlanCode,
    string PlanDisplayName,
    int PlanVersionNumber,
    SubscriptionStatus Status,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc);

public sealed record CommerceEntitlementDto(
    EntitlementKey Key,
    bool Enabled);

public sealed record CommerceQuotaDto(
    QuotaKey Key,
    long? Limit,
    long CurrentUsage,
    long? Remaining,
    bool IsUnlimited,
    bool IsOverQuota,
    QuotaUsageSource UsageSource);

public sealed record ManagerCommerceSummaryDto(
    Guid OrganizationId,
    CommerceSummaryState State,
    CommerceSubscriptionDto? Subscription,
    IReadOnlyCollection<CommerceEntitlementDto> Entitlements,
    IReadOnlyCollection<CommerceQuotaDto> Quotas,
    DateTime DataAsOfUtc);

public sealed record EffectiveSubscriptionResult(
    Guid SubscriptionId,
    Guid OrganizationId,
    Guid PlanVersionId,
    int PlanVersionNumber,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc);

public sealed record EffectiveQuotaResult(
    Guid SubscriptionId,
    QuotaKey Key,
    long? Limit,
    long CurrentUsage,
    bool IsUnlimited,
    bool IsOverQuota,
    QuotaUsageSource UsageSource);

public sealed record CapacityCheckResult(
    bool Allowed,
    string? DenialCode,
    Guid? SubscriptionId,
    QuotaKey Key,
    long RequestedAmount,
    long CurrentUsage,
    long? Limit,
    long? RemainingAfterOperation,
    bool IsUnlimited,
    bool IsOverQuota);

public sealed record UsageRecordResult(
    Guid EntryId,
    Guid SubscriptionId,
    QuotaKey Key,
    long Amount,
    long CurrentUsage,
    long? Limit,
    bool AlreadyRecorded);
