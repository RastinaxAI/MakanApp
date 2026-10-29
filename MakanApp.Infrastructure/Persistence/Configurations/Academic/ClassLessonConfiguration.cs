using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class ClassLessonConfiguration : IEntityTypeConfiguration<ClassLesson>
{
    public void Configure(EntityTypeBuilder<ClassLesson> b)
    {
        b.ToTable("ClassLessons", "academic", t => {
            t.HasCheckConstraint("CK_ClassLessons_Status", "[Status] IN (1,2)");
            t.HasCheckConstraint("CK_ClassLessons_Content", "LEN([Title]) BETWEEN 1 AND 200 AND LEN([Body]) <= 50000 AND [DisplayOrder] BETWEEN 0 AND 100000 AND [RevisionNumber] > 0");
            t.HasCheckConstraint("CK_ClassLessons_Publication", "([Status]=1 AND [PublishedAtUtc] IS NULL) OR ([Status]=2 AND [PublishedAtUtc] IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).IsRequired(); b.Property(x => x.CreationRequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(7)"); b.Property(x => x.PublishedAtUtc).HasColumnType("datetime2(7)");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedByUserId, x.ClientOperationId }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.ClassId, x.Status, x.DisplayOrder });
        b.HasOne<Class>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ClassId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => new { x.OrganizationId, x.LessonId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ClassLessonAttachmentConfiguration : IEntityTypeConfiguration<ClassLessonAttachment>
{
    public void Configure(EntityTypeBuilder<ClassLessonAttachment> b)
    {
        b.ToTable("ClassLessonAttachments", "academic", t => t.HasCheckConstraint("CK_ClassLessonAttachments_Order", "[DisplayOrder] BETWEEN 0 AND 9"));
        b.HasKey(x => new { x.LessonId, x.FileAssetId });
        b.HasOne<FileAsset>().WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.FileAssetId);
    }
}
