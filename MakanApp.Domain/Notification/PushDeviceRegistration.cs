namespace MakanApp.Domain.Notification;

public sealed class PushDeviceRegistration
{
    public const int MaximumTokenLength = 2048;
    public const int TokenHashLength = 32;

    private PushDeviceRegistration()
    {
    }

    private PushDeviceRegistration(
        Guid id,
        Guid userId,
        Guid userSessionId,
        PushDevicePlatform platform,
        string pushToken,
        byte[] tokenHash,
        DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        UserSessionId = userSessionId;
        Platform = platform;
        PushToken = pushToken;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid UserSessionId { get; private set; }
    public PushDevicePlatform Platform { get; private set; }
    public string PushToken { get; private set; } = string.Empty;
    public byte[] TokenHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public string? WebPushP256dh { get; private set; }
    public string? WebPushAuth { get; private set; }

    public void SetWebPushKeys(string? p256dh, string? auth)
    {
        if (Platform != PushDevicePlatform.Web)
        {
            if (p256dh is not null || auth is not null) throw new ArgumentException("کلید Web Push برای دستگاه موبایل مجاز نیست.");
        }
        else if (p256dh?.Length != 87 || auth?.Length != 22)
            throw new ArgumentException("کلیدهای subscription معتبر نیستند.");
        WebPushP256dh = p256dh;
        WebPushAuth = auth;
    }

    public static PushDeviceRegistration Create(
        Guid userId,
        Guid userSessionId,
        PushDevicePlatform platform,
        string pushToken,
        byte[] tokenHash,
        DateTime createdAtUtc)
    {
        Validate(userId, userSessionId, platform, pushToken, tokenHash, createdAtUtc);
        return new PushDeviceRegistration(
            Guid.NewGuid(), userId, userSessionId, platform,
            pushToken.Trim(), tokenHash.ToArray(), createdAtUtc);
    }

    public void Refresh(
        Guid userSessionId,
        PushDevicePlatform platform,
        string pushToken,
        byte[] tokenHash,
        DateTime lastSeenAtUtc)
    {
        Validate(UserId, userSessionId, platform, pushToken, tokenHash, lastSeenAtUtc);
        if (RevokedAtUtc.HasValue)
        {
            throw new InvalidOperationException("دستگاه لغوشده قابل تازه‌سازی نیست.");
        }

        UserSessionId = userSessionId;
        Platform = platform;
        PushToken = pushToken.Trim();
        TokenHash = tokenHash.ToArray();
        LastSeenAtUtc = lastSeenAtUtc > LastSeenAtUtc ? lastSeenAtUtc : LastSeenAtUtc;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        RevokedAtUtc ??= revokedAtUtc;
    }

    private static void Validate(
        Guid userId,
        Guid userSessionId,
        PushDevicePlatform platform,
        string pushToken,
        byte[] tokenHash,
        DateTime timestampUtc)
    {
        if (userId == Guid.Empty || userSessionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه کاربر و نشست دستگاه الزامی است.");
        }

        if (!Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(nameof(platform));
        }

        var normalizedToken = pushToken?.Trim() ?? string.Empty;
        if (normalizedToken.Length is < 16 or > MaximumTokenLength || normalizedToken.Any(char.IsControl))
        {
            throw new ArgumentException("Push token معتبر نیست.", nameof(pushToken));
        }

        if (tokenHash is null || tokenHash.Length != TokenHashLength)
        {
            throw new ArgumentException("هش Push token معتبر نیست.", nameof(tokenHash));
        }

        EnsureUtc(timestampUtc, nameof(timestampUtc));
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان دستگاه باید UTC باشد.", parameterName);
        }
    }
}
