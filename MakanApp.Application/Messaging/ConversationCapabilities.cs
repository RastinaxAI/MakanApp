using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record ConversationEffectiveCapabilities(bool CanEditInfo, bool CanManageMembers,
    bool CanManageMessages, bool CanPublish, bool CanManageAdmins, bool CanChangeSettings,
    bool CanInviteMembers, bool CanManageInvites, bool CanBanMembers, bool CanManageTopics)
{
    public static ConversationEffectiveCapabilities None { get; } = new(false, false, false, false, false,
        false, false, false, false, false);
}

public sealed record ConversationCapabilityFacts(ConversationType Type, ConversationManagementPolicy ManagementPolicy,
    ConversationStatus Status, bool ActiveParticipant, ConversationParticipantRole Role,
    ConversationAdminPermission AdminPermissions, GroupMemberCapability MemberCapabilities,
    bool OfficialManager, bool OfficialPublisher, bool ActorRestricted, bool Banned,
    GroupMemberCapability DeniedCapabilities);

public static class ConversationCapabilityPolicy
{
    public static ConversationEffectiveCapabilities Evaluate(ConversationCapabilityFacts facts)
    {
        if (!facts.ActiveParticipant || facts.Status != ConversationStatus.Active || facts.ActorRestricted || facts.Banned ||
            facts.Type is not ConversationType.Group and not ConversationType.Channel)
            return ConversationEffectiveCapabilities.None;
        var canSend = !facts.DeniedCapabilities.HasFlag(GroupMemberCapability.SendMessages);
        if (facts.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic)
            // Roster، مالکیت، History و Visibility رسمی از مسیر مدرسه/سیستم اداره می‌شوند.
            return new(facts.OfficialManager, false, facts.OfficialManager, canSend && facts.OfficialPublisher,
                false, false, false, false, false, facts.Type == ConversationType.Group && facts.OfficialManager);
        if (facts.ManagementPolicy != ConversationManagementPolicy.UserManaged)
            return ConversationEffectiveCapabilities.None;
        var owner = facts.Role == ConversationParticipantRole.Owner;
        bool Has(ConversationAdminPermission permission) => owner ||
            facts.Role == ConversationParticipantRole.Admin && facts.AdminPermissions.HasFlag(permission);
        var invite = !facts.DeniedCapabilities.HasFlag(GroupMemberCapability.InviteMembers) &&
            (Has(ConversationAdminPermission.ManageMembers) || facts.Type == ConversationType.Group &&
                facts.MemberCapabilities.HasFlag(GroupMemberCapability.InviteMembers));
        var publish = canSend && (facts.Type == ConversationType.Channel
            ? Has(ConversationAdminPermission.PublishMessages)
            : owner || facts.MemberCapabilities.HasFlag(GroupMemberCapability.SendMessages));
        return new(Has(ConversationAdminPermission.ChangeInfo), Has(ConversationAdminPermission.RemoveMembers),
            Has(ConversationAdminPermission.DeleteMessages), publish, Has(ConversationAdminPermission.ManageAdmins),
            Has(ConversationAdminPermission.ManageHistory) && Has(ConversationAdminPermission.ManageVisibility) && Has(ConversationAdminPermission.ManageMembers),
            invite, Has(ConversationAdminPermission.ManageInvites), Has(ConversationAdminPermission.BanMembers),
            facts.Type == ConversationType.Group && Has(ConversationAdminPermission.ManageTopics));
    }
}
