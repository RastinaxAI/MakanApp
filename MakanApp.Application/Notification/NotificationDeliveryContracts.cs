using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public enum DeliveryProviderOutcome
{
    Delivered = 1,
    TransientFailure = 2,
    PermanentFailure = 3,
    Suppressed = 4
}

public sealed record DeliveryProviderResult(
    DeliveryProviderOutcome Outcome,
    NotificationDeliveryFailureReason? FailureReason = null,
    string? ProviderMessageId = null,
    TimeSpan? RetryAfter = null)
{
    public static DeliveryProviderResult Delivered(string? providerMessageId = null) =>
        new(DeliveryProviderOutcome.Delivered, ProviderMessageId: providerMessageId);

    public static DeliveryProviderResult Transient(NotificationDeliveryFailureReason reason, TimeSpan? retryAfter = null) =>
        new(DeliveryProviderOutcome.TransientFailure, reason, RetryAfter: retryAfter);

    public static DeliveryProviderResult Permanent(NotificationDeliveryFailureReason reason) =>
        new(DeliveryProviderOutcome.PermanentFailure, reason);

    public static DeliveryProviderResult Suppressed(NotificationDeliveryFailureReason reason) =>
        new(DeliveryProviderOutcome.Suppressed, reason);
}

public sealed record PushNotificationDelivery(
    Guid DeliveryAttemptId,
    Guid NotificationId,
    string PushToken,
    string Title,
    string Body,
    string DeepLink,
    PushDevicePlatform Platform = PushDevicePlatform.Android,
    string? RegistrationVersion = null)
{
    public override string ToString() => $"PushNotificationDelivery {{ DeliveryAttemptId = {DeliveryAttemptId}, NotificationId = {NotificationId} }}";
}

public sealed record NotificationSmsDelivery(
    Guid DeliveryAttemptId,
    Guid NotificationId,
    string Destination,
    string TemplateKey,
    IReadOnlyDictionary<string, string> SafeParameters);

public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "Notifications:Delivery";

    public int BatchSize { get; init; } = 50;
    public TimeSpan ClaimLease { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan IdlePollingInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan ProviderTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public int MaximumAttempts { get; init; } = 3;
}

public static class NotificationExternalContentPolicy
{
    public static (string Title, string Body, string DeepLink) CreatePush(
        Domain.Notification.Notification notification) => notification.Type switch
    {
        NotificationType.AssignmentPublished or NotificationType.ExamPublished =>
            ("اعلان آموزشی جدید", "مورد آموزشی جدیدی در ماکان منتشر شده است.",
                $"makan://notifications/{notification.Id:D}"),
        NotificationType.AssignmentGradeReleased or NotificationType.ExamGradeReleased =>
            ("نتیجه آموزشی جدید", "نتیجه آموزشی جدید در ماکان منتشر شده است.",
                $"makan://notifications/{notification.Id:D}"),
        NotificationType.MessageReceived or NotificationType.MessageReply or NotificationType.MessageMention =>
            ("پیام جدید", "پیام جدیدی در ماکان دارید.",
                $"makan://notifications/{notification.Id:D}"),
        _ => ("اعلان جدید", "اعلان جدیدی در ماکان دارید.",
            $"makan://notifications/{notification.Id:D}")
    };

    public static (string TemplateKey, IReadOnlyDictionary<string, string> Parameters) CreateSms(
        Domain.Notification.Notification notification)
    {
        var templateKey = notification.Type switch
        {
            NotificationType.AssignmentPublished => "assignment_published",
            NotificationType.AssignmentGradeReleased => "assignment_result_published",
            NotificationType.ExamPublished => "exam_published",
            NotificationType.ExamGradeReleased => "exam_result_published",
            NotificationType.MessageReceived or NotificationType.MessageReply or NotificationType.MessageMention =>
                "message_available",
            _ => "notification_available"
        };
        return (templateKey, new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString("D")
        });
    }
}
