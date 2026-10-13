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

    public static readonly IReadOnlyCollection<string> Step15A =
    [
        Access,
        RolesRead,
        RolesManage,
        AuditRead,
        SecurityRead
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
}
