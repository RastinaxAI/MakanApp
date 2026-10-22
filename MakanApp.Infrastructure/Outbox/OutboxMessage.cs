namespace MakanApp.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const int MaximumMessageTypeLength = 100;
    public const int MaximumPayloadLength = 16000;
    public const int MaximumErrorCodeLength = 100;

    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        string messageType,
        string payload,
        DateTime occurredAtUtc)
    {
        Id = id;
        MessageType = messageType;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? ClaimToken { get; private set; }
    public DateTime? ClaimedUntilUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastErrorCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static OutboxMessage Create(
        Guid id,
        string messageType,
        string payload,
        DateTime occurredAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("شناسه رویداد Outbox الزامی است.", nameof(id));
        }

        messageType = ValidateText(messageType, MaximumMessageTypeLength, nameof(messageType));
        payload = ValidateText(payload, MaximumPayloadLength, nameof(payload));
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        return new OutboxMessage(id, messageType, payload, occurredAtUtc);
    }

    public void Claim(Guid claimToken, DateTime nowUtc, DateTime claimedUntilUtc)
    {
        if (claimToken == Guid.Empty || claimedUntilUtc <= nowUtc || ProcessedAtUtc.HasValue)
        {
            throw new InvalidOperationException("پیام Outbox در وضعیت قابل claim نیست.");
        }

        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureUtc(claimedUntilUtc, nameof(claimedUntilUtc));
        ClaimToken = claimToken;
        ClaimedUntilUtc = claimedUntilUtc;
        NextAttemptAtUtc = null;
        LastErrorCode = null;
        AttemptCount++;
    }

    public void MarkProcessed(Guid claimToken, DateTime processedAtUtc)
    {
        EnsureClaimOwner(claimToken);
        EnsureUtc(processedAtUtc, nameof(processedAtUtc));
        ProcessedAtUtc = processedAtUtc;
        ClaimToken = null;
        ClaimedUntilUtc = null;
        NextAttemptAtUtc = null;
        LastErrorCode = null;
    }

    public void ScheduleRetry(
        Guid claimToken,
        DateTime nextAttemptAtUtc,
        string errorCode)
    {
        EnsureClaimOwner(claimToken);
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        ClaimToken = null;
        ClaimedUntilUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastErrorCode = ValidateText(errorCode, MaximumErrorCodeLength, nameof(errorCode));
    }

    public void MarkFailed(Guid claimToken, string errorCode)
    {
        EnsureClaimOwner(claimToken);
        var normalizedErrorCode = ValidateText(errorCode, MaximumErrorCodeLength, nameof(errorCode));
        ClaimToken = null;
        ClaimedUntilUtc = null;
        NextAttemptAtUtc = null;
        LastErrorCode = normalizedErrorCode;
    }

    private void EnsureClaimOwner(Guid claimToken)
    {
        if (claimToken == Guid.Empty || ClaimToken != claimToken || ProcessedAtUtc.HasValue)
        {
            throw new InvalidOperationException("مالکیت claim پیام Outbox معتبر نیست.");
        }
    }

    private static string ValidateText(string value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException("متن Outbox نامعتبر است.", parameterName);
        }

        return normalized;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان Outbox باید UTC باشد.", parameterName);
        }
    }
}
