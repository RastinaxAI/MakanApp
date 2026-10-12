namespace MakanApp.Domain.Messaging;

public enum ModerationActionType
{
    Created = 1,
    ReviewStarted = 2,
    Resolved = 3,
    Dismissed = 4,
    Escalated = 5
}
