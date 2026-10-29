namespace MakanApp.Domain.Commerce;

public sealed partial class Order
{
    public bool HasCommercialTerms { get; private set; }
    public CommercialOrderPurpose Purpose { get; private set; } = CommercialOrderPurpose.Purchase;
    public Guid? TargetSubscriptionId { get; private set; }
    public Guid? ExpectedPreviousPlanVersionId { get; private set; }
    public DateTime? BillingAnchorUtc { get; private set; }
    public int BillingPeriodIndex { get; private set; }
    public DateTime? TermsExpireAtUtc { get; private set; }

    public void SetCommercialTerms(CommercialOrderPurpose purpose, Guid? targetSubscriptionId, Guid? previousVersion,
        DateTime anchorUtc, int periodIndex, DateTime expiresAtUtc, decimal authoritativeAmountIrr)
    {
        if (HasCommercialTerms || Status != OrderStatus.PendingPayment || !Enum.IsDefined(purpose)
            || purpose != CommercialOrderPurpose.Purchase && (!targetSubscriptionId.HasValue || !previousVersion.HasValue)
            || periodIndex < 0 || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= CreatedAtUtc
            || anchorUtc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("شرایط سفارش معتبر نیست.");
        EnsureAmountAndCurrency(authoritativeAmountIrr, Currency);
        HasCommercialTerms = true; Purpose = purpose; TargetSubscriptionId = targetSubscriptionId;
        ExpectedPreviousPlanVersionId = previousVersion; BillingAnchorUtc = anchorUtc; BillingPeriodIndex = periodIndex;
        TermsExpireAtUtc = expiresAtUtc; Amount = authoritativeAmountIrr;
    }
}
