namespace MakanApp.Domain.Platform;

public static class PlatformRoleKeys
{
    public const string PlatformOwner = "PlatformOwner";
    public const string Operations = "Operations";
    public const string Support = "Support";
    public const string Finance = "Finance";
    public const string Content = "Content";
    public const string SecurityAudit = "SecurityAudit";
    public const string AbuseReviewer = "AbuseReviewer";

    public static readonly IReadOnlyCollection<string> All =
    [
        PlatformOwner,
        Operations,
        Support,
        Finance,
        Content,
        SecurityAudit,
        AbuseReviewer
    ];
}

public static class PlatformPermissionKeys
{
    public const string Access = "platform.access";
    public const string RolesRead = "platform.roles.read";
    public const string RolesManage = "platform.roles.manage";
    public const string AuditRead = "platform.audit.read";
    public const string SecurityRead = "platform.security.read";
    public const string OrganizationsRead = "platform.organizations.read";
    public const string OrganizationsManage = "platform.organizations.manage";
    public const string UsersRead = "platform.users.read";
    public const string UsersManage = "platform.users.manage";
    public const string SessionsManage = "platform.sessions.manage";
    public const string ProvisioningRead = "platform.provisioning.read";
    public const string ProvisioningManage = "platform.provisioning.manage";
    public const string EducationRead = "platform.education.read";
    public const string EducationManage = "platform.education.manage";
    public const string MessagingReadMetadata = "platform.messaging.read_metadata";
    public const string ModerationReview = "platform.moderation.review";
    public const string ModerationEnforce = "platform.moderation.enforce";

    public static readonly IReadOnlyCollection<string> Step15A =
    [
        Access,
        RolesRead,
        RolesManage,
        AuditRead,
        SecurityRead
    ];

    public static readonly IReadOnlyCollection<string> All =
    [
        .. Step15A,
        OrganizationsRead,
        OrganizationsManage,
        UsersRead,
        UsersManage,
        SessionsManage,
        ProvisioningRead,
        ProvisioningManage,
        EducationRead,
        EducationManage,
        MessagingReadMetadata,
        ModerationReview,
        ModerationEnforce
    ];
}

public static class PlatformCatalogIds
{
    public static readonly Guid PlatformOwnerRole = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid OperationsRole = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SupportRole = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid FinanceRole = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid ContentRole = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid SecurityAuditRole = Guid.Parse("10000000-0000-0000-0000-000000000006");
    public static readonly Guid AbuseReviewerRole = Guid.Parse("10000000-0000-0000-0000-000000000007");

    public static readonly Guid AccessPermission = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid RolesReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid RolesManagePermission = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid AuditReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid SecurityReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000005");
    public static readonly Guid OrganizationsReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid OrganizationsManagePermission = Guid.Parse("20000000-0000-0000-0000-000000000007");
    public static readonly Guid UsersReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000008");
    public static readonly Guid UsersManagePermission = Guid.Parse("20000000-0000-0000-0000-000000000009");
    public static readonly Guid SessionsManagePermission = Guid.Parse("20000000-0000-0000-0000-000000000010");
    public static readonly Guid ProvisioningReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000011");
    public static readonly Guid ProvisioningManagePermission = Guid.Parse("20000000-0000-0000-0000-000000000012");
    public static readonly Guid EducationReadPermission = Guid.Parse("20000000-0000-0000-0000-000000000013");
    public static readonly Guid EducationManagePermission = Guid.Parse("20000000-0000-0000-0000-000000000014");
    public static readonly Guid MessagingReadMetadataPermission = Guid.Parse("20000000-0000-0000-0000-000000000015");
    public static readonly Guid ModerationReviewPermission = Guid.Parse("20000000-0000-0000-0000-000000000016");
    public static readonly Guid ModerationEnforcePermission = Guid.Parse("20000000-0000-0000-0000-000000000017");
}
