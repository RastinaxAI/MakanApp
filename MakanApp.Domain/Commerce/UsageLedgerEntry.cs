namespace MakanApp.Domain.Commerce;

public sealed class UsageLedgerEntry
{
    private UsageLedgerEntry()
    {
    }

    private UsageLedgerEntry(
        Guid id,
        Guid organizationId,
        Guid subscriptionId,
        QuotaKey quotaKey,
        long amount,
        Guid sourceOperationId,
        DateTime occurredAtUtc)
    {
        if (organizationId == Guid.Empty || subscriptionId == Guid.Empty ||
            sourceOperationId == Guid.Empty || !Enum.IsDefined(quotaKey) || amount <= 0)
        {
            throw new ArgumentException("رکورد مصرف معتبر نیست.");
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان مصرف باید UTC باشد.", nameof(occurredAtUtc));
        }

        Id = id;
        OrganizationId = organizationId;
        SubscriptionId = subscriptionId;
        QuotaKey = quotaKey;
        Amount = amount;
        SourceOperationId = sourceOperationId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public QuotaKey QuotaKey { get; private set; }
    public long Amount { get; private set; }
    public Guid SourceOperationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static UsageLedgerEntry Create(
        Guid organizationId,
        Guid subscriptionId,
        QuotaKey quotaKey,
        long amount,
        Guid sourceOperationId,
        DateTime occurredAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            subscriptionId,
            quotaKey,
            amount,
            sourceOperationId,
            occurredAtUtc);
}
