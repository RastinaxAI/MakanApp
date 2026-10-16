namespace MakanApp.Domain.Notification;

public sealed class NotificationDeliveryAttempt
{
    public const int MaximumDestinationReferenceLength = 100;
    public const int MaximumProviderMessageIdLength = 200;

    private NotificationDeliveryAttempt()
    {
    }

    private NotificationDeliveryAttempt(
        Guid id,
        Guid notificationId,
        NotificationDeliveryChannel channel,
        string destinationReference,
        NotificationDeliveryStatus status,
        DateTime createdAtUtc,
        DateTime? nextAttemptAtUtc,
        NotificationDeliveryFailureReason? failureReason)
    {
        Id = id;
        NotificationId = notificationId;
        Channel = channel;
        DestinationReference = destinationReference;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = nextAttemptAtUtc;
        FailureReason = failureReason;
    }

    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }
    public NotificationDeliveryChannel Channel { get; private set; }
    public string DestinationReference { get; private set; } = string.Empty;
    public NotificationDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public NotificationDeliveryFailureReason? FailureReason { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public Guid? ClaimToken { get; private set; }
    public DateTime? ClaimedUntilUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static NotificationDeliveryAttempt Schedule(
        Guid notificationId,
        NotificationDeliveryChannel channel,
        string destinationReference,
        DateTime createdAtUtc,
        DateTime deliverAtUtc)
    {
        ValidateIdentity(notificationId, channel, destinationReference);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(deliverAtUtc, nameof(deliverAtUtc));
        if (deliverAtUtc < createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(deliverAtUtc));
        }

        var deferred = deliverAtUtc > createdAtUtc;
        return new NotificationDeliveryAttempt(
            Guid.NewGuid(),
            notificationId,
            channel,
            destinationReference,
            deferred ? NotificationDeliveryStatus.Deferred : NotificationDeliveryStatus.Pending,
            createdAtUtc,
            deferred ? deliverAtUtc : createdAtUtc,
            null);
    }

    public static NotificationDeliveryAttempt Suppress(
        Guid notificationId,
        NotificationDeliveryChannel channel,
        string destinationReference,
        NotificationDeliveryFailureReason reason,
        DateTime createdAtUtc)
    {
        ValidateIdentity(notificationId, channel, destinationReference);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return new NotificationDeliveryAttempt(
            Guid.NewGuid(), notificationId, channel, destinationReference,
            NotificationDeliveryStatus.Suppressed, createdAtUtc, null, reason);
    }

    public void Claim(Guid claimToken, DateTime nowUtc, DateTime claimedUntilUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureUtc(claimedUntilUtc, nameof(claimedUntilUtc));
        var due = Status is NotificationDeliveryStatus.Pending or NotificationDeliveryStatus.Deferred &&
                  (!NextAttemptAtUtc.HasValue || NextAttemptAtUtc <= nowUtc);
        var abandoned = Status == NotificationDeliveryStatus.Processing &&
                        ClaimedUntilUtc.HasValue && ClaimedUntilUtc <= nowUtc;
        if (claimToken == Guid.Empty || claimedUntilUtc <= nowUtc || (!due && !abandoned))
        {
            throw new InvalidOperationException("تلاش ارسال در وضعیت قابل claim نیست.");
        }

        Status = NotificationDeliveryStatus.Processing;
        ClaimToken = claimToken;
        ClaimedUntilUtc = claimedUntilUtc;
        LastAttemptAtUtc = nowUtc;
        NextAttemptAtUtc = null;
        AttemptCount++;
    }

    public void MarkDelivered(Guid claimToken, DateTime deliveredAtUtc, string? providerMessageId)
    {
        EnsureClaimOwner(claimToken);
        EnsureUtc(deliveredAtUtc, nameof(deliveredAtUtc));
        var normalizedProviderMessageId = string.IsNullOrWhiteSpace(providerMessageId)
            ? null
            : providerMessageId.Trim();
        if (normalizedProviderMessageId?.Length > MaximumProviderMessageIdLength)
        {
            throw new ArgumentException("شناسه پیام provider بیش از حد طولانی است.", nameof(providerMessageId));
        }

        Status = NotificationDeliveryStatus.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        ProviderMessageId = normalizedProviderMessageId;
        FailureReason = null;
        ClearClaim();
    }

    public void DeferBeforeDelivery(Guid claimToken, DateTime deliverAtUtc)
    {
        EnsureClaimOwner(claimToken);
        EnsureUtc(deliverAtUtc, nameof(deliverAtUtc));
        if (deliverAtUtc <= LastAttemptAtUtc) throw new ArgumentOutOfRangeException(nameof(deliverAtUtc));
        Status = NotificationDeliveryStatus.Deferred;
        NextAttemptAtUtc = deliverAtUtc;
        AttemptCount--;
        ClearClaim();
    }

    public void FailExhaustedAbandonedClaim(DateTime nowUtc, int maximumAttempts)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        if (maximumAttempts < 1 || AttemptCount < maximumAttempts ||
            Status != NotificationDeliveryStatus.Processing || ClaimedUntilUtc > nowUtc || !ClaimedUntilUtc.HasValue)
            throw new InvalidOperationException("فقط claim رهاشده با سقف تلاش مصرف‌شده قابل پایان‌دادن است.");
        Status = NotificationDeliveryStatus.Failed;
        FailureReason = NotificationDeliveryFailureReason.ProviderUnavailable;
        NextAttemptAtUtc = null;
        ClearClaim();
    }

    public void MarkTransientFailure(
        Guid claimToken,
        NotificationDeliveryFailureReason reason,
        DateTime nextAttemptAtUtc,
        int maximumAttempts)
    {
        EnsureClaimOwner(claimToken);
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        if (!Enum.IsDefined(reason) || maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        FailureReason = reason;
        Status = AttemptCount >= maximumAttempts
            ? NotificationDeliveryStatus.Failed
            : NotificationDeliveryStatus.Deferred;
        NextAttemptAtUtc = Status == NotificationDeliveryStatus.Deferred ? nextAttemptAtUtc : null;
        ClearClaim();
    }

    public void MarkPermanentFailure(
        Guid claimToken,
        NotificationDeliveryFailureReason reason)
    {
        EnsureClaimOwner(claimToken);
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Status = NotificationDeliveryStatus.Failed;
        FailureReason = reason;
        NextAttemptAtUtc = null;
        ClearClaim();
    }

    public void MarkSuppressed(
        Guid claimToken,
        NotificationDeliveryFailureReason reason)
    {
        EnsureClaimOwner(claimToken);
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Status = NotificationDeliveryStatus.Suppressed;
        FailureReason = reason;
        NextAttemptAtUtc = null;
        ClearClaim();
    }

    private static void ValidateIdentity(
        Guid notificationId,
        NotificationDeliveryChannel channel,
        string destinationReference)
    {
        if (notificationId == Guid.Empty || !Enum.IsDefined(channel))
        {
            throw new ArgumentException("شناسه اعلان و کانال ارسال الزامی است.");
        }

        var normalizedReference = destinationReference?.Trim() ?? string.Empty;
        if (normalizedReference.Length is 0 or > MaximumDestinationReferenceLength ||
            normalizedReference.Any(char.IsControl))
        {
            throw new ArgumentException("مرجع مقصد ارسال معتبر نیست.", nameof(destinationReference));
        }
    }

    private void EnsureClaimOwner(Guid claimToken)
    {
        if (claimToken == Guid.Empty || ClaimToken != claimToken ||
            Status != NotificationDeliveryStatus.Processing)
        {
            throw new InvalidOperationException("مالک claim تلاش ارسال معتبر نیست.");
        }
    }

    private void ClearClaim()
    {
        ClaimToken = null;
        ClaimedUntilUtc = null;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تلاش ارسال باید UTC باشد.", parameterName);
        }
    }
}
