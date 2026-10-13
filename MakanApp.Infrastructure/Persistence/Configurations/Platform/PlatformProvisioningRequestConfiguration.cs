using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformProvisioningRequestConfiguration
    : IEntityTypeConfiguration<PlatformProvisioningRequest>
{
    public void Configure(EntityTypeBuilder<PlatformProvisioningRequest> builder)
    {
        builder.ToTable("ProvisioningRequests", "platform", table =>
            table.HasCheckConstraint(
                "CK_ProvisioningRequests_Status",
                "[Status] BETWEEN 1 AND 6"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ClientOperationId)
            .HasMaxLength(PlatformProvisioningRequest.MaximumClientOperationIdLength)
            .IsRequired();
        builder.Property(item => item.OrganizationName)
            .HasMaxLength(PlatformProvisioningRequest.MaximumOrganizationNameLength)
            .IsRequired();
        builder.Property(item => item.Reason)
            .HasMaxLength(PlatformProvisioningRequest.MaximumReasonLength)
            .IsRequired();
        builder.Property(item => item.FailureCode)
            .HasMaxLength(PlatformProvisioningRequest.MaximumFailureCodeLength);
        builder.Property(item => item.SubscriptionEndsAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.InvitationExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.ClientOperationId)
            .IsUnique()
            .HasDatabaseName("UX_ProvisioningRequests_ClientOperationId");
        builder.HasIndex(item => new { item.Status, item.UpdatedAtUtc })
            .HasDatabaseName("IX_ProvisioningRequests_Status_UpdatedAtUtc");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.InitialManagerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>()
            .WithMany()
            .HasForeignKey(item => item.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(item => item.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(item => item.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Invitation>()
            .WithMany()
            .HasForeignKey(item => item.ManagerInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
