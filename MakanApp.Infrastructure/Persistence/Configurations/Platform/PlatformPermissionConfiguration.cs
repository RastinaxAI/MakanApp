using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformPermissionConfiguration : IEntityTypeConfiguration<PlatformPermission>
{
    public void Configure(EntityTypeBuilder<PlatformPermission> builder)
    {
        builder.ToTable("PlatformPermissions", "platform");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Key).HasMaxLength(150).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(300).IsRequired();
        builder.HasIndex(permission => permission.Key)
            .IsUnique()
            .HasDatabaseName("UX_PlatformPermissions_Key");

        builder.HasData(
            Permission(PlatformCatalogIds.AccessPermission, PlatformPermissionKeys.Access, "Access platform backoffice"),
            Permission(PlatformCatalogIds.RolesReadPermission, PlatformPermissionKeys.RolesRead, "Read platform role catalog and assignments"),
            Permission(PlatformCatalogIds.RolesManagePermission, PlatformPermissionKeys.RolesManage, "Manage platform role assignments"),
            Permission(PlatformCatalogIds.AuditReadPermission, PlatformPermissionKeys.AuditRead, "Read platform administration audit"),
            Permission(PlatformCatalogIds.SecurityReadPermission, PlatformPermissionKeys.SecurityRead, "Read platform security metadata"),
            Permission(PlatformCatalogIds.OrganizationsReadPermission, PlatformPermissionKeys.OrganizationsRead, "Read global organization operations"),
            Permission(PlatformCatalogIds.OrganizationsManagePermission, PlatformPermissionKeys.OrganizationsManage, "Manage global organization operations"),
            Permission(PlatformCatalogIds.UsersReadPermission, PlatformPermissionKeys.UsersRead, "Read safe global user operations"),
            Permission(PlatformCatalogIds.UsersManagePermission, PlatformPermissionKeys.UsersManage, "Manage global user account state"),
            Permission(PlatformCatalogIds.SessionsManagePermission, PlatformPermissionKeys.SessionsManage, "Revoke user sessions"),
            Permission(PlatformCatalogIds.ProvisioningReadPermission, PlatformPermissionKeys.ProvisioningRead, "Read organization provisioning state"),
            Permission(PlatformCatalogIds.ProvisioningManagePermission, PlatformPermissionKeys.ProvisioningManage, "Manage organization provisioning"),
            Permission(PlatformCatalogIds.EducationReadPermission, PlatformPermissionKeys.EducationRead, "Read global education operations"),
            Permission(PlatformCatalogIds.EducationManagePermission, PlatformPermissionKeys.EducationManage, "Manage approved education operations"),
            Permission(PlatformCatalogIds.MessagingReadMetadataPermission, PlatformPermissionKeys.MessagingReadMetadata, "Read safe messaging metadata"),
            Permission(PlatformCatalogIds.ModerationReviewPermission, PlatformPermissionKeys.ModerationReview, "Review platform safety cases"),
            Permission(PlatformCatalogIds.ModerationEnforcePermission, PlatformPermissionKeys.ModerationEnforce, "Enforce case-scoped platform moderation decisions"),
            Permission(PlatformCatalogIds.CommerceReadPermission, PlatformPermissionKeys.CommerceRead, "Read plan and subscription operations"),
            Permission(PlatformCatalogIds.CommerceManagePermission, PlatformPermissionKeys.CommerceManage, "Manage plan and subscription operations"),
            Permission(PlatformCatalogIds.FinanceReadPermission, PlatformPermissionKeys.FinanceRead, "Read safe order and payment operations"),
            Permission(PlatformCatalogIds.FinanceManagePermission, PlatformPermissionKeys.FinanceManage, "Manage provider-backed finance operations"),
            Permission(PlatformCatalogIds.OperationsReadPermission, PlatformPermissionKeys.OperationsRead, "Read system and background work operations"),
            Permission(PlatformCatalogIds.OperationsManagePermission, PlatformPermissionKeys.OperationsManage, "Manage approved system operations"),
            Permission(PlatformCatalogIds.NotificationsReadPermission, PlatformPermissionKeys.NotificationsRead, "Read safe notification delivery operations"),
            Permission(PlatformCatalogIds.AiReadPermission, PlatformPermissionKeys.AiRead, "Read backend-owned AI operational state when integrated"));
    }

    private static object Permission(Guid id, string key, string description) => new
    {
        Id = id,
        Key = key,
        Description = description
    };
}
