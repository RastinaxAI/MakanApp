namespace MakanApp.Domain.Notification;

public enum NotificationDeliveryStatus
{
    Pending = 1,
    Processing = 2,
    Deferred = 3,
    Delivered = 4,
    Failed = 5,
    Suppressed = 6
}
