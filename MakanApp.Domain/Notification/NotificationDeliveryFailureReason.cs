namespace MakanApp.Domain.Notification;

public enum NotificationDeliveryFailureReason
{
    SuppressedByPreference = 1,
    NoDevice = 2,
    NoSmsDestination = 3,
    OrganizationAccessRevoked = 4,
    DeviceRevoked = 5,
    ProviderUnavailable = 6,
    ProviderRejected = 7,
    ProviderTimeout = 8
}
