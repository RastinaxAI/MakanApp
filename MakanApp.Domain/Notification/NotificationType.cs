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
    SchoolApplicationUpdated = 12,
    ConversationInvitation = 13,
    ConversationJoinRequestUpdated = 14,
    PersonalClassRequestUpdated = 15,
    PersonalClassInvitation = 16,
    PersonalClassJoinRequestUpdated = 17,
    PersonalClassManagerTransferUpdated = 18,
    PersonalClassMembershipChanged = 19,
    PersonalClassStateChanged = 20
}
