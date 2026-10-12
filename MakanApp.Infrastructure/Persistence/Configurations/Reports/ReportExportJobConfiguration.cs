using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> builder)
    {
        builder.ToTable("ReportExportJobs", "reports", table =>
        {
            table.HasCheckConstraint("CK_ReportExportJobs_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_ReportExportJobs_Audience", "[Audience] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_ReportExportJobs_Format", "[Format] = 1");
            table.HasCheckConstraint("CK_ReportExportJobs_ReportType", "[ReportType] BETWEEN 1 AND 14");
            table.HasCheckConstraint("CK_ReportExportJobs_EnrollmentStatus", "[EnrollmentStatus] IS NULL OR [EnrollmentStatus] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ReportExportJobs_AssessmentKind", "[AssessmentKind] IS NULL OR [AssessmentKind] IN (1, 2)");
            table.HasCheckConstraint("CK_ReportExportJobs_AttemptCount", "[AttemptCount] >= 0");
            table.HasCheckConstraint("CK_ReportExportJobs_ExpectedRowCount", "[ExpectedRowCount] >= 0");
            table.HasCheckConstraint("CK_ReportExportJobs_DateRange", "[FromUtc] IS NULL OR [ToUtc] IS NULL OR [FromUtc] <= [ToUtc]");
            table.HasCheckConstraint(
                "CK_ReportExportJobs_CompletedState",
                "([Status] = 3 AND [CompletedAtUtc] IS NOT NULL AND [OutputFileAssetId] IS NOT NULL AND [FailureCode] IS NULL) OR [Status] <> 3");
            table.HasCheckConstraint(
                "CK_ReportExportJobs_FailedState",
                "([Status] = 4 AND [FailedAtUtc] IS NOT NULL AND [FailureCode] IS NOT NULL AND [OutputFileAssetId] IS NULL) OR [Status] <> 4");
            table.HasCheckConstraint(
                "CK_ReportExportJobs_ProcessingLease",
                "([Status] = 2 AND [LeaseExpiresAtUtc] IS NOT NULL) OR [Status] <> 2");
        });

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Sort).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(job => job.FailureCode).HasMaxLength(64).IsUnicode(false);
        builder.Property(job => job.DataAsOfUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.FromUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.ToUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.RequestedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.StartedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.FailedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.LeaseExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.NextAttemptAtUtc).HasColumnType("datetime2(7)");
        builder.Property(job => job.RowVersion).IsRowVersion();

        builder.HasIndex(job => new { job.Status, job.NextAttemptAtUtc, job.RequestedAtUtc })
            .HasDatabaseName("IX_ReportExportJobs_Status_NextAttempt_Requested");
        builder.HasIndex(job => new { job.RequestedByUserId, job.OrganizationId, job.RequestedAtUtc })
            .HasDatabaseName("IX_ReportExportJobs_Owner_Organization_Requested");
        builder.HasIndex(job => job.OutputFileAssetId)
            .IsUnique()
            .HasFilter("[OutputFileAssetId] IS NOT NULL")
            .HasDatabaseName("UX_ReportExportJobs_OutputFileAsset");

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(job => job.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(job => job.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(job => job.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileAsset>()
            .WithMany()
            .HasForeignKey(job => job.OutputFileAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
