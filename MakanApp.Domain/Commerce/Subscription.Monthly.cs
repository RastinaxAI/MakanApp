namespace MakanApp.Domain.Commerce;

public sealed partial class Subscription
{
    public SubscriptionOwnerType OwnerType { get; private set; } = SubscriptionOwnerType.Organization;
    public Guid OwnerId { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public Guid InitialPlanVersionId { get; private set; }
    public bool CommercialControlEnabled { get; private set; }
    public DateTime? BillingAnchorUtc { get; private set; }
    public int BillingPeriodIndex { get; private set; }
    public Guid? ScheduledPlanVersionId { get; private set; }
    public CommercialMigrationStatus MigrationStatus => OwnerType != SubscriptionOwnerType.Organization
        ? CommercialMigrationStatus.NotApplicable : CommercialControlEnabled
            ? CommercialMigrationStatus.Completed : CommercialMigrationStatus.AwaitingValidContract;
    // شروع کنترل از زمان ثبت واقعی قرارداد است؛ لنگر دورهٔ صریح، سابقهٔ کنترل را عقب نمی‌برد.
    public DateTime? CommercialEnforcementFromUtc => !CommercialControlEnabled ? null
        : StartsAtUtc > CreatedAtUtc ? StartsAtUtc : CreatedAtUtc;

    public static Subscription CreateMonthly(SubscriptionOwner owner, PlanVersion version, DateTime anchorUtc,
        int periodIndex, DateTime createdAtUtc, Guid? sourceOrderId, bool commercialControlEnabled)
    {
        if (!version.IsPublished || !version.HasMonthlyTerms || version.ValidatePublication(owner.Type).Count != 0 || periodIndex < 0)
            throw new InvalidOperationException("قرارداد ماهانه به نسخهٔ تجاری کامل نیاز دارد.");
        CommercialPeriods.RequireUtc(createdAtUtc);
        var start = CommercialPeriods.MonthlyBoundary(anchorUtc, periodIndex, version.Calendar!.Value);
        var end = CommercialPeriods.MonthlyBoundary(anchorUtc, periodIndex + 1, version.Calendar.Value);
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(), OwnerType = owner.Type, OwnerId = owner.Id,
            OwnerUserId = owner.Type == SubscriptionOwnerType.Individual ? owner.Id : null,
            OrganizationId = owner.Type == SubscriptionOwnerType.Organization ? owner.Id : null,
            PlanVersionId = version.Id, InitialPlanVersionId = version.Id, SourceOrderId = sourceOrderId,
            Status = start > createdAtUtc ? SubscriptionStatus.Scheduled : SubscriptionStatus.Active,
            StartsAtUtc = start, EndsAtUtc = end, CreatedAtUtc = createdAtUtc, ActivatedAtUtc = createdAtUtc,
            BillingAnchorUtc = anchorUtc, BillingPeriodIndex = periodIndex,
            CommercialControlEnabled = commercialControlEnabled
        };
        return subscription;
    }

    public void ApplyUpgrade(PlanVersion next, DateTime utcNow)
    {
        if (!IsEffectiveAt(utcNow) || !BillingAnchorUtc.HasValue || !next.IsPublished || !next.HasMonthlyTerms)
            throw new InvalidOperationException("قرارداد در وضعیت ارتقای معتبر نیست.");
        PlanVersionId = next.Id;
    }

    public void ScheduleDowngrade(PlanVersion next, DateTime utcNow)
    {
        if (!IsEffectiveAt(utcNow) || !BillingAnchorUtc.HasValue || !next.IsPublished || !next.HasMonthlyTerms)
            throw new InvalidOperationException("قرارداد در وضعیت کاهش زمان‌بندی‌شده نیست.");
        ScheduledPlanVersionId = next.Id;
    }

    public bool IsInOrganizationGrace(DateTime utcNow) => CommercialControlEnabled && OwnerType == SubscriptionOwnerType.Organization
        && Status is SubscriptionStatus.Active or SubscriptionStatus.Expired or SubscriptionStatus.Scheduled
        && EndsAtUtc.HasValue && utcNow >= EndsAtUtc.Value && utcNow < EndsAtUtc.Value.AddDays(7);
}
