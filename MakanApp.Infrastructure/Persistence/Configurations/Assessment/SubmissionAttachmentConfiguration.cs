using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class SubmissionAttachmentConfiguration : IEntityTypeConfiguration<SubmissionAttachment>
{
    public void Configure(EntityTypeBuilder<SubmissionAttachment> builder)
    {
        builder.ToTable("SubmissionAttachments", "assessment");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.AttachedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(attachment => new
        {
            attachment.ContextKind, attachment.ContextId,
            attachment.SubmissionAttemptId,
            attachment.FileAssetId
        })
            .IsUnique()
            .HasDatabaseName("UX_SubmissionAttachments_Attempt_FileAsset");

        builder.HasOne<SubmissionAttempt>()
            .WithMany()
            .HasForeignKey(attachment => new
            {
                attachment.ContextKind, attachment.ContextId,
                attachment.SubmissionAttemptId
            })
            .HasPrincipalKey(attempt => new { attempt.ContextKind, attempt.ContextId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileAsset>()
            .WithMany()
            .HasForeignKey(attachment => attachment.FileAssetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureScope();
    }
}
