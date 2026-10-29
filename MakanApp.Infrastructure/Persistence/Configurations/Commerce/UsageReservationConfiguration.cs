using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class UsageReservationConfiguration : IEntityTypeConfiguration<UsageReservation>
{
    public void Configure(EntityTypeBuilder<UsageReservation> b)
    {
        b.ToTable("UsageReservations", "commerce", t =>
        {
            t.HasCheckConstraint("CK_UsageReservations_Values", "[Amount]>0 AND [Status] BETWEEN 1 AND 4 AND [OwnerType] IN (1,2) AND [ContextType] BETWEEN 1 AND 3 AND [QuotaKey] BETWEEN 1 AND 7 AND [PeriodEndsAtUtc]>[PeriodStartsAtUtc]");
            t.HasCheckConstraint("CK_UsageReservations_Trial", "([IsTrial]=1 AND [SubscriptionId] IS NULL AND [PlanVersionId] IS NULL AND [OwnerType]=1 AND [OwnerId]=[ConsumerUserId] AND [QuotaKey]=5 AND [Amount]=1) OR ([IsTrial]=0 AND [SubscriptionId] IS NOT NULL AND [PlanVersionId] IS NOT NULL)");
            t.HasCheckConstraint("CK_UsageReservations_Finalization", "([Status] IN (1,4) AND [FinalizedAtUtc] IS NULL) OR ([Status] IN (2,3) AND [FinalizedAtUtc] IS NOT NULL)");
        });
        b.HasKey(r => r.Id);
        b.HasAlternateKey(r => new { r.OwnerType, r.OwnerId, r.ConsumerUserId, r.Id });
        b.Property(r => r.PayloadHash).HasColumnType("binary(32)");
        b.Property(r => r.FinalizationReason).HasMaxLength(100);
        b.Property(r => r.RowVersion).IsRowVersion();
        b.HasIndex(r => new { r.ConsumerUserId, r.OperationId }).IsUnique();
        b.HasIndex(r => new { r.OwnerType, r.OwnerId, r.SubscriptionId, r.QuotaKey, r.PeriodStartsAtUtc, r.Status });
        b.HasOne<User>().WithMany().HasForeignKey(r => r.ConsumerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Subscription>().WithMany().HasForeignKey(r => new { r.OwnerType, r.OwnerId, r.SubscriptionId })
            .HasPrincipalKey(s => new { s.OwnerType, s.OwnerId, s.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlanVersion>().WithMany().HasForeignKey(r => r.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SubscriptionAmendmentConfiguration : IEntityTypeConfiguration<SubscriptionAmendment>
{
    public void Configure(EntityTypeBuilder<SubscriptionAmendment> b)
    {
        b.ToTable("SubscriptionAmendments", "commerce", t => t.HasCheckConstraint("CK_SubscriptionAmendments_Proration", "[DurationTicks]>0 AND [RemainingTicks]>=0 AND [RemainingTicks]<=[DurationTicks] AND [ChargedToman]>=0"));
        b.HasKey(a => a.Id); b.Property(a => a.ChargedToman).HasPrecision(18, 0);
        b.HasIndex(a => a.OrderId).IsUnique();
        b.HasOne<Subscription>().WithMany().HasForeignKey(a => a.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlanVersion>().WithMany().HasForeignKey(a => a.PreviousPlanVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlanVersion>().WithMany().HasForeignKey(a => a.NextPlanVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Order>().WithMany().HasForeignKey(a => a.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
