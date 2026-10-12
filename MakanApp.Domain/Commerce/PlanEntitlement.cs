namespace MakanApp.Domain.Commerce;

public sealed class PlanEntitlement
{
    private PlanEntitlement()
    {
    }

    internal PlanEntitlement(Guid planVersionId, EntitlementKey key, bool enabled)
    {
        if (planVersionId == Guid.Empty || !Enum.IsDefined(key))
        {
            throw new ArgumentException("تعریف دسترسی پلن معتبر نیست.");
        }

        Id = Guid.NewGuid();
        PlanVersionId = planVersionId;
        Key = key;
        Enabled = enabled;
    }

    public Guid Id { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public EntitlementKey Key { get; private set; }
    public bool Enabled { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void SetEnabled(bool enabled) => Enabled = enabled;
}
