namespace MakanApp.Domain.Notification;

public enum NotificationType
{
    AssignmentPublished = 1,
    AssignmentGradeReleased = 2,
    ExamPublished = 3,
    ExamGradeReleased = 4,
    MessageReceived = 5,
    MessageReply = 6,
    MessageMention = 7,
    OrganizationInvitation = 8,
    MembershipChanged = 9,
    OrderPaid = 10,
    SupportUpdated = 11,
    SchoolApplicationUpdated = 12
}
