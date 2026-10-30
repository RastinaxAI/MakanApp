using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Knowledge;

public sealed class EducationalSourceConfiguration : IEntityTypeConfiguration<EducationalSource>
{
    public void Configure(EntityTypeBuilder<EducationalSource> b)
    {
        b.ToTable("EducationalSources", "knowledge", t =>
        {
            t.HasCheckConstraint("CK_EducationalSources_Owner", "([Scope]=1 AND [OwnerUserId] IS NOT NULL AND [OrganizationId] IS NULL AND [ClassId] IS NULL) OR ([Scope]=2 AND [OwnerUserId] IS NULL AND [OrganizationId] IS NOT NULL AND [ClassId] IS NOT NULL) OR ([Scope]=3 AND [OwnerUserId] IS NULL AND [OrganizationId] IS NOT NULL AND [ClassId] IS NULL) OR ([Scope]=4 AND [OwnerUserId] IS NULL AND [OrganizationId] IS NULL AND [ClassId] IS NULL)");
            t.HasCheckConstraint("CK_EducationalSources_State", "[Status] IN (1,2,3) AND [Visibility] IN (1,2) AND ([Scope]=4 OR [Visibility]=1) AND [CurrentVersion]>0 AND ([PublishedVersion] IS NULL OR ([PublishedVersion]>0 AND [PublishedVersion]<=[CurrentVersion])) AND (([Status]=3 AND [ArchivedAtUtc] IS NOT NULL) OR ([Status]<>3 AND [ArchivedAtUtc] IS NULL))");
        });
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.CreatedByUserId, s.ClientOperationId }).IsUnique();
        b.HasIndex(s => new { s.OwnerUserId, s.CreatedAtUtc });
        b.HasIndex(s => new { s.OrganizationId, s.ClassId, s.CreatedAtUtc });
        b.Property(s => s.CreationRequestHash).HasMaxLength(64).IsRequired();
        b.Property(s => s.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Organization.Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Academic.Class>().WithMany().HasForeignKey(s => new { s.OrganizationId, s.ClassId })
            .HasPrincipalKey(c => new { c.OrganizationId, c.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class EducationalSourceVersionConfiguration : IEntityTypeConfiguration<EducationalSourceVersion>
{
    public void Configure(EntityTypeBuilder<EducationalSourceVersion> b)
    {
        b.ToTable("EducationalSourceVersions", "knowledge", t =>
        {
            t.HasCheckConstraint("CK_EducationalSourceVersions_State", "[Number]>0 AND [SourceType] IN (1,2) AND [SafetyStatus] IN (1,2,3,4) AND [ProcessingStatus] IN (1,2,3,4,5) AND (([PublicationStatus]=1 AND [PublishedAtUtc] IS NULL) OR ([PublicationStatus]=2 AND [PublishedAtUtc] IS NOT NULL)) AND (([ProcessingStatus]=5 AND [ProcessingFailureCode]=N'SOURCE_PROCESSING_FAILED') OR ([ProcessingStatus]<>5 AND [ProcessingFailureCode] IS NULL))");
        });
        b.HasKey(v => new { v.SourceId, v.Number });
        b.Property(v => v.Title).HasMaxLength(200).IsRequired();
        b.Property(v => v.NormalizedTitle).HasMaxLength(200).IsRequired();
        b.Property(v => v.Description).HasMaxLength(2000);
        b.Property(v => v.ProcessingFailureCode).HasMaxLength(64);
        b.Property(v => v.RowVersion).IsRowVersion();
        b.HasIndex(v => v.FileAssetId);
        b.HasOne<EducationalSource>().WithMany().HasForeignKey(v => v.SourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileAsset>().WithMany().HasForeignKey(v => v.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
