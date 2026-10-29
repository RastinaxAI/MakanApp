namespace MakanApp.Domain.Commerce;

public enum CommercialEducationalAction { Read = 1, ExistingWrite = 2, NewCapacity = 3, AccessReduction = 4, Messaging = 5, Payment = 6 }

public static class OrganizationCommercialAccess
{
    public static bool IsAllowed(Subscription? managedContract, CommercialEducationalAction action, DateTime utcNow)
    {
        CommercialPeriods.RequireUtc(utcNow);
        if (action is CommercialEducationalAction.Read or CommercialEducationalAction.AccessReduction
            or CommercialEducationalAction.Messaging or CommercialEducationalAction.Payment) return true;
        if (managedContract is null || !managedContract.CommercialControlEnabled) return true;
        if (managedContract.IsEffectiveAt(utcNow)) return true;
        return managedContract.IsInOrganizationGrace(utcNow) && action == CommercialEducationalAction.ExistingWrite;
    }
}
