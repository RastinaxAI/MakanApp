using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public sealed class NotificationPreferenceResolver(
    INotificationPreferenceStore store) : INotificationPreferenceResolver
{
    public async Task<EffectiveNotificationPreference> ResolveAsync(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        var scopeAvailable = !organizationId.HasValue ||
            await store.HasActiveOrganizationMembershipAsync(
                userId, organizationId.Value, cancellationToken);
        var explicitPreference = scopeAvailable
            ? await store.GetPreferenceForUpdateAsync(
                userId, organizationId, category, cancellationToken)
            : null;
        var mandatory = NotificationPreferencePolicy.IsMandatory(category);
        var defaults = NotificationPreferencePolicy.GetDefaults(category);
        return new EffectiveNotificationPreference(
            userId,
            organizationId,
            category,
            scopeAvailable,
            true,
            mandatory || (explicitPreference?.PushEnabled ?? defaults.PushEnabled),
            mandatory || (explicitPreference?.SmsEnabled ?? defaults.SmsEnabled),
            mandatory,
            explicitPreference?.QuietHoursEnabled ?? false,
            explicitPreference?.QuietHoursStartLocal,
            explicitPreference?.QuietHoursEndLocal,
            explicitPreference?.TimeZoneId,
            explicitPreference is not null,
            explicitPreference?.UpdatedAtUtc,
            explicitPreference?.RowVersion.ToArray());
    }
}
