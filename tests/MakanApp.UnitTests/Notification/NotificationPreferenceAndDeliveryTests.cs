using System.Text.Json;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.UnitTests.Notification;

public sealed class NotificationPreferenceAndDeliveryTests
{
    [Fact]
    public void PreferenceRequiresCompleteExplicitQuietHoursConfiguration()
    {
        var exception = Assert.Throws<ArgumentException>(() => NotificationPreference.Create(
            Guid.NewGuid(),
            null,
            NotificationPreferenceCategory.Assignments,
            true,
            false,
            true,
            new TimeOnly(22, 0),
            null,
            "UTC",
            Utc(2026, 10, 12, 10)));

        Assert.Contains("ساعات سکوت", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreferenceRejectsUnknownTimeZoneAndEqualQuietHourBoundaries()
    {
        Assert.ThrowsAny<TimeZoneNotFoundException>(() => NotificationPreference.Create(
            Guid.NewGuid(), null, NotificationPreferenceCategory.Results,
            true, false, true, new TimeOnly(22, 0), new TimeOnly(7, 0),
            "Makan/Unknown", Utc(2026, 10, 12, 10)));
        Assert.Throws<ArgumentException>(() => NotificationPreference.Create(
            Guid.NewGuid(), null, NotificationPreferenceCategory.Results,
            true, false, true, new TimeOnly(7, 0), new TimeOnly(7, 0),
            "UTC", Utc(2026, 10, 12, 10)));
    }

    [Theory]
    [MemberData(nameof(QuietHourCases))]
    public void QuietHoursCalculateExpectedDeliveryTime(
        bool enabled,
        bool mandatory,
        TimeOnly start,
        TimeOnly end,
        DateTime nowUtc,
        DateTime expectedUtc)
    {
        var preference = Effective(enabled, mandatory, start, end);

        var actual = NotificationQuietHoursPolicy.GetNextAllowedUtc(preference, nowUtc);

        Assert.Equal(expectedUtc, actual);
    }

    public static TheoryData<bool, bool, TimeOnly, TimeOnly, DateTime, DateTime> QuietHourCases => new()
    {
        { false, false, new(9, 0), new(17, 0), Utc(2026, 10, 12, 10), Utc(2026, 10, 12, 10) },
        { true, false, new(9, 0), new(17, 0), Utc(2026, 10, 12, 10), Utc(2026, 10, 12, 17) },
        { true, false, new(9, 0), new(17, 0), Utc(2026, 10, 12, 8), Utc(2026, 10, 12, 8) },
        { true, false, new(9, 0), new(17, 0), Utc(2026, 10, 12, 9), Utc(2026, 10, 12, 17) },
        { true, false, new(9, 0), new(17, 0), Utc(2026, 10, 12, 17), Utc(2026, 10, 12, 17) },
        { true, false, new(22, 0), new(7, 0), Utc(2026, 10, 12, 23), Utc(2026, 10, 13, 7) },
        { true, false, new(22, 0), new(7, 0), Utc(2026, 10, 12, 6), Utc(2026, 10, 12, 7) },
        { true, false, new(22, 0), new(7, 0), Utc(2026, 10, 12, 12), Utc(2026, 10, 12, 12) },
        { true, false, new(22, 0), new(7, 0), Utc(2026, 10, 12, 7), Utc(2026, 10, 12, 7) },
        { true, true, new(22, 0), new(7, 0), Utc(2026, 10, 12, 23), Utc(2026, 10, 12, 23) }
    };

    [Fact]
    public void DeliveryAttemptHasFiniteRetryAndTerminalLifecycle()
    {
        var nowUtc = Utc(2026, 10, 12, 10);
        var attempt = NotificationDeliveryAttempt.Schedule(
            Guid.NewGuid(), NotificationDeliveryChannel.Push, Guid.NewGuid().ToString("N"),
            nowUtc, nowUtc);

        var firstClaim = Guid.NewGuid();
        attempt.Claim(firstClaim, nowUtc, nowUtc.AddSeconds(30));
        attempt.MarkTransientFailure(
            firstClaim,
            NotificationDeliveryFailureReason.ProviderUnavailable,
            nowUtc.AddSeconds(5),
            2);
        Assert.Equal(NotificationDeliveryStatus.Deferred, attempt.Status);

        var secondClaim = Guid.NewGuid();
        attempt.Claim(secondClaim, nowUtc.AddSeconds(5), nowUtc.AddSeconds(35));
        attempt.MarkTransientFailure(
            secondClaim,
            NotificationDeliveryFailureReason.ProviderUnavailable,
            nowUtc.AddSeconds(15),
            2);

        Assert.Equal(NotificationDeliveryStatus.Failed, attempt.Status);
        Assert.Equal(2, attempt.AttemptCount);
        Assert.Null(attempt.NextAttemptAtUtc);
    }

    [Fact]
    public void ExternalPayloadUsesSafePreviewAndNoInternalSensitiveBody()
    {
        var notification = DomainNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationCategory.Educational,
            NotificationType.ExamGradeReleased,
            "نتیجه آزمون دانش‌آموز",
            "نمره 7 از 20، پاسخ مخفی و یادداشت خصوصی ارزیاب",
            NotificationResourceType.ExamAttempt,
            Guid.NewGuid(),
            Utc(2026, 10, 12, 10));

        var push = NotificationExternalContentPolicy.CreatePush(notification);
        var sms = NotificationExternalContentPolicy.CreateSms(notification);
        var serialized = JsonSerializer.Serialize(new { push, sms });

        Assert.DoesNotContain("نمره 7 از 20", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("پاسخ مخفی", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("یادداشت خصوصی", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(notification.Id.ToString("D"), push.DeepLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PushDeviceRejectsShortOrControlCharacterToken()
    {
        Assert.Throws<ArgumentException>(() => PushDeviceRegistration.Create(
            Guid.NewGuid(), Guid.NewGuid(), PushDevicePlatform.Android,
            "short", new byte[32], Utc(2026, 10, 12, 10)));
        Assert.Throws<ArgumentException>(() => PushDeviceRegistration.Create(
            Guid.NewGuid(), Guid.NewGuid(), PushDevicePlatform.Android,
            "valid-token\n-value-123", new byte[32], Utc(2026, 10, 12, 10)));
    }

    private static EffectiveNotificationPreference Effective(
        bool enabled,
        bool mandatory,
        TimeOnly start,
        TimeOnly end) =>
        new(
            Guid.NewGuid(),
            null,
            NotificationPreferenceCategory.Results,
            true,
            true,
            true,
            false,
            mandatory,
            enabled,
            enabled ? start : null,
            enabled ? end : null,
            enabled ? "UTC" : null,
            false,
            null,
            null);

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);
}
