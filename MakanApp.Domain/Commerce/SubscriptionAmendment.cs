namespace MakanApp.Domain.Commerce;

public sealed class SubscriptionAmendment
{
    private SubscriptionAmendment() { }
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public Guid PreviousPlanVersionId { get; private set; }
    public Guid NextPlanVersionId { get; private set; }
    public Guid OrderId { get; private set; }
    public DateTime EffectiveAtUtc { get; private set; }
    public long RemainingTicks { get; private set; }
    public long DurationTicks { get; private set; }
    public decimal ChargedToman { get; private set; }

    public static SubscriptionAmendment Create(Subscription subscription, PlanVersion next, Guid orderId, DateTime effectiveAtUtc, decimal chargedToman,
        DateTime? quotedAtUtc = null)
    {
        CommercialPeriods.RequireUtc(effectiveAtUtc);
        var basis = quotedAtUtc ?? effectiveAtUtc;
        CommercialPeriods.RequireUtc(basis);
        if (!subscription.EndsAtUtc.HasValue || !subscription.IsEffectiveAt(effectiveAtUtc) || orderId == Guid.Empty || chargedToman < 0)
            throw new InvalidOperationException("سند ارتقا معتبر نیست.");
        if (basis < subscription.StartsAtUtc || basis > effectiveAtUtc) throw new InvalidOperationException("مبنای زمانی پیش‌فاکتور معتبر نیست.");
        return new SubscriptionAmendment
        {
            Id = Guid.NewGuid(), SubscriptionId = subscription.Id, PreviousPlanVersionId = subscription.PlanVersionId,
            NextPlanVersionId = next.Id, OrderId = orderId, EffectiveAtUtc = effectiveAtUtc,
            RemainingTicks = (subscription.EndsAtUtc.Value - basis).Ticks,
            DurationTicks = (subscription.EndsAtUtc.Value - subscription.StartsAtUtc).Ticks, ChargedToman = chargedToman
        };
    }
}
