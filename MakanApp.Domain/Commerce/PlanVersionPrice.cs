namespace MakanApp.Domain.Commerce;

public sealed class PlanVersionPrice
{
    public const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    private PlanVersionPrice()
    {
    }

    internal PlanVersionPrice(Guid planVersionId, decimal amount, CurrencyCode currency, bool explicitlyFree = false)
    {
        PlanVersionId = planVersionId;
        if (explicitlyFree && amount == 0) { Amount = 0; Currency = currency; }
        else Change(amount, currency);
    }

    public Guid PlanVersionId { get; private set; }
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void MakeFree()
    {
        Amount = 0;
        Currency = CurrencyCode.Irr;
    }

    internal void Change(decimal amount, CurrencyCode currency)
    {
        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        if (amount <= 0 || amount > MaximumAmount || amount != normalizedAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "مبلغ نسخه پلن باید مثبت و در محدوده decimal(18,2) باشد.");
        }

        if (!Enum.IsDefined(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(currency));
        }

        Amount = normalizedAmount;
        Currency = currency;
    }
}
