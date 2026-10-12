using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public enum NotificationPreferenceScope
{
    Personal = 1,
    Organization = 2
}

public sealed record UpdateNotificationPreferenceCommand(
    bool PushEnabled,
    bool SmsEnabled,
    bool QuietHoursEnabled,
    TimeOnly? QuietHoursStartLocal,
    TimeOnly? QuietHoursEndLocal,
    string? TimeZoneId,
    string? ExpectedRowVersion);

public sealed record NotificationPreferenceItem(
    NotificationPreferenceScope Scope,
    Guid? OrganizationId,
    NotificationPreferenceCategory Category,
    bool InAppEnabled,
    bool PushEnabled,
    bool SmsEnabled,
    bool IsMandatory,
    bool CanDisablePush,
    bool CanDisableSms,
    bool QuietHoursEnabled,
    TimeOnly? QuietHoursStartLocal,
    TimeOnly? QuietHoursEndLocal,
    string? TimeZoneId,
    bool IsExplicit,
    DateTime? UpdatedAtUtc,
    string? RowVersion);

public sealed record RegisterPushDeviceCommand(
    PushDevicePlatform Platform,
    string PushToken);

public sealed record PushDeviceRegistrationResult(
    Guid Id,
    PushDevicePlatform Platform,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc);

public sealed record EffectiveNotificationPreference(
    Guid UserId,
    Guid? OrganizationId,
    NotificationPreferenceCategory Category,
    bool ScopeAvailable,
    bool InAppEnabled,
    bool PushEnabled,
    bool SmsEnabled,
    bool IsMandatory,
    bool QuietHoursEnabled,
    TimeOnly? QuietHoursStartLocal,
    TimeOnly? QuietHoursEndLocal,
    string? TimeZoneId,
    bool IsExplicit,
    DateTime? UpdatedAtUtc,
    byte[]? RowVersion);

public static class NotificationPreferenceErrorCodes
{
    public const string Invalid = "notification_preference_invalid";
    public const string ScopeNotFound = "notification_preference_scope_not_found";
    public const string Mandatory = "notification_preference_mandatory";
    public const string ConcurrencyConflict = "notification_preference_concurrency_conflict";
    public const string DeviceNotFound = "notification_device_not_found";
    public const string DeviceConflict = "notification_device_conflict";
}

public sealed class NotificationPreferenceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
