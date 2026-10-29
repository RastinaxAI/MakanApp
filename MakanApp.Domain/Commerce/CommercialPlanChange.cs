namespace MakanApp.Domain.Commerce;

public static class CommercialPlanChange
{
    public static void EnsureUpgrade(PlanVersion previous, PlanVersion next)
    {
        if (previous.Price is null || next.Price is null || next.Price.Currency != previous.Price.Currency
            || next.Price.Amount < previous.Price.Amount || next.Calendar != previous.Calendar
            || previous.Entitlements.Any(old => old.Enabled && !next.Entitlements.Any(e => e.Key == old.Key && e.Enabled)))
            throw new InvalidOperationException("تغییر ترکیبی یا کاهنده فقط در دورهٔ بعد قابل اجراست.");
        foreach (var old in previous.QuotaDefinitions)
        {
            var updated = next.QuotaDefinitions.SingleOrDefault(q => q.Key == old.Key);
            if (updated is null || old.Limit is null && updated.Limit.HasValue
                || old.Limit.HasValue && updated.Limit.HasValue && updated.Limit < old.Limit)
                throw new InvalidOperationException("ارتقا نباید هیچ سقف قبلی را کاهش دهد.");
        }
    }
}
