using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformRoleAssignmentConfiguration : IEntityTypeConfiguration<PlatformRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PlatformRoleAssignment> builder)
    {
        builder.ToTable("PlatformRoleAssignments", "platform", table =>
            table.HasCheckConstraint(
                "CK_PlatformRoleAssignments_Status",
                "[Status] IN (1, 2)"));
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.GrantedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.RowVersion).IsRowVersion();

        builder.HasIndex(assignment => new { assignment.UserId, assignment.RoleId })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_PlatformRoleAssignments_Active_User_Role");
        builder.HasIndex(assignment => new { assignment.RoleId, assignment.Status, assignment.EndedAtUtc })
            .HasDatabaseName("IX_PlatformRoleAssignments_Role_Status");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlatformRole>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.EndedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
