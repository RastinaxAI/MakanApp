using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class SchoolEstablishmentOfferingConfiguration : IEntityTypeConfiguration<SchoolEstablishmentOffering>
{
    public void Configure(EntityTypeBuilder<SchoolEstablishmentOffering> b)
    {
        b.ToTable("SchoolEstablishmentOfferings", "commerce", t => t.HasCheckConstraint("CK_SchoolOfferings_KindVersion", "[Kind] IN (1,2) AND [Version]>0"));
        b.HasKey(o => o.Kind); b.Property(o => o.Kind).ValueGeneratedNever(); b.Property(o => o.RowVersion).IsRowVersion();
        b.HasOne<PlanVersion>().WithMany().HasForeignKey(o => o.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SchoolOfferingOperationConfiguration : IEntityTypeConfiguration<SchoolOfferingOperation>
{
    public void Configure(EntityTypeBuilder<SchoolOfferingOperation> b)
    {
        b.ToTable("SchoolOfferingOperations", "commerce"); b.HasKey(o => new { o.ActorUserId, o.ClientOperationId });
        b.Property(o => o.Fingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasOne<User>().WithMany().HasForeignKey(o => o.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlanVersion>().WithMany().HasForeignKey(o => o.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
