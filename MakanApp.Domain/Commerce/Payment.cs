namespace MakanApp.Domain.Commerce;

public sealed class Payment
{
    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid orderId,
        Guid paymentAttemptId,
        string provider,
        string providerReference,
        decimal amount,
        CurrencyCode currency,
        DateTime paidAtUtc)
    {
        Id = id;
        OrderId = orderId;
        PaymentAttemptId = paymentAttemptId;
        Provider = provider;
        ProviderReference = providerReference;
        Amount = amount;
        Currency = currency;
        PaidAtUtc = paidAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PaymentAttemptId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderReference { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public DateTime PaidAtUtc { get; private set; }

    public static Payment Create(PaymentAttempt attempt, DateTime paidAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (attempt.Status != PaymentAttemptStatus.Succeeded ||
            string.IsNullOrWhiteSpace(attempt.ProviderReference))
        {
            throw new InvalidOperationException("پرداخت فقط از تلاش تأییدشده ساخته می‌شود.");
        }

        if (paidAtUtc.Kind != DateTimeKind.Utc || paidAtUtc < attempt.CreatedAtUtc)
        {
            throw new ArgumentException("زمان پرداخت معتبر نیست.", nameof(paidAtUtc));
        }

        return new Payment(
            Guid.NewGuid(),
            attempt.OrderId,
            attempt.Id,
            attempt.Provider,
            attempt.ProviderReference,
            attempt.Amount,
            attempt.Currency,
            paidAtUtc);
    }
}
