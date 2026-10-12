namespace MakanApp.Domain.Commerce;

public sealed class Order
{
    public const int RequestPayloadHashLength = 32;

    private Order()
    {
    }

    private Order(
        Guid id,
        Guid purchaserUserId,
        Guid? organizationId,
        Guid planVersionId,
        decimal amount,
        CurrencyCode currency,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        if (purchaserUserId == Guid.Empty || planVersionId == Guid.Empty || clientOperationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه خریدار، نسخه پلن و عملیات الزامی است.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه سازمان در صورت ارائه باید معتبر باشد.", nameof(organizationId));
        }

        EnsureAmountAndCurrency(amount, currency);
        EnsureHash(requestPayloadHash);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        Id = id;
        PurchaserUserId = purchaserUserId;
        OrganizationId = organizationId;
        PlanVersionId = planVersionId;
        Status = OrderStatus.PendingPayment;
        Amount = amount;
        Currency = currency;
        ClientOperationId = clientOperationId;
        RequestPayloadHash = requestPayloadHash.ToArray();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PurchaserUserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public byte[] RequestPayloadHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Order Create(
        Guid purchaserUserId,
        Guid? organizationId,
        PlanVersion planVersion,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(planVersion);
        if (!planVersion.IsPublished || planVersion.Price is null)
        {
            throw new InvalidOperationException("نسخه پلن برای خرید منتشر و قیمت‌گذاری نشده است.");
        }

        return new Order(
            Guid.NewGuid(),
            purchaserUserId,
            organizationId,
            planVersion.Id,
            planVersion.Price.Amount,
            planVersion.Price.Currency,
            clientOperationId,
            requestPayloadHash,
            createdAtUtc);
    }

    public void MarkPaid(DateTime paidAtUtc)
    {
        EnsureUtc(paidAtUtc, nameof(paidAtUtc));
        if (Status != OrderStatus.PendingPayment || paidAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("سفارش در وضعیت قابل پرداخت نیست.");
        }

        Status = OrderStatus.Paid;
        PaidAtUtc = paidAtUtc;
    }

    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureUtc(cancelledAtUtc, nameof(cancelledAtUtc));
        if (Status != OrderStatus.PendingPayment || cancelledAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("فقط سفارش پرداخت‌نشده قابل لغو است.");
        }

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    private static void EnsureAmountAndCurrency(decimal amount, CurrencyCode currency)
    {
        if (amount <= 0 || amount > PlanVersionPrice.MaximumAmount || !Enum.IsDefined(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "مبلغ یا واحد پول سفارش معتبر نیست.");
        }
    }

    private static void EnsureHash(byte[] hash)
    {
        if (hash is null || hash.Length != RequestPayloadHashLength)
        {
            throw new ArgumentException("اثر انگشت درخواست سفارش معتبر نیست.", nameof(hash));
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان سفارش باید UTC باشد.", parameterName);
        }
    }
}
