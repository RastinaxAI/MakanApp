namespace MakanApp.Domain.Commerce;

public sealed partial class PlanVersion
{
    private readonly List<PlanEntitlement> _entitlements = [];
    private readonly List<PlanQuotaDefinition> _quotaDefinitions = [];

    private PlanVersion()
    {
    }

    private PlanVersion(
        Guid id,
        Guid planId,
        int versionNumber,
        DateTime createdAtUtc)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("شناسه پلن الزامی است.", nameof(planId));
        }

        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "شماره نسخه باید مثبت باشد.");
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        Id = id;
        PlanId = planId;
        VersionNumber = versionNumber;
        Status = PlanVersionStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PlanId { get; private set; }
    public int VersionNumber { get; private set; }
    public PlanVersionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public PlanVersionPrice? Price { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PlanEntitlement> Entitlements => _entitlements;
    public IReadOnlyCollection<PlanQuotaDefinition> QuotaDefinitions => _quotaDefinitions;
    public bool IsPublished => Status == PlanVersionStatus.Published;

    public static PlanVersion CreateDraft(Guid planId, int versionNumber, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), planId, versionNumber, createdAtUtc);

    public void SetEntitlement(EntitlementKey key, bool enabled)
    {
        EnsureDraft();
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        var current = _entitlements.SingleOrDefault(item => item.Key == key);
        if (current is null)
        {
            _entitlements.Add(new PlanEntitlement(Id, key, enabled));
            return;
        }

        current.SetEnabled(enabled);
    }

    public void SetQuota(QuotaKey key, long? limit)
    {
        EnsureDraft();
        if (!Enum.IsDefined(key) || limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "سقف سهمیه باید صفر، مثبت یا نامحدود باشد.");
        }

        var current = _quotaDefinitions.SingleOrDefault(item => item.Key == key);
        if (current is null)
        {
            _quotaDefinitions.Add(new PlanQuotaDefinition(Id, key, limit));
            return;
        }

        current.SetLimit(limit);
    }

    public void SetPrice(decimal amount, CurrencyCode currency)
    {
        EnsureDraft();
        if (Price is null)
        {
            Price = new PlanVersionPrice(Id, amount, currency);
            ExplicitlyFree = false;
            return;
        }

        Price.Change(amount, currency);
        ExplicitlyFree = false;
    }

    public void Publish(DateTime publishedAtUtc, SubscriptionOwnerType? audience = null)
    {
        EnsureDraft();
        var missing = ValidatePublication(audience);
        if (missing.Count != 0) throw new InvalidOperationException("شرایط تجاری ناقص است: " + string.Join(", ", missing));
        EnsureUtc(publishedAtUtc, nameof(publishedAtUtc));
        if (publishedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(publishedAtUtc));
        }

        Status = PlanVersionStatus.Published;
        PublishedAtUtc = publishedAtUtc;
    }

    private void EnsureDraft()
    {
        if (Status != PlanVersionStatus.Draft)
        {
            throw new InvalidOperationException("شرایط تجاری نسخه منتشرشده قابل ویرایش نیست؛ نسخه جدید بسازید.");
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان نسخه پلن باید UTC باشد.", parameterName);
        }
    }
}
