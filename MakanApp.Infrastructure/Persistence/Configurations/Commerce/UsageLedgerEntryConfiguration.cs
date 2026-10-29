using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class UsageLedgerEntryConfiguration : IEntityTypeConfiguration<UsageLedgerEntry>
{
    public void Configure(EntityTypeBuilder<UsageLedgerEntry> builder)
    {
        builder.ToTable("UsageLedger", "commerce", table =>
        {
            table.HasCheckConstraint("CK_UsageLedger_QuotaKey", "[QuotaKey] BETWEEN 1 AND 7");
            table.HasCheckConstraint("CK_UsageLedger_Status", "[Status] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_UsageLedger_Owner", "([OwnerType]=1 AND [OrganizationId] IS NULL) OR ([OwnerType]=2 AND [OrganizationId]=[OwnerId] AND [OrganizationId] IS NOT NULL)");
            table.HasCheckConstraint("CK_UsageLedger_Reservation", "[ReservationId] IS NULL OR ([ConsumerUserId] IS NOT NULL AND [ContextType] IS NOT NULL AND [ContextId] IS NOT NULL AND [PeriodStartsAtUtc] IS NOT NULL AND [PeriodEndsAtUtc] IS NOT NULL AND [PeriodEndsAtUtc]>[PeriodStartsAtUtc])");
            table.HasCheckConstraint("CK_UsageLedger_Amount", "[Amount] > 0");
        });
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.OccurredAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(entry => new
        {
            entry.SubscriptionId,
            entry.QuotaKey,
            entry.SourceOperationId
        })
            .IsUnique()
            .HasFilter("[Status]=2 AND [SubscriptionId] IS NOT NULL")
            .HasDatabaseName("UX_UsageLedger_Subscription_Quota_SourceOperation");
        builder.HasIndex(entry => new
        {
            entry.OrganizationId,
            entry.SubscriptionId,
            entry.QuotaKey,
            entry.OccurredAtUtc
        }).HasDatabaseName("IX_UsageLedger_Organization_Subscription_Quota_OccurredAt");
        builder.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(entry => new { entry.OwnerType, entry.OwnerId, entry.SubscriptionId })
            .HasPrincipalKey(subscription => new { subscription.OwnerType, subscription.OwnerId, subscription.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.ReasonCode).HasMaxLength(100);
        builder.Property(e => e.PeriodStartsAtUtc).HasColumnType("datetime2(7)");
        builder.Property(e => e.PeriodEndsAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(e => new { e.ReservationId, e.Status }).IsUnique().HasFilter("[ReservationId] IS NOT NULL");
        builder.HasOne<UsageReservation>().WithMany().HasForeignKey(e => new { e.OwnerType, e.OwnerId, e.ConsumerUserId, e.ReservationId })
            .HasPrincipalKey(r => new { r.OwnerType, r.OwnerId, r.ConsumerUserId, r.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>().WithMany().HasForeignKey(e => e.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Identity.User>().WithMany().HasForeignKey(e => e.ConsumerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
