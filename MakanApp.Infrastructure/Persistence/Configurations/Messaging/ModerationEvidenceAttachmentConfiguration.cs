using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ModerationEvidenceAttachmentConfiguration :
    IEntityTypeConfiguration<ModerationEvidenceAttachment>
{
    public void Configure(EntityTypeBuilder<ModerationEvidenceAttachment> builder)
    {
        builder.ToTable("ModerationEvidenceAttachments", "messaging");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(item => new { item.ModerationCaseId, item.FileAssetId })
            .IsUnique()
            .HasDatabaseName("UX_ModerationEvidenceAttachments_Case_File");
        builder.HasIndex(item => item.FileAssetId)
            .HasDatabaseName("IX_ModerationEvidenceAttachments_FileAssetId");
        builder.HasOne<ModerationCase>()
            .WithMany()
            .HasForeignKey(item => item.ModerationCaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileAsset>()
            .WithMany()
            .HasForeignKey(item => item.FileAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
