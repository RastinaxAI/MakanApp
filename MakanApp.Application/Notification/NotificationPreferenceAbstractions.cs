using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public interface INotificationPreferenceService
{
    Task<IReadOnlyCollection<NotificationPreferenceItem>> GetAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken cancellationToken);

    Task<NotificationPreferenceItem> UpdateAsync(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        UpdateNotificationPreferenceCommand command,
        CancellationToken cancellationToken);
}

public interface IPushDeviceService
{
    Task<PushDeviceRegistrationResult> RegisterAsync(
        Guid userId,
        Guid sessionId,
        RegisterPushDeviceCommand command,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        Guid userId,
        Guid registrationId,
        CancellationToken cancellationToken);
}

public interface INotificationPreferenceResolver
{
    Task<EffectiveNotificationPreference> ResolveAsync(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        CancellationToken cancellationToken);
}

public interface INotificationDeliveryCoordinator
{
    Task ScheduleAsync(
        Domain.Notification.Notification notification,
        CancellationToken cancellationToken);
}

public interface INotificationPreferenceStore
{
    Task<bool> HasActiveOrganizationMembershipAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<NotificationPreference?> GetPreferenceForUpdateAsync(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        CancellationToken cancellationToken);

    void Add(NotificationPreference preference);
    void SetOriginalRowVersion(NotificationPreference preference, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPushDeviceStore
{
    Task<bool> IsActiveSessionAsync(
        Guid sessionId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<PushDeviceRegistration?> GetActiveByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken);

    Task<PushDeviceRegistration?> GetForUpdateAsync(
        Guid registrationId,
        Guid userId,
        CancellationToken cancellationToken);

    void Add(PushDeviceRegistration registration);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INotificationDeliveryStore
{
    Task<IReadOnlyCollection<Guid>> GetActivePushDeviceIdsAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Guid?> GetVerifiedMobileCredentialIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<NotificationDeliveryAttempt> attempts);
}

public interface IPushNotificationProvider
{
    Task<DeliveryProviderResult> SendAsync(
        PushNotificationDelivery delivery,
        CancellationToken cancellationToken);
}

public interface INotificationSmsProvider
{
    Task<DeliveryProviderResult> SendAsync(
        NotificationSmsDelivery delivery,
        CancellationToken cancellationToken);
}
