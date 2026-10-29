namespace MakanApp.Domain.Commerce;

public sealed partial class PlanVersion
{
    public BillingCalendar? Calendar { get; private set; }
    public string? SaleTermsReference { get; private set; }
    public int? WarningPercent { get; private set; }
    public int? CriticalWarningPercent { get; private set; }
    public bool ExplicitlyFree { get; private set; }
    public bool HasMonthlyTerms => Calendar.HasValue && !string.IsNullOrWhiteSpace(SaleTermsReference);

    public void SetMonthlyTerms(BillingCalendar calendar, string saleTermsReference, int? warningPercent, int? criticalWarningPercent)
    {
        EnsureDraft();
        if (!Enum.IsDefined(calendar) || string.IsNullOrWhiteSpace(saleTermsReference) || saleTermsReference.Trim().Length > 200
            || warningPercent is < 1 or > 99 || criticalWarningPercent is < 1 or > 99
            || warningPercent.HasValue && criticalWarningPercent.HasValue && warningPercent >= criticalWarningPercent)
            throw new ArgumentException("دوره، مرجع شرایط فروش یا آستانهٔ هشدار معتبر نیست.");
        Calendar = calendar;
        SaleTermsReference = saleTermsReference.Trim();
        WarningPercent = warningPercent;
        CriticalWarningPercent = criticalWarningPercent;
    }

    public void SetFreePrice()
    {
        EnsureDraft();
        ExplicitlyFree = true;
        if (Price is null) Price = new PlanVersionPrice(Id, 0, CurrencyCode.Irr, true);
        else Price.MakeFree();
    }

    public IReadOnlyList<string> ValidatePublication(SubscriptionOwnerType? audience = null)
    {
        var missing = new List<string>();
        if (Price is null || Price.Amount == 0 && !ExplicitlyFree) missing.Add("Price");
        if (!HasMonthlyTerms) missing.Add("MonthlyCalendarAndSaleTerms");
        if (_entitlements.Count == 0) missing.Add("Entitlements");
        if (audience == SubscriptionOwnerType.Organization)
            foreach (var item in _entitlements.Where(e => e.Enabled && e.Key is EntitlementKey.PersonalClassHosting or EntitlementKey.PersonalSources))
                missing.Add("OwnerFeatureMismatch:" + item.Key);
        foreach (var enabled in _entitlements.Where(item => item.Enabled))
        {
            QuotaKey[] required = enabled.Key switch
            {
                EntitlementKey.Academic => [QuotaKey.ActiveClasses, QuotaKey.ActiveLearners],
                EntitlementKey.PersonalClassHosting => [QuotaKey.ActiveClasses, QuotaKey.ActiveLearners, QuotaKey.StorageBytes, QuotaKey.AssignmentFileBytes],
                EntitlementKey.Ai => [QuotaKey.AiTextRequests],
                EntitlementKey.Reports => [QuotaKey.ReportExports],
                EntitlementKey.Storage => [QuotaKey.StorageBytes],
                EntitlementKey.PersonalSources => [QuotaKey.SourceCount, QuotaKey.StorageBytes],
                _ => []
            };
            foreach (var key in required)
                if (!_quotaDefinitions.Any(item => item.Key == key)) missing.Add("Quota:" + key);
        }
        return missing;
    }
}
