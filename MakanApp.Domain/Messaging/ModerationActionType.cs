namespace MakanApp.Domain.Messaging;

public enum ModerationActionType
{
    Created = 1,
    ReviewStarted = 2,
    Resolved = 3,
    Dismissed = 4,
    Escalated = 5,
    ContentRemoved = 6,
    Warned = 7,
    MessagingRestricted = 8
}
