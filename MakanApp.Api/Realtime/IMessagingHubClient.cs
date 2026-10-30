using MakanApp.Application.Messaging;

namespace MakanApp.Api.Realtime;

public interface IMessagingHubClient
{
    Task ClientInvalidated(MakanApp.Application.Realtime.RealtimeInvalidation notification);
    Task ConversationChanged(MessagingRealtimeNotification notification);
    Task TypingChanged(TypingNotification notification);
    Task PresenceChanged(PresenceResult notification);
}
