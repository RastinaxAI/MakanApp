namespace MakanApp.Domain.Commerce;

public sealed class Subscription
{
    private Subscription()
    {
    }

    private Subscription(
        Guid id,
        Guid organizationId,
        Guid planVersionId,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        DateTime createdAtUtc)
    {
        if (organizationId == Guid.Empty || planVersionId == Guid.Empty)
        {
            throw new ArgumentException("سازمان و نسخه پلن اشتراک الزامی هستند.");
        }

        EnsureUtc(startsAtUtc, nameof(startsAtUtc));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        if (endsAtUtc.HasValue)
        {
            EnsureUtc(endsAtUtc.Value, nameof(endsAtUtc));
            if (endsAtUtc <= startsAtUtc)
            {
                throw new ArgumentOutOfRangeException(nameof(endsAtUtc));
            }
        }

        Id = id;
        OrganizationId = organizationId;
        PlanVersionId = planVersionId;
        Status = SubscriptionStatus.Pending;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Subscription CreatePending(
        Guid organizationId,
        PlanVersion planVersion,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(planVersion);
        if (!planVersion.IsPublished)
        {
            throw new InvalidOperationException("اشتراک فقط می‌تواند به نسخه منتشرشده پلن متصل شود.");
        }

        return new Subscription(
            Guid.NewGuid(),
            organizationId,
            planVersion.Id,
            startsAtUtc,
            endsAtUtc,
            createdAtUtc);
    }

    public void Activate(DateTime activatedAtUtc)
    {
        EnsureUtc(activatedAtUtc, nameof(activatedAtUtc));
        if (Status != SubscriptionStatus.Pending ||
            activatedAtUtc < StartsAtUtc ||
            EndsAtUtc.HasValue && activatedAtUtc >= EndsAtUtc.Value)
        {
            throw new InvalidOperationException("اشتراک در این زمان قابل فعال‌سازی نیست.");
        }

        Status = SubscriptionStatus.Active;
        ActivatedAtUtc = activatedAtUtc;
    }

    public void Expire(DateTime expiredAtUtc)
    {
        EnsureUtc(expiredAtUtc, nameof(expiredAtUtc));
        if (Status != SubscriptionStatus.Active ||
            !EndsAtUtc.HasValue ||
            expiredAtUtc < EndsAtUtc.Value)
        {
            throw new InvalidOperationException("اشتراک هنوز قابل انقضا نیست.");
        }

        Status = SubscriptionStatus.Expired;
    }

    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureUtc(cancelledAtUtc, nameof(cancelledAtUtc));
        if (Status is SubscriptionStatus.Expired or SubscriptionStatus.Cancelled ||
            cancelledAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("اشتراک قابل لغو نیست.");
        }

        Status = SubscriptionStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    public bool IsEffectiveAt(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        return Status == SubscriptionStatus.Active &&
               StartsAtUtc <= utcNow &&
               (!EndsAtUtc.HasValue || utcNow < EndsAtUtc.Value);
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان اشتراک باید UTC باشد.", parameterName);
        }
    }
}
