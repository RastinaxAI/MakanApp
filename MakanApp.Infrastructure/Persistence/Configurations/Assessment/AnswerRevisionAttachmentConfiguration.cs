using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class AnswerRevisionAttachmentConfiguration : IEntityTypeConfiguration<AnswerRevisionAttachment>
{
    public void Configure(EntityTypeBuilder<AnswerRevisionAttachment> builder)
    {
        builder.ToTable("AnswerRevisionAttachments", "assessment");
        builder.HasKey(item => new { item.AnswerRevisionId, item.FileAssetId });
        builder.Property(item => item.AttachedAtUtc).HasColumnType("datetime2(7)");
        builder.HasOne<AnswerRevision>().WithMany(revision => revision.Attachments)
            .HasForeignKey(item => new { item.OrganizationId, item.ExamAttemptId, item.ExamAttemptQuestionId, item.AnswerRevisionId })
            .HasPrincipalKey(revision => new { revision.OrganizationId, revision.ExamAttemptId, revision.ExamAttemptQuestionId, revision.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileAsset>().WithMany().HasForeignKey(item => item.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.OrganizationId, item.ExamAttemptId, item.FileAssetId });
    }
}
