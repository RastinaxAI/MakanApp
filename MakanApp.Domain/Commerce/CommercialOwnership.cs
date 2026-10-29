namespace MakanApp.Domain.Commerce;

public enum SubscriptionOwnerType { Individual = 1, Organization = 2 }
public enum CommerceContextType { Personal = 1, PersonalClass = 2, Organization = 3 }
public enum AiPayerSelection { Individual = 1, Organization = 2, FreeTrial = 3 }
public enum QuotaKind { CurrentCapacity = 1, PeriodicConsumption = 2 }
public enum BillingCalendar { Gregorian = 1, Persian = 2 }
public enum CommercialOrderPurpose { Purchase = 1, Renewal = 2, Upgrade = 3 }
public enum UsageReservationStatus { Reserved = 1, Consumed = 2, Released = 3, Unknown = 4 }
public enum CommercialMigrationStatus { NotApplicable = 1, AwaitingValidContract = 2, Completed = 3 }

public readonly record struct SubscriptionOwner
{
    public SubscriptionOwner(SubscriptionOwnerType type, Guid id)
    {
        if (!Enum.IsDefined(type) || id == Guid.Empty) throw new ArgumentException("مالک تجاری معتبر لازم است.");
        Type = type;
        Id = id;
    }
    public SubscriptionOwnerType Type { get; }
    public Guid Id { get; }
    public static SubscriptionOwner Individual(Guid userId) => new(SubscriptionOwnerType.Individual, userId);
    public static SubscriptionOwner Organization(Guid organizationId) => new(SubscriptionOwnerType.Organization, organizationId);
}

public static class QuotaKinds
{
    public static QuotaKind For(QuotaKey key) => key switch
    {
        QuotaKey.ReportExports or QuotaKey.AiTextRequests => QuotaKind.PeriodicConsumption,
        QuotaKey.ActiveLearners or QuotaKey.ActiveClasses or QuotaKey.StorageBytes or QuotaKey.SourceCount
            or QuotaKey.AssignmentFileBytes => QuotaKind.CurrentCapacity,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
    public static string Unit(QuotaKey key) => key switch
    {
        QuotaKey.StorageBytes or QuotaKey.AssignmentFileBytes => "bytes",
        _ => "count"
    };
}
