using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public sealed class NotificationDeliveryCoordinator(
    INotificationPreferenceResolver preferenceResolver,
    INotificationDeliveryStore store,
    TimeProvider timeProvider) : INotificationDeliveryCoordinator
{
    private const string PolicyDestination = "policy";
    private const string AvailabilityDestination = "availability";
    private const string ScopeDestination = "scope";

    public async Task ScheduleAsync(
        Domain.Notification.Notification notification,
        bool suppressExternalDelivery,
        CancellationToken cancellationToken)
    {
        var category = NotificationPreferencePolicy.Map(notification.Type);
        var preference = await preferenceResolver.ResolveAsync(
            notification.RecipientUserId,
            notification.OrganizationId,
            category,
            cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var attempts = new List<NotificationDeliveryAttempt>();
        if (suppressExternalDelivery)
        {
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Push,
                PolicyDestination,
                NotificationDeliveryFailureReason.SuppressedByPreference,
                nowUtc));
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Sms,
                PolicyDestination,
                NotificationDeliveryFailureReason.SuppressedByPreference,
                nowUtc));
            store.AddRange(attempts);
            return;
        }

        if (!preference.ScopeAvailable)
        {
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Push,
                ScopeDestination,
                NotificationDeliveryFailureReason.OrganizationAccessRevoked,
                nowUtc));
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Sms,
                ScopeDestination,
                NotificationDeliveryFailureReason.OrganizationAccessRevoked,
                nowUtc));
            store.AddRange(attempts);
            return;
        }

        var deliverAtUtc = NotificationQuietHoursPolicy.GetNextAllowedUtc(preference, nowUtc);
        await AddPushAttemptsAsync(notification, preference, nowUtc, deliverAtUtc, attempts, cancellationToken);
        await AddSmsAttemptAsync(notification, preference, nowUtc, deliverAtUtc, attempts, cancellationToken);
        store.AddRange(attempts);
    }

    private async Task AddPushAttemptsAsync(
        Domain.Notification.Notification notification,
        EffectiveNotificationPreference preference,
        DateTime nowUtc,
        DateTime deliverAtUtc,
        ICollection<NotificationDeliveryAttempt> attempts,
        CancellationToken cancellationToken)
    {
        if (!preference.PushEnabled)
        {
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Push,
                PolicyDestination,
                NotificationDeliveryFailureReason.SuppressedByPreference,
                nowUtc));
            return;
        }

        var hasDevice = false;
        await foreach (var deviceIds in store.GetActivePushDeviceIdBatchesAsync(
            notification.RecipientUserId, nowUtc, cancellationToken))
        {
            hasDevice = true;
            var batch = deviceIds.Select(deviceId => NotificationDeliveryAttempt.Schedule(
                notification.Id,
                NotificationDeliveryChannel.Push,
                deviceId.ToString("N"),
                nowUtc,
                deliverAtUtc)).ToArray();
            await store.AddBatchAsync(batch, cancellationToken);
        }

        if (!hasDevice)
        {
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Push,
                AvailabilityDestination,
                NotificationDeliveryFailureReason.NoDevice,
                nowUtc));
            return;
        }
    }

    private async Task AddSmsAttemptAsync(
        Domain.Notification.Notification notification,
        EffectiveNotificationPreference preference,
        DateTime nowUtc,
        DateTime deliverAtUtc,
        ICollection<NotificationDeliveryAttempt> attempts,
        CancellationToken cancellationToken)
    {
        if (!preference.SmsEnabled)
        {
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Sms,
                PolicyDestination,
                NotificationDeliveryFailureReason.SuppressedByPreference,
                nowUtc));
            return;
        }

        var credentialId = await store.GetVerifiedMobileCredentialIdAsync(
            notification.RecipientUserId, cancellationToken);
        if (!credentialId.HasValue)
        {
            attempts.Add(NotificationDeliveryAttempt.Suppress(
                notification.Id,
                NotificationDeliveryChannel.Sms,
                AvailabilityDestination,
                NotificationDeliveryFailureReason.NoSmsDestination,
                nowUtc));
            return;
        }

        attempts.Add(NotificationDeliveryAttempt.Schedule(
            notification.Id,
            NotificationDeliveryChannel.Sms,
            credentialId.Value.ToString("N"),
            nowUtc,
            deliverAtUtc));
    }
}
