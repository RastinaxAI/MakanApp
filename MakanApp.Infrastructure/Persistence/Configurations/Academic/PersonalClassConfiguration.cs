using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class PersonalClassConfiguration : IEntityTypeConfiguration<PersonalClass>
{
    public void Configure(EntityTypeBuilder<PersonalClass> b)
    {
        b.ToTable("PersonalClasses", "academic"); b.HasKey(c => c.Id);
        b.Property(c => c.Title).HasMaxLength(200).IsRequired(); b.Property(c => c.RowVersion).IsRowVersion();
        b.HasIndex(c => new { c.HostUserId, c.ClientOperationId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(c => c.HostUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PersonalClassInvitationConfiguration : IEntityTypeConfiguration<PersonalClassInvitation>
{
    public void Configure(EntityTypeBuilder<PersonalClassInvitation> b)
    {
        b.ToTable("PersonalClassInvitations", "academic", t => t.HasCheckConstraint("CK_PersonalClassInvitations_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]"));
        b.HasKey(i => i.Id); b.Property(i => i.RowVersion).IsRowVersion();
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
        b.ToTable("PersonalClassParticipants", "academic", t => t.HasCheckConstraint("CK_PersonalClassParticipants_End", "[EndedAtUtc] IS NULL OR [EndedAtUtc]>=[JoinedAtUtc]"));
        b.HasKey(p => p.Id); b.Property(p => p.RowVersion).IsRowVersion();
        b.HasIndex(p => new { p.PersonalClassId, p.StudentUserId }).IsUnique().HasFilter("[EndedAtUtc] IS NULL");
        b.HasIndex(p => p.InvitationId).IsUnique();
        b.HasOne<PersonalClassInvitation>().WithMany().HasForeignKey(p => new { p.PersonalClassId, p.StudentUserId, p.InvitationId })
            .HasPrincipalKey(i => new { i.PersonalClassId, i.DestinationUserId, i.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.StudentUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
