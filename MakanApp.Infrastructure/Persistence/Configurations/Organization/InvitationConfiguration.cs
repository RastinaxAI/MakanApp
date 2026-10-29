using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Guardian;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", "organization", table =>
        {
            table.HasCheckConstraint("CK_Invitations_Role", "[Role] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Invitations_Status", "[Status] IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint(
                "CK_Invitations_Expiry",
                "[ExpiresAtUtc] > [CreatedAtUtc]");
            table.HasCheckConstraint("CK_Invitations_PersonLink",
                "([OrganizationPersonId] IS NULL AND [GuardianLearnerOrganizationPersonId] IS NULL AND [ClientOperationId] IS NULL) OR " +
                "([OrganizationPersonId] IS NOT NULL AND [GuardianLearnerOrganizationPersonId] IS NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 1) OR " +
                $"([OrganizationPersonId] IS NULL AND [GuardianLearnerOrganizationPersonId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [Role] = {(int)OrganizationRole.Parent})");
            table.HasCheckConstraint("CK_Invitations_GuardianReceipt",
                "([GuardianLearnerOrganizationPersonId] IS NULL AND [AcceptedGuardianRelationId] IS NULL) OR " +
                "([GuardianLearnerOrganizationPersonId] IS NOT NULL AND (([Status]=2 AND [AcceptedGuardianRelationId] IS NOT NULL) OR ([Status]<>2 AND [AcceptedGuardianRelationId] IS NULL)))");
        });
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.AcceptedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.DeclinedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.RevokedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.RowVersion).IsRowVersion();
        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.ClientOperationId })
            .IsUnique().HasFilter("[ClientOperationId] IS NOT NULL")
            .HasDatabaseName("UX_Invitations_LinkOperation");
        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.OrganizationPersonId })
            .IsUnique().HasFilter("[OrganizationPersonId] IS NOT NULL AND [Status] = 1")
            .HasDatabaseName("UX_Invitations_Pending_PersonLink");
        builder.HasOne<OrganizationPerson>().WithMany()
            .HasForeignKey(invitation => new { invitation.OrganizationId, invitation.OrganizationPersonId })
            .HasPrincipalKey(person => new { person.OrganizationId, person.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrganizationPerson>().WithMany()
            .HasForeignKey(invitation => new { invitation.OrganizationId, invitation.GuardianLearnerOrganizationPersonId })
            .HasPrincipalKey(person => new { person.OrganizationId, person.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GuardianRelation>().WithMany().HasForeignKey(invitation => invitation.AcceptedGuardianRelationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.DestinationUserId, invitation.GuardianLearnerOrganizationPersonId })
            .IsUnique().HasFilter("[Status] = 1 AND [GuardianLearnerOrganizationPersonId] IS NOT NULL")
            .HasDatabaseName("UX_Invitations_Pending_Guardian_Learner");

        builder.HasIndex(invitation => new
        {
            invitation.DestinationUserId,
            invitation.OrganizationId,
            invitation.Role
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [GuardianLearnerOrganizationPersonId] IS NULL")
            .HasDatabaseName("UX_Invitations_Pending_Destination_Organization_Role");

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(invitation => invitation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.DestinationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedMembershipId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoleAssignment>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedRoleAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
