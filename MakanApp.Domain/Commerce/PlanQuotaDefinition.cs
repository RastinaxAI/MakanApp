namespace MakanApp.Domain.Commerce;

public sealed class PlanQuotaDefinition
{
    private PlanQuotaDefinition()
    {
    }

    internal PlanQuotaDefinition(Guid planVersionId, QuotaKey key, long? limit)
    {
        Validate(planVersionId, key, limit);
        Id = Guid.NewGuid();
        PlanVersionId = planVersionId;
        Key = key;
        Limit = limit;
    }

    public Guid Id { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public QuotaKey Key { get; private set; }
    public long? Limit { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void SetLimit(long? limit)
    {
        Validate(PlanVersionId, Key, limit);
        Limit = limit;
    }

    private static void Validate(Guid planVersionId, QuotaKey key, long? limit)
    {
        if (planVersionId == Guid.Empty || !Enum.IsDefined(key) || limit < 0)
        {
            throw new ArgumentException("تعریف سهمیه پلن معتبر نیست.");
        }
    }
}
