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
    string PushToken,
    string? P256dh = null,
    string? Auth = null,
    string? ExpectedVersion = null);

public sealed record RegisterWebPushCommand(string Endpoint, string P256dh, string Auth, string? ExpectedVersion = null);

public sealed record PushDeviceListQuery(int Page = 1, int PageSize = 30, bool IncludeRevoked = false);

public sealed record PushDeviceListItem(Guid Id, PushDevicePlatform Platform, DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc, DateTime? RevokedAtUtc, bool IsActive, string Version);

public sealed record PushDevicePageResult(IReadOnlyCollection<PushDeviceListItem> Items, int Page, int PageSize, int TotalCount);

public sealed class PushDeviceOptions
{
    public IReadOnlyCollection<string> AllowedWebPushHosts { get; init; } = [];
}

public sealed record PushDeviceRegistrationResult(
    Guid Id,
    PushDevicePlatform Platform,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    string? Version = null);

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
    public const string PushUnavailable = "notification_push_unavailable";
}

public sealed class NotificationPreferenceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
