using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public sealed class NotificationPreferenceService(
    INotificationPreferenceStore store,
    INotificationPreferenceResolver resolver,
    TimeProvider timeProvider) : INotificationPreferenceService
{
    public async Task<IReadOnlyCollection<NotificationPreferenceItem>> GetAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        await EnsureScopeAsync(userId, organizationId, cancellationToken);
        var results = new List<NotificationPreferenceItem>();
        foreach (var category in Enum.GetValues<NotificationPreferenceCategory>())
        {
            results.Add(ToItem(await resolver.ResolveAsync(
                userId, organizationId, category, cancellationToken)));
        }

        return results;
    }

    public async Task<NotificationPreferenceItem> UpdateAsync(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        UpdateNotificationPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(category))
        {
            throw Invalid("دسته تنظیم اعلان معتبر نیست.");
        }

        await EnsureScopeAsync(userId, organizationId, cancellationToken);
        var isMandatory = NotificationPreferencePolicy.IsMandatory(category);
        if (isMandatory && (!command.PushEnabled || !command.SmsEnabled))
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.Mandatory,
                "کانال‌های اعلان امنیتی اجباری قابل غیرفعال‌کردن نیستند.");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var preference = await store.GetPreferenceForUpdateAsync(
            userId, organizationId, category, cancellationToken);
        try
        {
            if (preference is null)
            {
                if (!string.IsNullOrWhiteSpace(command.ExpectedRowVersion))
                {
                    throw ConcurrencyConflict();
                }

                preference = NotificationPreference.Create(
                    userId,
                    organizationId,
                    category,
                    command.PushEnabled,
                    command.SmsEnabled,
                    command.QuietHoursEnabled,
                    command.QuietHoursStartLocal,
                    command.QuietHoursEndLocal,
                    command.TimeZoneId,
                    nowUtc);
                store.Add(preference);
            }
            else
            {
                store.SetOriginalRowVersion(
                    preference,
                    DecodeRequiredRowVersion(command.ExpectedRowVersion));
                preference.Update(
                    command.PushEnabled,
                    command.SmsEnabled,
                    command.QuietHoursEnabled,
                    command.QuietHoursStartLocal,
                    command.QuietHoursEndLocal,
                    command.TimeZoneId,
                    nowUtc);
            }
        }
        catch (NotificationPreferenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw Invalid(exception.Message);
        }

        await store.SaveChangesAsync(cancellationToken);
        return ToItem(await resolver.ResolveAsync(userId, organizationId, category, cancellationToken));
    }

    private async Task EnsureScopeAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty ||
            organizationId.HasValue && !await store.HasActiveOrganizationMembershipAsync(
                userId, organizationId.Value, cancellationToken))
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.ScopeNotFound,
                "بافت تنظیم اعلان در دسترس نیست.");
        }
    }

    private static NotificationPreferenceItem ToItem(EffectiveNotificationPreference preference) =>
        new(
            preference.OrganizationId.HasValue
                ? NotificationPreferenceScope.Organization
                : NotificationPreferenceScope.Personal,
            preference.OrganizationId,
            preference.Category,
            preference.InAppEnabled,
            preference.PushEnabled,
            preference.SmsEnabled,
            preference.IsMandatory,
            !preference.IsMandatory,
            !preference.IsMandatory,
            preference.QuietHoursEnabled,
            preference.QuietHoursStartLocal,
            preference.QuietHoursEndLocal,
            preference.TimeZoneId,
            preference.IsExplicit,
            preference.UpdatedAtUtc,
            preference.RowVersion is null ? null : Convert.ToBase64String(preference.RowVersion));

    private static byte[] DecodeRequiredRowVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw ConcurrencyConflict();
        }

        try
        {
            var bytes = Convert.FromBase64String(rowVersion);
            return bytes.Length == 8 ? bytes : throw ConcurrencyConflict();
        }
        catch (FormatException)
        {
            throw ConcurrencyConflict();
        }
    }

    private static NotificationPreferenceException Invalid(string message) =>
        new(NotificationPreferenceErrorCodes.Invalid, message);

    private static NotificationPreferenceException ConcurrencyConflict() =>
        new(
            NotificationPreferenceErrorCodes.ConcurrencyConflict,
            "تنظیم اعلان هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید.");
}
