namespace MakanApp.Domain.Commerce;

public sealed partial class UsageLedgerEntry
{
    public SubscriptionOwnerType OwnerType { get; private set; } = SubscriptionOwnerType.Organization;
    public Guid OwnerId { get; private set; }
    public Guid? PlanVersionId { get; private set; }
    public Guid? ConsumerUserId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public CommerceContextType? ContextType { get; private set; }
    public Guid? ContextId { get; private set; }
    public DateTime? PeriodStartsAtUtc { get; private set; }
    public DateTime? PeriodEndsAtUtc { get; private set; }
    public UsageReservationStatus Status { get; private set; } = UsageReservationStatus.Consumed;
    public string? ReasonCode { get; private set; }

    public static UsageLedgerEntry FromReservation(UsageReservation reservation, DateTime utcNow, string reasonCode)
    {
        CommercialPeriods.RequireUtc(utcNow);
        if (utcNow < reservation.CreatedAtUtc || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 100)
            throw new ArgumentException("رویداد مصرف معتبر نیست.");
        return new UsageLedgerEntry
        {
            Id = Guid.NewGuid(), OwnerType = reservation.OwnerType, OwnerId = reservation.OwnerId,
            OrganizationId = reservation.OwnerType == SubscriptionOwnerType.Organization ? reservation.OwnerId : null,
            SubscriptionId = reservation.SubscriptionId, PlanVersionId = reservation.PlanVersionId,
            ConsumerUserId = reservation.ConsumerUserId, ReservationId = reservation.Id,
            ContextType = reservation.ContextType, ContextId = reservation.ContextId,
            PeriodStartsAtUtc = reservation.PeriodStartsAtUtc, PeriodEndsAtUtc = reservation.PeriodEndsAtUtc,
            QuotaKey = reservation.QuotaKey, Amount = reservation.Amount, SourceOperationId = reservation.OperationId,
            Status = reservation.Status, ReasonCode = reasonCode, OccurredAtUtc = utcNow
        };
    }
}
