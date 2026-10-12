using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions", "commerce", table =>
        {
            table.HasCheckConstraint("CK_Subscriptions_Status", "[Status] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_Subscriptions_Dates", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
            table.HasCheckConstraint(
                "CK_Subscriptions_Cancellation",
                "([Status] = 4 AND [CancelledAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [CancelledAtUtc] IS NULL)");
        });
        builder.HasKey(subscription => subscription.Id);
        builder.HasAlternateKey(subscription => new { subscription.OrganizationId, subscription.Id })
            .HasName("UQ_Subscriptions_Organization_Id");
        builder.Property(subscription => subscription.StartsAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.EndsAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.ActivatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.CancelledAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.RowVersion).IsRowVersion();
        builder.HasIndex(subscription => subscription.OrganizationId)
            .IsUnique()
            .HasFilter("[Status] = 2")
            .HasDatabaseName("UX_Subscriptions_Organization_Active");
        builder.HasIndex(subscription => new { subscription.OrganizationId, subscription.CreatedAtUtc })
            .HasDatabaseName("IX_Subscriptions_Organization_CreatedAt");
        builder.HasIndex(subscription => subscription.SourceOrderId)
            .IsUnique()
            .HasFilter("[SourceOrderId] IS NOT NULL")
            .HasDatabaseName("UX_Subscriptions_SourceOrder");
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(subscription => subscription.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>()
            .WithMany()
            .HasForeignKey(subscription => subscription.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(subscription => subscription.SourceOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
