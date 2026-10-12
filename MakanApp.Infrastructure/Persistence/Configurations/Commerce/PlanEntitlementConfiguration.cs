using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        builder.ToTable("PlanEntitlements", "commerce", table =>
            table.HasCheckConstraint("CK_PlanEntitlements_Key", "[Key] BETWEEN 1 AND 6"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PlanVersionId, item.Key })
            .IsUnique()
            .HasDatabaseName("UX_PlanEntitlements_Version_Key");
        builder.HasOne<PlanVersion>()
            .WithMany(version => version.Entitlements)
            .HasForeignKey(item => item.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
