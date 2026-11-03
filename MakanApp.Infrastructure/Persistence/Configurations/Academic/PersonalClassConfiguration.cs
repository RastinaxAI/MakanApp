using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class PersonalClassConfiguration : IEntityTypeConfiguration<PersonalClass>
{
    public void Configure(EntityTypeBuilder<PersonalClass> b)
    {
        b.ToTable("PersonalClasses", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClasses_Capacity", "[Capacity] IS NULL OR [Capacity]>0");
            t.HasCheckConstraint("CK_PersonalClasses_State", "[State] BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_PersonalClasses_Migration", "[MigrationState] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_PersonalClasses_Archive", "([State]=6 AND [ArchivedAtUtc] IS NOT NULL) OR ([State]<>6 AND [ArchivedAtUtc] IS NULL)");
            t.HasCheckConstraint("CK_PersonalClasses_Manager", "[State]<>3 OR [ManagerUserId] IS NOT NULL OR [MigrationState]<>0");
        }); b.HasKey(c => c.Id);
        b.Property(c => c.Title).HasMaxLength(200).IsRequired(); b.Property(c => c.RowVersion).IsRowVersion();
        b.HasIndex(c => new { c.HostUserId, c.ClientOperationId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(c => c.HostUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => c.FormationRequestId).IsUnique().HasFilter("[FormationRequestId] IS NOT NULL");
        b.HasOne<PersonalClassRequest>().WithMany().HasForeignKey(c => c.FormationRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PersonalClassInvitationConfiguration : IEntityTypeConfiguration<PersonalClassInvitation>
{
    public void Configure(EntityTypeBuilder<PersonalClassInvitation> b)
    {
        b.ToTable("PersonalClassInvitations", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClassInvitations_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]");
            t.HasCheckConstraint("CK_PersonalClassInvitations_Roles", "[LocalRoles] BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_PersonalClassInvitations_Delegation", "[DelegatedCapabilities] BETWEEN 0 AND 3 AND ([DelegatedCapabilities]=0 OR ([LocalRoles]&2)=2)");
        });
        b.HasKey(i => i.Id); b.Property(i => i.RowVersion).IsRowVersion();
        b.Property(i => i.ClosureReason).HasMaxLength(500);
        b.HasAlternateKey(i => new { i.PersonalClassId, i.DestinationUserId, i.Id });
        b.HasIndex(i => new { i.PersonalClassId, i.ClientOperationId }).IsUnique();
        b.HasOne<PersonalClass>().WithMany().HasForeignKey(i => i.PersonalClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.DestinationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PersonalClassParticipantConfiguration : IEntityTypeConfiguration<PersonalClassParticipant>
{
    public void Configure(EntityTypeBuilder<PersonalClassParticipant> b)
    {
        b.ToTable("PersonalClassParticipants", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClassParticipants_End", "[EndedAtUtc] IS NULL OR [EndedAtUtc]>=[JoinedAtUtc]");
            t.HasCheckConstraint("CK_PersonalClassParticipants_Roles", "[LocalRoles] BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_PersonalClassParticipants_Delegation", "[DelegatedCapabilities] BETWEEN 0 AND 3 AND ([DelegatedCapabilities]=0 OR ([LocalRoles]&2)=2)");
        });
        b.HasKey(p => p.Id); b.Property(p => p.RowVersion).IsRowVersion();
        b.Property(p => p.EndReason).HasMaxLength(500);
        b.HasAlternateKey(p => new { p.PersonalClassId, p.StudentUserId, p.Id });
        b.HasOne<PersonalClass>().WithMany().HasForeignKey(p => p.PersonalClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.PersonalClassId, p.StudentUserId }).IsUnique().HasFilter("[EndedAtUtc] IS NULL");
        b.HasIndex(p => p.PersonalClassId).IsUnique().HasFilter("[EndedAtUtc] IS NULL AND [LocalRoles]>=4").HasDatabaseName("UX_PersonalClassParticipants_PrimaryManager");
        b.HasIndex(p => p.InvitationId).IsUnique().HasFilter("[InvitationId] IS NOT NULL");
        b.HasOne<PersonalClassInvitation>().WithMany().HasForeignKey(p => new { p.PersonalClassId, p.StudentUserId, p.InvitationId })
            .HasPrincipalKey(i => new { i.PersonalClassId, i.DestinationUserId, i.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.StudentUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
