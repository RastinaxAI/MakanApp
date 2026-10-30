using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Knowledge;

public sealed class AiSourceSelectionConfiguration : IEntityTypeConfiguration<AiSourceSelectionOperation>
{
    public void Configure(EntityTypeBuilder<AiSourceSelectionOperation> b)
    {
        b.ToTable("AiSourceSelectionOperations", "knowledge", t => t.HasCheckConstraint("CK_AiSourceSelectionOperations_Hash", "DATALENGTH([PayloadHash])=32"));
        b.HasKey(x => new { x.UserId, x.OperationId });
        b.Property(x => x.PayloadHash).HasColumnType("binary(32)").IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Pins).WithOne().HasForeignKey(x => new { x.UserId, x.OperationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AiSourceSelectionPinConfiguration : IEntityTypeConfiguration<AiSourceSelectionPin>
{
    public void Configure(EntityTypeBuilder<AiSourceSelectionPin> b)
    {
        b.ToTable("AiSourceSelectionPins", "knowledge", t => t.HasCheckConstraint("CK_AiSourceSelectionPins_Number", "[Number]>0"));
        b.HasKey(x => new { x.UserId, x.OperationId, x.SourceId });
        b.HasOne<EducationalSourceVersion>().WithMany().HasForeignKey(x => new { x.SourceId, x.Number }).OnDelete(DeleteBehavior.Restrict);
    }
}
