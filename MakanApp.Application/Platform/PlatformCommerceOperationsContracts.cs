using MakanApp.Domain.Commerce;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Reports;

namespace MakanApp.Application.Platform;

public sealed record PlatformPlanQuery(string? Search, int Page = 1, int PageSize = 30);

public sealed record PlatformPlanItem(
    Guid PlanId,
    string Code,
    string DisplayName,
    int VersionCount,
    int? LatestPublishedVersion,
    DateTime CreatedAtUtc);

public sealed record PlatformPlanPage(
    IReadOnlyCollection<PlatformPlanItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformPlanEntitlementItem(EntitlementKey Key, bool Enabled);
public sealed record PlatformPlanQuotaItem(QuotaKey Key, long? Limit);

public sealed record PlatformPlanVersionItem(
    Guid PlanVersionId,
    int VersionNumber,
    PlanVersionStatus Status,
    decimal? PriceAmount,
    CurrencyCode? Currency,
    IReadOnlyCollection<PlatformPlanEntitlementItem> Entitlements,
    IReadOnlyCollection<PlatformPlanQuotaItem> Quotas,
    int SubscriptionCount,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    string Version);

public sealed record PlatformPlanDetail(
    Guid PlanId,
    string Code,
    string DisplayName,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<PlatformPlanVersionItem> Versions,
    bool VersionsTruncated);

public sealed record CreatePlatformPlanCommand(string Code, string DisplayName, string Reason);

public sealed record CreatePlatformPlanVersionCommand(
    decimal PriceAmount,
    CurrencyCode Currency,
    IReadOnlyCollection<PlatformPlanEntitlementItem> Entitlements,
    IReadOnlyCollection<PlatformPlanQuotaItem> Quotas,
    string Reason);

public sealed record PublishPlatformPlanVersionCommand(string ExpectedVersion, string Reason);

public sealed record PlatformSubscriptionQuery(
    Guid? OrganizationId,
    SubscriptionStatus? Status,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformSubscriptionUsageItem(
    QuotaKey Key,
    long? Limit,
    long CurrentUsage,
    bool IsOverQuota);

public sealed record PlatformSubscriptionItem(
    Guid SubscriptionId,
    PlatformOrganizationContext Organization,
    Guid PlanId,
    string PlanCode,
    Guid PlanVersionId,
    int PlanVersionNumber,
    SubscriptionStatus Status,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    IReadOnlyCollection<PlatformSubscriptionUsageItem> Usage,
    DateTime CreatedAtUtc);

public sealed record PlatformSubscriptionPage(
    IReadOnlyCollection<PlatformSubscriptionItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record OverridePlatformSubscriptionCommand(
    Guid PlanVersionId,
    DateTime? EndsAtUtc,
    string Reason);

public sealed record PlatformSubscriptionOverrideResult(
    PlatformSubscriptionItem Subscription,
    Guid? ReplacedSubscriptionId);

public sealed record PlatformOrderQuery(
    Guid? OrganizationId,
    OrderStatus? Status,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformPaymentAttemptItem(
    Guid PaymentAttemptId,
    string Provider,
    PaymentAttemptStatus Status,
    decimal Amount,
    CurrencyCode Currency,
    string? MaskedProviderReference,
    DateTime CreatedAtUtc,
    DateTime? VerifiedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureCode);

public sealed record PlatformPaymentItem(
    Guid PaymentId,
    Guid PaymentAttemptId,
    string Provider,
    string MaskedProviderReference,
    decimal Amount,
    CurrencyCode Currency,
    DateTime PaidAtUtc);

public sealed record PlatformOrderItem(
    Guid OrderId,
    Guid PurchaserUserId,
    PlatformOrganizationContext? Organization,
    Guid PlanVersionId,
    OrderStatus Status,
    decimal Amount,
    CurrencyCode Currency,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyCollection<PlatformPaymentAttemptItem> PaymentAttempts,
    PlatformPaymentItem? Payment);

public sealed record PlatformOrderPage(
    IReadOnlyCollection<PlatformOrderItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string ReconciliationAvailability);

public sealed record PlatformNotificationFailureItem(
    Guid DeliveryAttemptId,
    NotificationDeliveryChannel Channel,
    NotificationDeliveryStatus Status,
    int AttemptCount,
    NotificationDeliveryFailureReason? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc,
    DateTime? NextAttemptAtUtc);

public sealed record PlatformNotificationOperations(
    int PendingCount,
    int ProcessingCount,
    int DeferredCount,
    int FailedCount,
    double? OldestQueuedAgeSeconds,
    string PushProviderAvailability,
    string SmsProviderAvailability,
    IReadOnlyCollection<PlatformNotificationFailureItem> RecentFailures);

public sealed record PlatformJobStatusCount(string JobType, string Status, int Count);

public sealed record PlatformJobOperations(
    IReadOnlyCollection<PlatformJobStatusCount> Counts,
    DateTime? OldestPendingAtUtc);

public sealed record PlatformDependencyHealth(string Name, string Status, string Detail);

public sealed record PlatformSystemHealth(
    DateTime ObservedAtUtc,
    IReadOnlyCollection<PlatformDependencyHealth> Dependencies,
    PlatformNotificationOperations Notifications,
    PlatformJobOperations Jobs,
    string AiIntegrationStatus);
