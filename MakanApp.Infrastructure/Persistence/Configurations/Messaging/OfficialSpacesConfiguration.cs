using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class OfficialSpaceProvisionConfiguration : IEntityTypeConfiguration<OfficialSpaceProvision>
{
    public void Configure(EntityTypeBuilder<OfficialSpaceProvision> b)
    {
        b.ToTable("OfficialSpaceProvisions", "messaging", t =>
        {
            t.HasCheckConstraint("CK_OfficialSpaceProvisions_State", "[State] IN (1,2,3)");
            t.HasCheckConstraint("CK_OfficialSpaceProvisions_Purpose", "[Purpose] IN (2,3)");
            t.HasCheckConstraint("CK_OfficialSpaceProvisions_Attempt", "[AttemptCount]>=0");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.OrganizationId, x.Purpose }).IsUnique().HasFilter("[ClassId] IS NULL");
        b.HasIndex(x => new { x.OrganizationId, x.ClassId, x.Purpose }).IsUnique().HasFilter("[ClassId] IS NOT NULL");
        b.HasOne<MakanApp.Domain.Organization.Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Class>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ClassId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LastFailureCategory).HasMaxLength(64);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class AcademicActivityPublicationConfiguration : IEntityTypeConfiguration<AcademicActivityPublication>
{
    public void Configure(EntityTypeBuilder<AcademicActivityPublication> b)
    {
        b.ToTable("AcademicActivityPublications", "messaging", t =>
        {
            t.HasCheckConstraint("CK_AcademicActivityPublications_Source", "([Kind]=1 AND [LessonId] IS NOT NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NULL) OR ([Kind]=2 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NOT NULL AND [ExamVersionId] IS NULL) OR ([Kind]=3 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NOT NULL)");
            t.HasCheckConstraint("CK_AcademicActivityPublications_Revision", "[SourceRevision]>0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.LessonId, x.SourceRevision }).IsUnique().HasFilter("[LessonId] IS NOT NULL");
        b.HasIndex(x => x.AssignmentVersionId).IsUnique().HasFilter("[AssignmentVersionId] IS NOT NULL");
        b.HasIndex(x => x.ExamVersionId).IsUnique().HasFilter("[ExamVersionId] IS NOT NULL");
        b.HasOne<Class>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ClassId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClassLesson>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LessonId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssignmentVersion>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.AssignmentVersionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExamVersion>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ExamVersionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.InitiatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AcademicActivityDeliveryConfiguration : IEntityTypeConfiguration<AcademicActivityDelivery>
{
    public void Configure(EntityTypeBuilder<AcademicActivityDelivery> b)
    {
        b.ToTable("AcademicActivityDeliveries", "messaging", t =>
        {
            t.HasCheckConstraint("CK_AcademicActivityDeliveries_Purpose", "[Purpose] IN (2,3)");
            t.HasCheckConstraint("CK_AcademicActivityDeliveries_State", "([State] IN (1,3) AND [MessageId] IS NULL AND [ConversationId] IS NULL) OR ([State]=2 AND [MessageId] IS NOT NULL AND [ConversationId] IS NOT NULL)");
            t.HasCheckConstraint("CK_AcademicActivityDeliveries_Attempt", "[AttemptCount]>=0");
        });
        b.HasKey(x => new { x.PublicationId, x.Purpose });
        b.HasIndex(x => new { x.PublicationId, x.ConversationId }).IsUnique().HasFilter("[ConversationId] IS NOT NULL");
        b.HasIndex(x => x.MessageId).IsUnique().HasFilter("[MessageId] IS NOT NULL");
        b.HasOne<AcademicActivityPublication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Message>().WithMany().HasForeignKey(x => new { x.MessageId, x.ConversationId }).HasPrincipalKey(x => new { x.Id, x.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LastFailureCategory).HasMaxLength(64); b.Property(x => x.RowVersion).IsRowVersion();
    }
}
