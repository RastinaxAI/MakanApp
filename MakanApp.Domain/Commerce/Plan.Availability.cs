namespace MakanApp.Domain.Commerce;

public sealed partial class Plan
{
    public SubscriptionOwnerType Audience { get; private set; } = SubscriptionOwnerType.Organization;
    public bool CatalogVisible { get; private set; }
    public bool NewPurchaseAvailable { get; private set; }
    public bool RenewalAvailable { get; private set; }
    public DateTime? AvailabilityEffectiveAtUtc { get; private set; }

    public void SetAvailability(bool visible, bool newPurchase, bool renewal, DateTime effectiveAtUtc)
    {
        EnsureUtc(effectiveAtUtc, nameof(effectiveAtUtc));
        CatalogVisible = visible;
        NewPurchaseAvailable = newPurchase;
        RenewalAvailable = renewal;
        AvailabilityEffectiveAtUtc = effectiveAtUtc;
    }
}
