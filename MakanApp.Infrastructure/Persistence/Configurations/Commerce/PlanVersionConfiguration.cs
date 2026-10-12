using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.ToTable("PlanVersions", "commerce", table =>
        {
            table.HasCheckConstraint("CK_PlanVersions_VersionNumber", "[VersionNumber] > 0");
            table.HasCheckConstraint("CK_PlanVersions_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_PlanVersions_Publication",
                "([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] = 2 AND [PublishedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(version => version.Id);
        builder.Property(version => version.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.PublishedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.RowVersion).IsRowVersion();
        builder.HasIndex(version => new { version.PlanId, version.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_PlanVersions_Plan_VersionNumber");
        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(version => version.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(version => version.Entitlements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(version => version.QuotaDefinitions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
