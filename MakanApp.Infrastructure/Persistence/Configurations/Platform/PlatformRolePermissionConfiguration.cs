using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformRolePermissionConfiguration : IEntityTypeConfiguration<PlatformRolePermission>
{
    public void Configure(EntityTypeBuilder<PlatformRolePermission> builder)
    {
        builder.ToTable("PlatformRolePermissions", "platform");
        builder.HasKey(item => new { item.RoleId, item.PermissionId });
        builder.HasOne<PlatformRole>()
            .WithMany()
            .HasForeignKey(item => item.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlatformPermission>()
            .WithMany()
            .HasForeignKey(item => item.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            Owner(PlatformCatalogIds.AccessPermission),
            Owner(PlatformCatalogIds.RolesReadPermission),
            Owner(PlatformCatalogIds.RolesManagePermission),
            Owner(PlatformCatalogIds.AuditReadPermission),
            Owner(PlatformCatalogIds.SecurityReadPermission),
            Owner(PlatformCatalogIds.OrganizationsReadPermission),
            Owner(PlatformCatalogIds.OrganizationsManagePermission),
            Owner(PlatformCatalogIds.UsersReadPermission),
            Owner(PlatformCatalogIds.UsersManagePermission),
            Owner(PlatformCatalogIds.SessionsManagePermission),
            Owner(PlatformCatalogIds.ProvisioningReadPermission),
            Owner(PlatformCatalogIds.ProvisioningManagePermission),
            Owner(PlatformCatalogIds.EducationReadPermission),
            Owner(PlatformCatalogIds.EducationManagePermission),
            Owner(PlatformCatalogIds.MessagingReadMetadataPermission),
            Owner(PlatformCatalogIds.ModerationReviewPermission),
            Owner(PlatformCatalogIds.ModerationEnforcePermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.OrganizationsReadPermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.OrganizationsManagePermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.UsersReadPermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.UsersManagePermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.SessionsManagePermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.ProvisioningReadPermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.ProvisioningManagePermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.EducationReadPermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.EducationManagePermission),
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.MessagingReadMetadataPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.OrganizationsReadPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.UsersReadPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.SessionsManagePermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.ProvisioningReadPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.EducationReadPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.MessagingReadMetadataPermission),
            Mapping(PlatformCatalogIds.FinanceRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.FinanceRole, PlatformCatalogIds.OrganizationsReadPermission),
            Mapping(PlatformCatalogIds.ContentRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.AuditReadPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.SecurityReadPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.MessagingReadMetadataPermission),
            Mapping(PlatformCatalogIds.AbuseReviewerRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.AbuseReviewerRole, PlatformCatalogIds.MessagingReadMetadataPermission),
            Mapping(PlatformCatalogIds.AbuseReviewerRole, PlatformCatalogIds.ModerationReviewPermission));
    }

    private static object Owner(Guid permissionId) =>
        Mapping(PlatformCatalogIds.PlatformOwnerRole, permissionId);

    private static object Mapping(Guid roleId, Guid permissionId) => new
    {
        RoleId = roleId,
        PermissionId = permissionId
    };
}
