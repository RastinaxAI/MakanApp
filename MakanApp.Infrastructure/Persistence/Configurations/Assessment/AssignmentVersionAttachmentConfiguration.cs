using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class AssignmentVersionAttachmentConfiguration : IEntityTypeConfiguration<AssignmentVersionAttachment>
{
    public void Configure(EntityTypeBuilder<AssignmentVersionAttachment> b)
    {
        b.ToTable("AssignmentVersionAttachments", "assessment");
        b.HasKey(a => a.Id);
        b.HasIndex(a => new { a.AssignmentVersionId, a.FileAssetId }).IsUnique();
        b.HasOne<AssignmentVersion>().WithMany(v => v.Attachments)
            .HasForeignKey(a => new { a.ContextKind, a.ContextId, a.AssignmentId, a.AssignmentVersionId })
            .HasPrincipalKey(v => new { v.ContextKind, v.ContextId, v.AssignmentId, v.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<FileAsset>().WithMany().HasForeignKey(a => a.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        b.Property(a => a.AttachedAtUtc).HasColumnType("datetime2(7)");
        b.ConfigureScope();
    }
}
