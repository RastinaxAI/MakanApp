namespace MakanApp.Domain.Commerce;

public enum PaymentAttemptEventType
{
    Created = 1,
    InitiationStarted = 2,
    InitiationCompleted = 3,
    CallbackReceived = 4,
    VerificationCompleted = 5,
    InquiryCompleted = 6,
    SettlementReplayed = 7,
    InitiationUncertain = 8
}

// سابقه فقط metadata کنترل‌شده دارد؛ payload، پیام درگاه و دادهٔ کارت ذخیره نمی‌شوند.
public sealed class PaymentAttemptEvent
{
    private PaymentAttemptEvent() { }
    public Guid Id { get; private set; }
    public Guid PaymentAttemptId { get; private set; }
    public PaymentAttemptEventType Type { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public int? ProviderResult { get; private set; }
    public int? ProviderStatus { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? CorrelationId { get; private set; }

    public static PaymentAttemptEvent Create(Guid attemptId, PaymentAttemptEventType type, DateTime occurredAtUtc,
        int? providerResult = null, int? providerStatus = null, string? errorCode = null, string? correlationId = null)
    {
        if (attemptId == Guid.Empty || !Enum.IsDefined(type) || occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("رویداد پرداخت معتبر نیست.");
        return new PaymentAttemptEvent
        {
            Id = Guid.NewGuid(), PaymentAttemptId = attemptId, Type = type, OccurredAtUtc = occurredAtUtc,
            ProviderResult = providerResult, ProviderStatus = providerStatus,
            ErrorCode = SafeCode(errorCode, 100), CorrelationId = SafeCode(correlationId, 100)
        };
    }

    private static string? SafeCode(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or ':') ? value : null;
}
