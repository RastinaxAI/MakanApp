namespace MakanApp.Domain.Commerce;

public sealed class PaymentAttempt
{
    public const int MaximumProviderLength = 50;
    public const int MaximumProviderReferenceLength = 200;
    public const int MaximumPaymentUrlLength = 2048;
    public const int MaximumFailureCodeLength = 100;
    public const int RequestPayloadHashLength = 32;

    private PaymentAttempt()
    {
    }

    private PaymentAttempt(
        Guid id,
        Guid orderId,
        string provider,
        decimal amount,
        CurrencyCode currency,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        if (orderId == Guid.Empty || clientOperationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه سفارش و عملیات پرداخت الزامی است.");
        }

        Provider = NormalizeText(provider, MaximumProviderLength, nameof(provider));
        if (amount <= 0 || amount > PlanVersionPrice.MaximumAmount || !Enum.IsDefined(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "مبلغ یا واحد پول تلاش پرداخت معتبر نیست.");
        }

        if (requestPayloadHash is null || requestPayloadHash.Length != RequestPayloadHashLength)
        {
            throw new ArgumentException("اثر انگشت درخواست پرداخت معتبر نیست.", nameof(requestPayloadHash));
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        Id = id;
        OrderId = orderId;
        Status = PaymentAttemptStatus.Created;
        Amount = amount;
        Currency = currency;
        ClientOperationId = clientOperationId;
        RequestPayloadHash = requestPayloadHash.ToArray();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public PaymentAttemptStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public byte[] RequestPayloadHash { get; private set; } = [];
    public string? ProviderReference { get; private set; }
    public string? PaymentUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? InitiatedAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid? InitiationClaimToken { get; private set; }
    public DateTime? InitiationClaimedUntilUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PaymentAttempt Create(
        Order order,
        string provider,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOperationException("برای سفارش غیرقابل پرداخت نمی‌توان تلاش جدید ساخت.");
        }

        return new PaymentAttempt(
            Guid.NewGuid(),
            order.Id,
            provider,
            order.Amount,
            order.Currency,
            clientOperationId,
            requestPayloadHash,
            createdAtUtc);
    }

    public bool TryClaimInitiation(Guid claimToken, DateTime nowUtc, DateTime claimedUntilUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureUtc(claimedUntilUtc, nameof(claimedUntilUtc));
        if (claimToken == Guid.Empty || claimedUntilUtc <= nowUtc)
        {
            throw new ArgumentException("claim تلاش پرداخت معتبر نیست.");
        }

        var claimable = Status == PaymentAttemptStatus.Created ||
                        Status == PaymentAttemptStatus.Initiating && InitiationClaimedUntilUtc <= nowUtc;
        if (!claimable)
        {
            return false;
        }

        Status = PaymentAttemptStatus.Initiating;
        InitiationClaimToken = claimToken;
        InitiationClaimedUntilUtc = claimedUntilUtc;
        InitiatedAtUtc ??= nowUtc;
        return true;
    }

    public void MarkPendingVerification(
        Guid claimToken,
        string providerReference,
        string paymentUrl,
        DateTime occurredAtUtc)
    {
        EnsureClaim(claimToken);
        ProviderReference = NormalizeText(
            providerReference,
            MaximumProviderReferenceLength,
            nameof(providerReference));
        PaymentUrl = NormalizePaymentUrl(paymentUrl);
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        Status = PaymentAttemptStatus.PendingVerification;
        FailureCode = null;
        ClearClaim();
    }

    public void MarkInitiationFailed(Guid claimToken, string failureCode, DateTime occurredAtUtc)
    {
        EnsureClaim(claimToken);
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        Status = PaymentAttemptStatus.Failed;
        FailureCode = NormalizeText(failureCode, MaximumFailureCodeLength, nameof(failureCode));
        CompletedAtUtc = occurredAtUtc;
        ClearClaim();
    }

    public void MarkInitiationUnknown(
        Guid claimToken,
        string failureCode,
        DateTime occurredAtUtc,
        string? providerReference = null)
    {
        EnsureClaim(claimToken);
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        SetProviderReference(providerReference);
        Status = PaymentAttemptStatus.Unknown;
        FailureCode = NormalizeText(failureCode, MaximumFailureCodeLength, nameof(failureCode));
        ClearClaim();
    }

    public void MarkVerificationFailed(
        string? providerReference,
        string failureCode,
        DateTime verifiedAtUtc)
    {
        EnsureVerifiable();
        EnsureUtc(verifiedAtUtc, nameof(verifiedAtUtc));
        SetProviderReference(providerReference);
        Status = PaymentAttemptStatus.Failed;
        FailureCode = NormalizeText(failureCode, MaximumFailureCodeLength, nameof(failureCode));
        VerifiedAtUtc = verifiedAtUtc;
        CompletedAtUtc = verifiedAtUtc;
        ClearClaim();
    }

    public void MarkVerificationUnknown(
        string? providerReference,
        string failureCode,
        DateTime verifiedAtUtc)
    {
        EnsureVerifiable();
        EnsureUtc(verifiedAtUtc, nameof(verifiedAtUtc));
        SetProviderReference(providerReference);
        Status = PaymentAttemptStatus.Unknown;
        FailureCode = NormalizeText(failureCode, MaximumFailureCodeLength, nameof(failureCode));
        VerifiedAtUtc = verifiedAtUtc;
        CompletedAtUtc = null;
        ClearClaim();
    }

    public void MarkSucceeded(string providerReference, DateTime verifiedAtUtc)
    {
        EnsureVerifiable();
        EnsureUtc(verifiedAtUtc, nameof(verifiedAtUtc));
        SetProviderReference(NormalizeText(
            providerReference,
            MaximumProviderReferenceLength,
            nameof(providerReference)));
        Status = PaymentAttemptStatus.Succeeded;
        FailureCode = null;
        VerifiedAtUtc = verifiedAtUtc;
        CompletedAtUtc = verifiedAtUtc;
        ClearClaim();
    }

    private void EnsureClaim(Guid claimToken)
    {
        if (claimToken == Guid.Empty || Status != PaymentAttemptStatus.Initiating || InitiationClaimToken != claimToken)
        {
            throw new InvalidOperationException("مالکیت initiation تلاش پرداخت معتبر نیست.");
        }
    }

    private void EnsureVerifiable()
    {
        if (Status == PaymentAttemptStatus.Succeeded)
        {
            throw new InvalidOperationException("تلاش پرداخت قبلاً با موفقیت نهایی شده است.");
        }
    }

    private void SetProviderReference(string? providerReference)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            return;
        }

        var normalized = NormalizeText(
            providerReference,
            MaximumProviderReferenceLength,
            nameof(providerReference));
        if (ProviderReference is not null &&
            !ProviderReference.Equals(normalized, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("مرجع provider تلاش پرداخت قابل تغییر نیست.");
        }

        ProviderReference = normalized;
    }

    private void ClearClaim()
    {
        InitiationClaimToken = null;
        InitiationClaimedUntilUtc = null;
    }

    private static string NormalizePaymentUrl(string value)
    {
        var normalized = NormalizeText(value, MaximumPaymentUrlLength, nameof(value));
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("نشانی پرداخت معتبر نیست.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeText(string value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException("متن تلاش پرداخت معتبر نیست.", parameterName);
        }

        return normalized;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تلاش پرداخت باید UTC باشد.", parameterName);
        }
    }
}
