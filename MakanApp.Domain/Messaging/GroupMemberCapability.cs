namespace MakanApp.Domain.Messaging;

[Flags]
public enum GroupMemberCapability
{
    None = 0,
    SendMessages = 1 << 0,
    SendMedia = 1 << 1,
    InviteMembers = 1 << 2,
    LegacyMemberDefault = SendMessages | SendMedia,
    All = SendMessages | SendMedia | InviteMembers
}
