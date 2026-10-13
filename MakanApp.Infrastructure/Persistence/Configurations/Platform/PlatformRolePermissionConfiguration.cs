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
            Mapping(PlatformCatalogIds.OperationsRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.SupportRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.FinanceRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.ContentRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.AccessPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.AuditReadPermission),
            Mapping(PlatformCatalogIds.SecurityAuditRole, PlatformCatalogIds.SecurityReadPermission),
            Mapping(PlatformCatalogIds.AbuseReviewerRole, PlatformCatalogIds.AccessPermission));
    }

    private static object Owner(Guid permissionId) =>
        Mapping(PlatformCatalogIds.PlatformOwnerRole, permissionId);

    private static object Mapping(Guid roleId, Guid permissionId) => new
    {
        RoleId = roleId,
        PermissionId = permissionId
    };
}
