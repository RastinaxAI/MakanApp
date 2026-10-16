namespace MakanApp.Domain.Messaging;

[Flags]
public enum ConversationAdminPermission
{
    None = 0,
    ManageMembers = 1 << 0,
    RemoveMembers = 1 << 1,
    ManageAdmins = 1 << 2,
    DeleteMessages = 1 << 3,
    PinMessages = 1 << 4,
    PublishMessages = 1 << 5,
    ManageHistory = 1 << 6,
    ManageVisibility = 1 << 7,
    ReviewReports = 1 << 8,
    ManageInvites = 1 << 9,
    BanMembers = 1 << 10,
    ChangeInfo = 1 << 11,
    ManageTopics = 1 << 12,
    All = ManageMembers |
          RemoveMembers |
          ManageAdmins |
          DeleteMessages |
          PinMessages |
          PublishMessages |
          ManageHistory |
          ManageVisibility |
          ReviewReports |
          ManageInvites |
          BanMembers |
          ChangeInfo |
          ManageTopics,
    LegacyAdminDefault = ManageMembers |
                         RemoveMembers |
                         PinMessages |
                         PublishMessages
}
