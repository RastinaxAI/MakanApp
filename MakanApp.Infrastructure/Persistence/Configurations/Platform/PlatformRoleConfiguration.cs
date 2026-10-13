using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformRoleConfiguration : IEntityTypeConfiguration<PlatformRole>
{
    public void Configure(EntityTypeBuilder<PlatformRole> builder)
    {
        builder.ToTable("PlatformRoles", "platform");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Key).HasMaxLength(100).IsRequired();
        builder.Property(role => role.DisplayName).HasMaxLength(150).IsRequired();
        builder.HasIndex(role => role.Key).IsUnique().HasDatabaseName("UX_PlatformRoles_Key");

        builder.HasData(
            Role(PlatformCatalogIds.PlatformOwnerRole, PlatformRoleKeys.PlatformOwner, "Platform Owner"),
            Role(PlatformCatalogIds.OperationsRole, PlatformRoleKeys.Operations, "Operations"),
            Role(PlatformCatalogIds.SupportRole, PlatformRoleKeys.Support, "Support"),
            Role(PlatformCatalogIds.FinanceRole, PlatformRoleKeys.Finance, "Finance"),
            Role(PlatformCatalogIds.ContentRole, PlatformRoleKeys.Content, "Content"),
            Role(PlatformCatalogIds.SecurityAuditRole, PlatformRoleKeys.SecurityAudit, "Security Audit"),
            Role(PlatformCatalogIds.AbuseReviewerRole, PlatformRoleKeys.AbuseReviewer, "Abuse Reviewer"));
    }

    private static object Role(Guid id, string key, string displayName) => new
    {
        Id = id,
        Key = key,
        DisplayName = displayName
    };
}
