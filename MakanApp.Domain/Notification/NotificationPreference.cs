namespace MakanApp.Domain.Notification;

public sealed class NotificationPreference
{
    public const int MaximumTimeZoneIdLength = 100;

    private NotificationPreference()
    {
    }

    private NotificationPreference(
        Guid id,
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        bool pushEnabled,
        bool smsEnabled,
        bool quietHoursEnabled,
        TimeOnly? quietHoursStartLocal,
        TimeOnly? quietHoursEndLocal,
        string? timeZoneId,
        DateTime updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        OrganizationId = organizationId;
        Category = category;
        PushEnabled = pushEnabled;
        SmsEnabled = smsEnabled;
        QuietHoursEnabled = quietHoursEnabled;
        QuietHoursStartLocal = quietHoursStartLocal;
        QuietHoursEndLocal = quietHoursEndLocal;
        TimeZoneId = timeZoneId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public NotificationPreferenceCategory Category { get; private set; }
    public bool PushEnabled { get; private set; }
    public bool SmsEnabled { get; private set; }
    public bool QuietHoursEnabled { get; private set; }
    public TimeOnly? QuietHoursStartLocal { get; private set; }
    public TimeOnly? QuietHoursEndLocal { get; private set; }
    public string? TimeZoneId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static NotificationPreference Create(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        bool pushEnabled,
        bool smsEnabled,
        bool quietHoursEnabled,
        TimeOnly? quietHoursStartLocal,
        TimeOnly? quietHoursEndLocal,
        string? timeZoneId,
        DateTime updatedAtUtc)
    {
        Validate(
            userId,
            organizationId,
            category,
            quietHoursEnabled,
            quietHoursStartLocal,
            quietHoursEndLocal,
            timeZoneId,
            updatedAtUtc);
        return new NotificationPreference(
            Guid.NewGuid(),
            userId,
            organizationId,
            category,
            pushEnabled,
            smsEnabled,
            quietHoursEnabled,
            quietHoursStartLocal,
            quietHoursEndLocal,
            NormalizeTimeZoneId(timeZoneId),
            updatedAtUtc);
    }

    public void Update(
        bool pushEnabled,
        bool smsEnabled,
        bool quietHoursEnabled,
        TimeOnly? quietHoursStartLocal,
        TimeOnly? quietHoursEndLocal,
        string? timeZoneId,
        DateTime updatedAtUtc)
    {
        Validate(
            UserId,
            OrganizationId,
            Category,
            quietHoursEnabled,
            quietHoursStartLocal,
            quietHoursEndLocal,
            timeZoneId,
            updatedAtUtc);
        PushEnabled = pushEnabled;
        SmsEnabled = smsEnabled;
        QuietHoursEnabled = quietHoursEnabled;
        QuietHoursStartLocal = quietHoursStartLocal;
        QuietHoursEndLocal = quietHoursEndLocal;
        TimeZoneId = NormalizeTimeZoneId(timeZoneId);
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void Validate(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        bool quietHoursEnabled,
        TimeOnly? quietHoursStartLocal,
        TimeOnly? quietHoursEndLocal,
        string? timeZoneId,
        DateTime updatedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه کاربر تنظیم اعلان الزامی است.", nameof(userId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه سازمان در صورت ارائه باید معتبر باشد.", nameof(organizationId));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), "دسته تنظیم اعلان معتبر نیست.");
        }

        if (updatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تغییر تنظیم اعلان باید UTC باشد.", nameof(updatedAtUtc));
        }

        if (!quietHoursEnabled)
        {
            if (quietHoursStartLocal.HasValue || quietHoursEndLocal.HasValue ||
                !string.IsNullOrWhiteSpace(timeZoneId))
            {
                throw new ArgumentException("هنگام غیرفعال بودن ساعات سکوت، زمان و منطقه زمانی نباید ارسال شوند.");
            }

            return;
        }

        var normalizedTimeZoneId = NormalizeTimeZoneId(timeZoneId);
        if (!quietHoursStartLocal.HasValue || !quietHoursEndLocal.HasValue ||
            quietHoursStartLocal == quietHoursEndLocal || normalizedTimeZoneId is null)
        {
            throw new ArgumentException("برای ساعات سکوت، شروع، پایان متفاوت و منطقه زمانی معتبر الزامی است.");
        }

        if (normalizedTimeZoneId.Length > MaximumTimeZoneIdLength)
        {
            throw new ArgumentException("طول شناسه منطقه زمانی معتبر نیست.", nameof(timeZoneId));
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedTimeZoneId);
    }

    private static string? NormalizeTimeZoneId(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId.Trim();
}
