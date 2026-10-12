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
            table.HasCheckConstraint("CK_UsageLedger_QuotaKey", "[QuotaKey] BETWEEN 1 AND 4");
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
            .HasForeignKey(entry => new { entry.OrganizationId, entry.SubscriptionId })
            .HasPrincipalKey(subscription => new { subscription.OrganizationId, subscription.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
