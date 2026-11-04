using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class OrganizationMembershipOperationConfiguration : IEntityTypeConfiguration<OrganizationMembershipOperation>
{
    public void Configure(EntityTypeBuilder<OrganizationMembershipOperation> builder)
    {
        builder.ToTable("MembershipManagementOperations", "organization", table =>
        {
            table.HasCheckConstraint("CK_MembershipManagementOperations_Kind", "[Kind] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_MembershipManagementOperations_Hash", "LEN([RequestHash]) = 64");
            table.HasCheckConstraint("CK_MembershipManagementOperations_Result", "([Kind] = 4 AND [ResultRoleAssignmentId] IS NULL) OR ([Kind] IN (1, 2, 3) AND [ResultRoleAssignmentId] IS NOT NULL)");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(o => o.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(o => new { o.OrganizationId, o.ActorUserId, o.ClientOperationId }).IsUnique()
            .HasDatabaseName("UX_MembershipManagementOperations_Intent");
        builder.HasOne<Membership>().WithMany().HasForeignKey(o => new { o.OrganizationId, o.MembershipId })
            .HasPrincipalKey(m => new { m.OrganizationId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoleAssignment>().WithMany().HasForeignKey(o => o.ResultRoleAssignmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
