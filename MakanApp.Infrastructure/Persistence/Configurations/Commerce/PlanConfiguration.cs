using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans", "commerce", t => t.HasCheckConstraint("CK_Plans_Audience", "[Audience] IN (1,2)"));
        builder.Property(p => p.AvailabilityEffectiveAtUtc).HasColumnType("datetime2(7)");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Code).HasMaxLength(Plan.MaximumCodeLength).IsRequired();
        builder.Property(plan => plan.DisplayName).HasMaxLength(Plan.MaximumDisplayNameLength).IsRequired();
        builder.Property(plan => plan.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(plan => plan.RowVersion).IsRowVersion();
        builder.HasIndex(plan => plan.Code)
            .IsUnique()
            .HasDatabaseName("UX_Plans_Code");
    }
}
