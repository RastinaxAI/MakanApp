using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

internal static class AcademicScopeConfiguration
{
    public static void ConfigureScope<TEntity>(this EntityTypeBuilder<TEntity> builder) where TEntity : class, IAcademicScoped
    {
        builder.Ignore(x => x.Scope);
        var name = builder.Metadata.GetTableName()!;
        var schema = builder.Metadata.GetSchema()!;
        builder.Property(x => x.OrganizationId).IsRequired(false);
        builder.Property(x => x.PersonalClassId).IsRequired(false);
        builder.HasOne<PersonalClass>().WithMany().HasForeignKey(x => x.PersonalClassId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(name, schema, table => table.HasCheckConstraint($"CK_{name}_AcademicScope",
            "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR " +
            "([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])"));
        if (typeof(TEntity).GetProperty("ClassRef") is not null)
        {
            builder.Ignore("ClassRef");
            builder.ToTable(name, schema, table => table.HasCheckConstraint($"CK_{name}_PersonalClass",
                "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]"));
        }
        if (typeof(TEntity).GetProperty("Participation") is not null)
        {
            builder.Ignore("Participation");
            builder.Property<Guid?>("EnrollmentId").IsRequired(false);
            builder.HasOne<PersonalClassParticipant>().WithMany()
                .HasForeignKey("PersonalClassId", "PersonalClassMembershipId")
                .HasPrincipalKey(x => new { x.PersonalClassId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            builder.ToTable(name, schema, table => table.HasCheckConstraint($"CK_{name}_Participation",
                "([ContextKind] = 1 AND [EnrollmentId] IS NOT NULL AND [ParticipantId] = [EnrollmentId] AND [PersonalClassMembershipId] IS NULL) OR " +
                "([ContextKind] = 2 AND [EnrollmentId] IS NULL AND [PersonalClassMembershipId] IS NOT NULL AND [ParticipantId] = [PersonalClassMembershipId])"));
        }
        foreach (var prefix in new[] { "CreatedBy", "ReleasedBy", "ReviewedBy" })
        {
            var organizationActor = prefix + "MembershipId";
            var personalActor = prefix + "PersonalMembershipId";
            if (typeof(TEntity).GetProperty(personalActor) is null) continue;
            builder.Property<Guid?>(organizationActor).IsRequired(false);
            builder.HasOne<PersonalClassParticipant>().WithMany()
                .HasForeignKey("PersonalClassId", personalActor)
                .HasPrincipalKey(x => new { x.PersonalClassId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            var optional = prefix == "ReviewedBy" ? $" OR ([{organizationActor}] IS NULL AND [{personalActor}] IS NULL)" : "";
            builder.ToTable(name, schema, table => table.HasCheckConstraint($"CK_{name}_{prefix}Actor",
                $"([ContextKind] = 1 AND [{organizationActor}] IS NOT NULL AND [{personalActor}] IS NULL) OR " +
                $"([ContextKind] = 2 AND [{organizationActor}] IS NULL AND [{personalActor}] IS NOT NULL)" + optional));
        }
    }
}
