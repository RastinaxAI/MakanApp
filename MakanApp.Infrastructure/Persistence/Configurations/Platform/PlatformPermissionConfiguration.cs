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
            Permission(PlatformCatalogIds.SecurityReadPermission, PlatformPermissionKeys.SecurityRead, "Read platform security metadata"));
    }

    private static object Permission(Guid id, string key, string description) => new
    {
        Id = id,
        Key = key,
        Description = description
    };
}
