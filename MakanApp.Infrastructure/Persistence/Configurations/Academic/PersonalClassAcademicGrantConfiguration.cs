using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class PersonalClassAcademicGrantConfiguration : IEntityTypeConfiguration<PersonalClassAcademicGrant>
{
    public void Configure(EntityTypeBuilder<PersonalClassAcademicGrant> b)
    {
        b.ToTable("PersonalClassAcademicGrants", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClassAcademicGrants_Capabilities", "[Capabilities] BETWEEN 0 AND 7 AND ([Capabilities] <> 0 OR [RevokedAtUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_PersonalClassAcademicGrants_Time", "[RevokedAtUtc] IS NULL OR [RevokedAtUtc] >= [CreatedAtUtc]");
            t.HasCheckConstraint("CK_PersonalClassAcademicGrants_Parent", "[ParentGrantId] IS NULL OR [ParentGrantId] <> [Id]");
        });
        b.HasKey(g => g.Id);
        b.HasAlternateKey(g => new { g.PersonalClassId, g.Id });
        b.HasIndex(g => new { g.PersonalClassId, g.MembershipId }).IsUnique().HasFilter("[RevokedAtUtc] IS NULL");
        b.HasIndex(g => new { g.GrantedByUserId, g.ClientOperationId }).IsUnique();
        b.Property(g => g.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.Property(g => g.CreatedAtUtc).HasColumnType("datetime2(7)");
        b.Property(g => g.RevokedAtUtc).HasColumnType("datetime2(7)");
        b.Property(g => g.RowVersion).IsRowVersion();
        b.HasOne<PersonalClassParticipant>().WithMany().HasForeignKey(g => new { g.PersonalClassId, g.MembershipId })
            .HasPrincipalKey(m => new { m.PersonalClassId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonalClassAcademicGrant>().WithMany().HasForeignKey(g => new { g.PersonalClassId, g.ParentGrantId })
            .HasPrincipalKey(g => new { g.PersonalClassId, g.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(g => g.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
