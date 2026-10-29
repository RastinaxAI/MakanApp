using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PlanQuotaDefinitionConfiguration : IEntityTypeConfiguration<PlanQuotaDefinition>
{
    public void Configure(EntityTypeBuilder<PlanQuotaDefinition> builder)
    {
        builder.ToTable("PlanQuotaDefinitions", "commerce", table =>
        {
            table.HasCheckConstraint("CK_PlanQuotaDefinitions_Key", "[Key] BETWEEN 1 AND 7");
            table.HasCheckConstraint("CK_PlanQuotaDefinitions_Limit", "[Limit] IS NULL OR [Limit] >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PlanVersionId, item.Key })
            .IsUnique()
            .HasDatabaseName("UX_PlanQuotaDefinitions_Version_Key");
        builder.HasOne<PlanVersion>()
            .WithMany(version => version.QuotaDefinitions)
            .HasForeignKey(item => item.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
