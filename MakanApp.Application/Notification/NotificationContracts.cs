using MakanApp.Domain.Notification;
using MakanApp.Application.Outbox;
using System.Text.Json;

namespace MakanApp.Application.Notification;

public sealed record NotificationRecipientIntent(Guid UserId, string Title, string Body);

public sealed record NotificationIntent(
    Guid EventId,
    Guid? OrganizationId,
    NotificationCategory Category,
    NotificationType Type,
    NotificationResourceType ResourceType,
    Guid ResourceId,
    DateTime OccurredAtUtc,
    IReadOnlyCollection<NotificationRecipientIntent> Recipients);

public sealed record NotificationOutboxPayload(
    Guid SourceEventId,
    Guid? OrganizationId,
    NotificationCategory Category,
    NotificationType Type,
    NotificationResourceType ResourceType,
    Guid ResourceId,
    DateTime OccurredAtUtc,
    NotificationRecipientIntent Recipient);

public static class NotificationOutboxContract
{
    public static IReadOnlyCollection<OutboxEnvelope> Create(NotificationIntent intent) =>
        intent.Recipients
            .Where(recipient => recipient.UserId != Guid.Empty)
            .DistinctBy(recipient => recipient.UserId)
            .Select(recipient => new NotificationOutboxPayload(
                intent.EventId,
                intent.OrganizationId,
                intent.Category,
                intent.Type,
                intent.ResourceType,
                intent.ResourceId,
                intent.OccurredAtUtc,
                recipient))
            .Select(payload => new OutboxEnvelope(
                Guid.NewGuid(),
                GetMessageType(payload.Type),
                JsonSerializer.Serialize(payload),
                payload.OccurredAtUtc))
            .ToArray();

    public static bool Matches(string messageType, NotificationType type) =>
        string.Equals(messageType, GetMessageType(type), StringComparison.Ordinal);

    private static string GetMessageType(NotificationType type) => type switch
    {
        NotificationType.AssignmentPublished => "notification.assignment-published.v1",
        NotificationType.AssignmentGradeReleased => "notification.assignment-grade-released.v1",
        NotificationType.ExamPublished => "notification.exam-published.v1",
        NotificationType.ExamGradeReleased => "notification.exam-grade-released.v1",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public sealed record NotificationQuery(
    int Page = 1,
    int PageSize = 20,
    bool UnreadOnly = false,
    NotificationCategory? Category = null,
    Guid? OrganizationId = null);

public sealed record NotificationItem(
    Guid Id,
    Guid? OrganizationId,
    NotificationCategory Category,
    NotificationType Type,
    string Title,
    string Body,
    NotificationResourceType ResourceType,
    Guid ResourceId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public sealed record NotificationPage(
    IReadOnlyCollection<NotificationItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record NotificationUnreadCount(int Count);

public static class NotificationErrorCodes
{
    public const string NotFound = "notification_not_found";
    public const string InvalidQuery = "notification_query_invalid";
    public const string ConcurrencyConflict = "notification_concurrency_conflict";
}

public sealed class NotificationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
