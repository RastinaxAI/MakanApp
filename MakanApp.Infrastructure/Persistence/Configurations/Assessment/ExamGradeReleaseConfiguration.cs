using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamGradeReleaseConfiguration : IEntityTypeConfiguration<ExamGradeRelease>
{
    public void Configure(EntityTypeBuilder<ExamGradeRelease> builder)
    {
        builder.ToTable("ExamGradeReleases", "assessment", table =>
            table.HasCheckConstraint(
                "CK_ExamGradeReleases_RequestHash",
                $"LEN([RequestHash]) = {ExamGradeRelease.RequestHashLength}"));
        builder.HasKey(release => release.Id);
        builder.Property(release => release.RequestHash)
            .HasMaxLength(ExamGradeRelease.RequestHashLength)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(release => release.ReleasedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(release => new
        {
            release.ContextKind, release.ContextId,
            release.ExamGradeRevisionId
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamGradeReleases_Organization_Revision");
        builder.HasIndex(release => new
        {
            release.ContextKind, release.ContextId,
            release.ExamAttemptId,
            release.ClientOperationId
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamGradeReleases_Attempt_ClientOperation");
        builder.HasIndex(release => new
        {
            release.ContextKind, release.ContextId,
            release.ExamAttemptId,
            release.ReleasedAtUtc
        }).HasDatabaseName("IX_ExamGradeReleases_Attempt_ReleasedAtUtc");

        builder.HasOne<ExamGradeRevision>()
            .WithMany()
            .HasForeignKey(release => new
            {
                release.ContextKind, release.ContextId,
                release.ExamAttemptId,
                Id = release.ExamGradeRevisionId
            })
            .HasPrincipalKey(revision => new
            {
                revision.ContextKind, revision.ContextId,
                revision.ExamAttemptId,
                revision.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(release => new
            {
                release.OrganizationId,
                Id = release.ReleasedByMembershipId
            })
            .HasPrincipalKey(membership => new
            {
                membership.OrganizationId,
                membership.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureScope();
    }
}
