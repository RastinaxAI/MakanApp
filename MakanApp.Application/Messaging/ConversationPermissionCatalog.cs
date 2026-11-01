using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record ConversationPermissionDescriptor(string Key, string Category, long Flag, string Description,
    IReadOnlyList<ConversationType> ConversationTypes, bool OwnerCanGrant, bool AdminCanDelegate,
    bool AppliesToUserManaged, bool AppliesToSystemManagedAcademic, bool OfficialRelationshipDerivedOnly,
    string DelegationRequirement);

public static class ConversationPermissionCatalog
{
    public static IReadOnlyList<ConversationPermissionDescriptor> Get() =>
    [
        Admin(ConversationAdminPermission.ManageMembers, "افزودن عضو با رعایت رضایت و Privacy"),
        Admin(ConversationAdminPermission.RemoveMembers, "حذف عضو از گفتگوی کاربرساخته؛ بدون تغییر Enrollment"),
        Admin(ConversationAdminPermission.ManageAdmins, "مدیریت Admin با رعایت سقف تفویض و حفاظت Owner"),
        Admin(ConversationAdminPermission.DeleteMessages, "مدیریت حذف پیام مجاز", official: true),
        Admin(ConversationAdminPermission.PinMessages, "سنجاق پیام مجاز", official: true),
        Admin(ConversationAdminPermission.PublishMessages, "انتشار پیام در کانال", official: true),
        Admin(ConversationAdminPermission.ManageHistory, "تغییر History گفتگوی کاربرساخته"),
        Admin(ConversationAdminPermission.ManageVisibility, "تغییر Visibility گفتگوی کاربرساخته"),
        Admin(ConversationAdminPermission.ReviewReports, "بررسی گزارش مجاز طبق Policy موجود"),
        Admin(ConversationAdminPermission.ManageInvites, "مدیریت لینک و درخواست عضویت شخصی"),
        Admin(ConversationAdminPermission.BanMembers, "مدیریت محدودیت عضو در گفتگوی کاربرساخته"),
        Admin(ConversationAdminPermission.ChangeInfo, "ویرایش اطلاعات مجاز؛ عنوان رسمی از منبع آموزشی می‌آید", official: true),
        Admin(ConversationAdminPermission.ManageTopics, "مدیریت موضوع مجاز گفتگو", official: true),
        Member(GroupMemberCapability.SendMessages, "ارسال پیام در گروه"),
        Member(GroupMemberCapability.SendMedia, "ارسال رسانه با ACL و Safety معتبر"),
        Member(GroupMemberCapability.InviteMembers, "دعوت عضو با رضایت و Privacy معتبر")
    ];

    private static ConversationPermissionDescriptor Admin(ConversationAdminPermission permission, string description, bool official = false) =>
        new(permission.ToString(), "AdminPermission", (long)permission, description,
            permission == ConversationAdminPermission.ManageTopics ? [ConversationType.Group] :
            permission == ConversationAdminPermission.PublishMessages ? [ConversationType.Channel] : [ConversationType.Group, ConversationType.Channel],
            true, true, true, official, official, "Owner یا Admin دارای ManageAdmins و همان permission؛ در Official فقط رابطهٔ آموزشی جاری");

    private static ConversationPermissionDescriptor Member(GroupMemberCapability capability, string description) =>
        new(capability.ToString(), "MemberCapability", (long)capability, description, [ConversationType.Group],
            true, true, true, capability != GroupMemberCapability.InviteMembers, false,
            "تغییر تنظیمات گروه با ManageMembers؛ محدودیت عضو و Policy رسمی مقدم است");
}
