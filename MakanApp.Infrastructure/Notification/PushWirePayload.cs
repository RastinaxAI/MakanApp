using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed record PushWirePayload(string NotificationId, string? ConversationId, string Type,
    string Title, string Body, string DeepLink, string Tag)
{
    public static PushWirePayload Create(PushNotificationDelivery delivery)
    {
        var isMessage = delivery.Type is NotificationType.MessageReceived or NotificationType.MessageReply or NotificationType.MessageMention;
        var conversationId = isMessage && delivery.ConversationId != Guid.Empty ? delivery.ConversationId : null;
        return new(delivery.NotificationId.ToString("D"), conversationId?.ToString("D"),
            delivery.Type.HasValue && Enum.IsDefined(delivery.Type.Value) ? delivery.Type.Value.ToString() : "Notification",
            isMessage ? "پیام جدید" : "اعلان جدید",
            isMessage ? "پیام جدیدی در ماکان دارید." : "اعلان جدیدی در ماکان دارید.",
            conversationId.HasValue ? $"makan://conversations/{conversationId:D}" : $"makan://notifications/{delivery.NotificationId:D}",
            delivery.NotificationId.ToString("N"));
    }
}
