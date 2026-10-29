namespace MakanApp.Domain.Commerce;

public sealed class UsageReservation
{
    private UsageReservation() { }
    public Guid Id { get; private set; }
    public SubscriptionOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public Guid? PlanVersionId { get; private set; }
    public Guid ConsumerUserId { get; private set; }
    public CommerceContextType ContextType { get; private set; }
    public Guid ContextId { get; private set; }
    public QuotaKey QuotaKey { get; private set; }
    public DateTime PeriodStartsAtUtc { get; private set; }
    public DateTime PeriodEndsAtUtc { get; private set; }
    public long Amount { get; private set; }
    public Guid OperationId { get; private set; }
    public byte[] PayloadHash { get; private set; } = [];
    public bool IsTrial { get; private set; }
    public UsageReservationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? FinalizedAtUtc { get; private set; }
    public string? FinalizationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static UsageReservation Create(SubscriptionOwner owner, Guid? subscriptionId, Guid? versionId, Guid consumerUserId,
        CommerceContextType contextType, Guid contextId, QuotaKey key, DateTime start, DateTime end, long amount,
        Guid operationId, byte[] hash, bool trial, DateTime utcNow)
    {
        CommercialPeriods.RequireUtc(start); CommercialPeriods.RequireUtc(end); CommercialPeriods.RequireUtc(utcNow);
        if (consumerUserId == Guid.Empty || contextId == Guid.Empty || operationId == Guid.Empty || amount <= 0 || hash.Length != 32
            || !Enum.IsDefined(contextType) || !Enum.IsDefined(key) || start > utcNow || end <= utcNow
            || trial && (owner.Type != SubscriptionOwnerType.Individual || owner.Id != consumerUserId || subscriptionId.HasValue
                || versionId.HasValue || key != QuotaKey.AiTextRequests || amount != 1)
            || !trial && (!subscriptionId.HasValue || !versionId.HasValue))
            throw new ArgumentException("رزرو مصرف معتبر نیست.");
        return new UsageReservation
        {
            Id = Guid.NewGuid(), OwnerType = owner.Type, OwnerId = owner.Id, SubscriptionId = subscriptionId,
            PlanVersionId = versionId, ConsumerUserId = consumerUserId, ContextType = contextType, ContextId = contextId,
            QuotaKey = key, PeriodStartsAtUtc = start, PeriodEndsAtUtc = end, Amount = amount, OperationId = operationId,
            PayloadHash = hash.ToArray(), IsTrial = trial, Status = UsageReservationStatus.Reserved, CreatedAtUtc = utcNow
        };
    }

    public bool Finalize(UsageReservationStatus status, string reason, DateTime utcNow)
    {
        CommercialPeriods.RequireUtc(utcNow);
        if (status is not (UsageReservationStatus.Consumed or UsageReservationStatus.Released or UsageReservationStatus.Unknown)
            || string.IsNullOrWhiteSpace(reason) || reason.Length > 100 || reason.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-' and not '.')
            || utcNow < CreatedAtUtc) throw new ArgumentException("نتیجه و کد علت معتبر لازم است.");
        if (Status == status) return false;
        if (Status is UsageReservationStatus.Consumed or UsageReservationStatus.Released)
            throw new InvalidOperationException("نتیجهٔ قطعی رزرو قابل بازنویسی نیست.");
        Status = status; FinalizationReason = reason;
        FinalizedAtUtc = status == UsageReservationStatus.Unknown ? null : utcNow;
        return true;
    }
}
