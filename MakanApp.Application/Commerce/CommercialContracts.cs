using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public enum SubscriptionDisplayState
{
    NoSubscription = 1, PendingPayment = 2, Active = 3, QuotaWarning = 4,
    QuotaExceeded = 5, GracePeriod = 6, Expired = 7, ScheduledDowngrade = 8, LegacyPendingContract = 9
}

public sealed record CommercialQuotaDto(QuotaKey Key, QuotaKind Kind, string Unit, long? Limit,
    long Consumed, long Reserved, long? Remaining, bool IsOverQuota, int? WarningPercent)
{
    public int? CriticalWarningPercent { get; init; }
    public bool IsUsageAvailable { get; init; } = true;
    public bool IsUnlimited => Limit is null;
    public string WarningLevel => !IsUsageAvailable ? "None" : Limit > 0 && CriticalWarningPercent.HasValue && ((decimal)Consumed + Reserved) * 100 >= Limit * CriticalWarningPercent
        ? "Critical" : Limit > 0 && WarningPercent.HasValue && ((decimal)Consumed + Reserved) * 100 >= Limit * WarningPercent ? "Warning" : "None";
}
public sealed record CommercialPlanDto(Guid PlanId, Guid PlanVersionId, string Code, string DisplayName,
    SubscriptionOwnerType Audience, int VersionNumber, decimal PriceToman, string DisplayCurrency,
    BillingCalendar Calendar, string RenewalMode, bool AutoRenew,
    IReadOnlyDictionary<EntitlementKey, bool> Entitlements, IReadOnlyDictionary<QuotaKey, long?> Quotas);
public sealed record CommercialSummaryDto(SubscriptionOwnerType OwnerType, Guid OwnerId, SubscriptionDisplayState State,
    bool CommercialControlEnabled, Guid? SubscriptionId, Guid? PlanVersionId, DateTime? PeriodStartsAtUtc,
    DateTime? PeriodEndsAtUtc, DateTime? GraceEndsAtUtc, Guid? ScheduledPlanVersionId,
    IReadOnlyDictionary<EntitlementKey, bool> Entitlements, IReadOnlyList<CommercialQuotaDto> Quotas,
    DateTime ObservedAtUtc)
{
    public string? Version { get; init; }
    public CommercialMigrationStatus MigrationStatus { get; init; } = CommercialMigrationStatus.NotApplicable;
    public Guid? LegacySubscriptionId { get; init; }
    public DateTime? CommercialEnforcementFromUtc { get; init; }
    public bool HasVerifiedCommercialContract => CommercialControlEnabled && SubscriptionId.HasValue && PlanVersionId.HasValue
        && PeriodStartsAtUtc.HasValue && PeriodEndsAtUtc > PeriodStartsAtUtc;
    public string? PlanCode { get; init; }
    public string? PlanDisplayName { get; init; }
    public int? PlanVersionNumber { get; init; }
    public decimal? MonthlyPriceToman { get; init; }
    public string DisplayCurrency => "TOMAN";
    public bool AutoRenew => false;
    public string RenewalMode => "Manual";
    public bool HasPendingPayment { get; init; }
    public Guid? RenewalPlanVersionId { get; init; }
    public decimal? RenewalPriceToman { get; init; }
}
public sealed record AiQuotaPreflightCommand(CommerceContextType ContextType, Guid? ContextId = null, AiPayerSelection? Payer = null);
public sealed record CommercialContext(Guid ConsumerUserId, SubscriptionOwner Owner, CommerceContextType ContextType,
    Guid ContextId, bool Trial);
public sealed record AiQuotaPreflightDto(AiPayerSelection Payer, SubscriptionOwnerType OwnerType, Guid OwnerId,
    string Unit, long Amount, long? Remaining, DateTime? PeriodEndsAtUtc, bool Allowed, string? ErrorCode,
    bool RequiresExplicitSelection, bool AuthorizesEducationalData = false);
public sealed record ReserveUsageCommand(AiQuotaPreflightCommand Context, Guid OperationId, byte[] PayloadHash);
public sealed record UsageReservationDto(Guid ReservationId, Guid OperationId, SubscriptionOwnerType OwnerType,
    Guid OwnerId, Guid? SubscriptionId, Guid? PlanVersionId, QuotaKey QuotaKey, long Amount,
    DateTime PeriodStartsAtUtc, DateTime PeriodEndsAtUtc, UsageReservationStatus Status, bool Replayed);
public sealed record ScheduleDowngradeCommand(Guid PlanVersionId, string ExpectedVersion, string Reason);
public sealed record CommercialQuoteDto(CommercialOrderPurpose Purpose, Guid PlanVersionId, Guid? SubscriptionId,
    decimal PriceToman, string DisplayCurrency, DateTime EffectiveAtUtc, DateTime PeriodEndsAtUtc,
    IReadOnlyList<CommercialQuotaDto> EffectiveQuotas, bool AutoRenew)
{
    public DateTime ExpiresAtUtc { get; init; }
    public bool PreviewOnly => true;
    public bool StartsUponVerifiedPayment { get; init; }
}

public interface ICommercialStore
{
    Task<IReadOnlyList<CommercialPlanDto>> CatalogAsync(SubscriptionOwnerType audience, DateTime utcNow, CancellationToken cancellationToken);
    Task<CommercialSummaryDto> SummaryAsync(SubscriptionOwner owner, DateTime utcNow, CancellationToken cancellationToken);
    Task<bool> CanAccessPersonalClassAsync(Guid userId, Guid classId, CancellationToken cancellationToken);
    Task<AiQuotaPreflightDto> PreflightAsync(CommercialContext context, DateTime utcNow, CancellationToken cancellationToken);
    Task<UsageReservationDto> ReserveAsync(CommercialContext context, Guid operationId, byte[] hash, DateTime utcNow, CancellationToken cancellationToken);
    Task<UsageReservationDto?> ReserveReportExportAsync(Guid organizationId, Guid consumerUserId, Guid jobId, byte[] hash,
        DateTime utcNow, CancellationToken cancellationToken);
    Task<UsageReservationDto> FinalizeAsync(Guid consumerUserId, Guid reservationId, UsageReservationStatus status,
        string reasonCode, DateTime utcNow, CancellationToken cancellationToken);
    Task<CommercialSummaryDto> ScheduleDowngradeAsync(SubscriptionOwner owner, ScheduleDowngradeCommand command,
        Guid actorUserId, Guid sessionId, DateTime utcNow, CancellationToken cancellationToken);
}
