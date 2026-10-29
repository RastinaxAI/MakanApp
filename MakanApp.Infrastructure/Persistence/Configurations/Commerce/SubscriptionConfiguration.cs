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
            table.HasCheckConstraint("CK_Subscriptions_Status", "[Status] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Subscriptions_Owner", "([OwnerType]=1 AND [OwnerUserId]=[OwnerId] AND [OwnerUserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([OwnerType]=2 AND [OrganizationId]=[OwnerId] AND [OrganizationId] IS NOT NULL AND [OwnerUserId] IS NULL)");
            table.HasCheckConstraint("CK_Subscriptions_ManagedPeriod", "[CommercialControlEnabled]=0 OR ([BillingAnchorUtc] IS NOT NULL AND [EndsAtUtc] IS NOT NULL AND [BillingPeriodIndex]>=0)");
            table.HasCheckConstraint("CK_Subscriptions_Dates", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
            table.HasCheckConstraint(
                "CK_Subscriptions_Cancellation",
                "([Status] = 4 AND [CancelledAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [CancelledAtUtc] IS NULL)");
        });
        builder.HasKey(subscription => subscription.Id);
        builder.HasAlternateKey(subscription => new { subscription.OwnerType, subscription.OwnerId, subscription.Id })
            .HasName("UQ_Subscriptions_Owner_Id");
        builder.Property(subscription => subscription.StartsAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.EndsAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.ActivatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.CancelledAtUtc).HasColumnType("datetime2(7)");
        builder.Property(subscription => subscription.RowVersion).IsRowVersion();
        builder.HasIndex(subscription => subscription.OrganizationId)
            .IsUnique()
            .HasFilter("[Status] = 2 AND [OrganizationId] IS NOT NULL")
            .HasDatabaseName("UX_Subscriptions_Organization_Active");
        builder.HasIndex(subscription => new { subscription.OrganizationId, subscription.CreatedAtUtc })
            .HasDatabaseName("IX_Subscriptions_Organization_CreatedAt");
        builder.HasIndex(s => new { s.OwnerType, s.OwnerId, s.StartsAtUtc }).HasDatabaseName("IX_Subscriptions_Owner_Period");
        builder.HasIndex(s => s.OwnerUserId).IsUnique().HasFilter("[Status]=2 AND [OwnerUserId] IS NOT NULL");
        builder.HasOne<MakanApp.Domain.Identity.User>().WithMany().HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>().WithMany().HasForeignKey(s => s.InitialPlanVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>().WithMany().HasForeignKey(s => s.ScheduledPlanVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(s => s.BillingAnchorUtc).HasColumnType("datetime2(7)");
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
