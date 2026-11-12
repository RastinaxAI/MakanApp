using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class SchoolApplicationConfiguration : IEntityTypeConfiguration<SchoolApplication>
{
    public void Configure(EntityTypeBuilder<SchoolApplication> b)
    {
        b.ToTable("SchoolApplications", "organization", t => {
            t.HasCheckConstraint("CK_SchoolApplications_Kind", "[Kind] IN (1,2)");
            t.HasCheckConstraint("CK_SchoolApplications_Status", "[Status] IN (1,2,3,4,5,6)");
            t.HasCheckConstraint("CK_SchoolApplications_Version", "[Revision] > 0 AND [Version] >= [Revision]");
            t.HasCheckConstraint("CK_SchoolApplications_SubmissionPolicy", "[Status]=1 OR [SubmittedPolicyVersion] IS NOT NULL");
        });
        b.HasKey(a => a.Id); b.Property(a => a.RowVersion).IsRowVersion();
        b.Property(a => a.SchoolName).HasMaxLength(200); b.Property(a => a.LegalEntityName).HasMaxLength(200);
        b.Property(a => a.TeachingHistory).HasMaxLength(4000); b.Property(a => a.WorkSamples).HasMaxLength(4000);
        b.Property(a => a.DegreeField).HasMaxLength(500); b.Property(a => a.SubmittedPolicyVersion).HasMaxLength(100);
        b.HasOne<User>().WithMany().HasForeignKey(a => a.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.ApplicantUserId, a.UpdatedAtUtc }); b.HasIndex(a => new { a.Status, a.UpdatedAtUtc });
    }
}
public sealed class SchoolApplicationRevisionConfiguration : IEntityTypeConfiguration<SchoolApplicationRevision>
{
    public void Configure(EntityTypeBuilder<SchoolApplicationRevision> b)
    {
        b.ToTable("SchoolApplicationRevisions", "organization"); b.HasKey(r => new { r.ApplicationId, r.Number });
        b.Property(r => r.ProtectedSnapshot).HasMaxLength(131072);
        b.HasOne<SchoolApplication>().WithMany().HasForeignKey(r => r.ApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SchoolApplicationDocumentConfiguration : IEntityTypeConfiguration<SchoolApplicationDocument>
{
    public void Configure(EntityTypeBuilder<SchoolApplicationDocument> b)
    {
        b.ToTable("SchoolApplicationDocuments", "organization"); b.HasKey(d => new { d.ApplicationId, d.Revision, d.FileAssetId });
        b.Property(d => d.Kind).HasMaxLength(64);
        b.HasOne<SchoolApplicationRevision>().WithMany().HasForeignKey(d => new { d.ApplicationId, d.Revision }).HasPrincipalKey(r => new { r.ApplicationId, r.Number }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileAsset>().WithMany().HasForeignKey(d => d.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => d.FileAssetId);
    }
}
public sealed class SchoolApplicationDecisionConfiguration : IEntityTypeConfiguration<SchoolApplicationDecision>
{
    public void Configure(EntityTypeBuilder<SchoolApplicationDecision> b)
    {
        b.ToTable("SchoolApplicationDecisions", "organization"); b.HasKey(d => d.Id); b.Property(d => d.ApplicantFeedback).HasMaxLength(2000);
        b.HasOne<SchoolApplicationRevision>().WithMany().HasForeignKey(d => new { d.ApplicationId, d.Revision }).HasPrincipalKey(r => new { r.ApplicationId, r.Number }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(d => d.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.ApplicationId, d.OccurredAtUtc });
    }
}
public sealed class SchoolApplicationOperationConfiguration : IEntityTypeConfiguration<SchoolApplicationOperation>
{
    public void Configure(EntityTypeBuilder<SchoolApplicationOperation> b)
    {
        b.ToTable("SchoolApplicationOperations", "organization"); b.HasKey(o => new { o.ActorUserId, o.ClientOperationId });
        b.Property(o => o.Fingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasOne<User>().WithMany().HasForeignKey(o => o.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SchoolApplication>().WithMany().HasForeignKey(o => o.ApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
