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
    Task<PushDevicePageResult> GetPageAsync(Guid userId, Guid sessionId, PushDeviceListQuery query, CancellationToken cancellationToken);

    Task<PushDeviceRegistrationResult> RegisterWebPushAsync(Guid userId, Guid sessionId, RegisterWebPushCommand command, CancellationToken cancellationToken);

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
        bool suppressExternalDelivery,
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
    Task<PushDeviceRegistration> UpsertAsync(Guid userId, Guid sessionId, RegisterPushDeviceCommand command,
        byte[] tokenHash, DateTime nowUtc, CancellationToken cancellationToken);

    Task<PushDevicePageResult> GetPageAsync(Guid userId, PushDeviceListQuery query, DateTime nowUtc, CancellationToken cancellationToken);

    Task<bool> IsActiveSessionAsync(
        Guid sessionId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<PushDeviceRegistration?> GetForUpdateAsync(
        Guid registrationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INotificationDeliveryStore
{
    IAsyncEnumerable<IReadOnlyList<Guid>> GetActivePushDeviceIdBatchesAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Guid?> GetVerifiedMobileCredentialIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<NotificationDeliveryAttempt> attempts);

    // هر batch در transaction موجود Outbox ذخیره می‌شود؛ commit مالک همان transaction است.
    Task AddBatchAsync(IReadOnlyCollection<NotificationDeliveryAttempt> attempts, CancellationToken cancellationToken);
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
