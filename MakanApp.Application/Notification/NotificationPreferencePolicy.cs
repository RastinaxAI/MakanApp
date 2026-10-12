using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public static class NotificationPreferencePolicy
{
    public static bool IsMandatory(NotificationPreferenceCategory category) =>
        category == NotificationPreferenceCategory.AccountSecurity;

    public static NotificationPreferenceCategory Map(NotificationType type) => type switch
    {
        NotificationType.AssignmentPublished or NotificationType.ExamPublished =>
            NotificationPreferenceCategory.Assignments,
        NotificationType.AssignmentGradeReleased or NotificationType.ExamGradeReleased =>
            NotificationPreferenceCategory.Results,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static (bool PushEnabled, bool SmsEnabled) GetDefaults(
        NotificationPreferenceCategory category) =>
        IsMandatory(category) ? (true, true) : (true, false);
}

public static class NotificationQuietHoursPolicy
{
    public static DateTime GetNextAllowedUtc(
        EffectiveNotificationPreference preference,
        DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان ارزیابی ساعات سکوت باید UTC باشد.", nameof(nowUtc));
        }

        if (preference.IsMandatory || !preference.QuietHoursEnabled)
        {
            return nowUtc;
        }

        if (!preference.QuietHoursStartLocal.HasValue ||
            !preference.QuietHoursEndLocal.HasValue ||
            string.IsNullOrWhiteSpace(preference.TimeZoneId))
        {
            throw new InvalidOperationException("پیکربندی ساعات سکوت کامل نیست.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localTime = TimeOnly.FromDateTime(localNow);
        var start = preference.QuietHoursStartLocal.Value;
        var end = preference.QuietHoursEndLocal.Value;
        var crossesMidnight = start > end;
        var isQuiet = crossesMidnight
            ? localTime >= start || localTime < end
            : localTime >= start && localTime < end;
        if (!isQuiet)
        {
            return nowUtc;
        }

        var endDate = localNow.Date;
        if (crossesMidnight && localTime >= start)
        {
            endDate = endDate.AddDays(1);
        }

        var localEnd = DateTime.SpecifyKind(endDate + end.ToTimeSpan(), DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(localEnd))
        {
            localEnd = localEnd.AddMinutes(1);
        }

        var allowedUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        return allowedUtc > nowUtc ? allowedUtc : nowUtc;
    }
}
