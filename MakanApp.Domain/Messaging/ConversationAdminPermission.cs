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
    All = ManageMembers |
          RemoveMembers |
          ManageAdmins |
          DeleteMessages |
          PinMessages |
          PublishMessages |
          ManageHistory |
          ManageVisibility,
    LegacyAdminDefault = ManageMembers |
                         RemoveMembers |
                         PinMessages |
                         PublishMessages
}
