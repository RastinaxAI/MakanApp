using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MakanApp.Domain.Notification;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Persistence.Configurations.Notification;

public sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        builder.ToTable("NotificationDeliveryAttempts", "notification", table =>
        {
            table.HasCheckConstraint("CK_NotificationDeliveryAttempts_Channel", "[Channel] IN (1, 2)");
            table.HasCheckConstraint("CK_NotificationDeliveryAttempts_Status", "[Status] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_NotificationDeliveryAttempts_AttemptCount", "[AttemptCount] >= 0");
            table.HasCheckConstraint(
                "CK_NotificationDeliveryAttempts_FailureReason",
                "[FailureReason] IS NULL OR [FailureReason] BETWEEN 1 AND 13");
            table.HasCheckConstraint(
                "CK_NotificationDeliveryAttempts_Lifecycle",
                "([Status] = 2 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL) OR " +
                "([Status] <> 2 AND [ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL)");
            table.HasCheckConstraint(
                "CK_NotificationDeliveryAttempts_Delivered",
                "([Status] = 4 AND [DeliveredAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [DeliveredAtUtc] IS NULL)");
        });
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.DestinationReference)
            .HasMaxLength(NotificationDeliveryAttempt.MaximumDestinationReferenceLength)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.LastAttemptAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.NextAttemptAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.DeliveredAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.ClaimedUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.ProviderMessageId)
            .HasMaxLength(NotificationDeliveryAttempt.MaximumProviderMessageIdLength);
        builder.Property(attempt => attempt.RowVersion).IsRowVersion();
        builder.HasIndex(attempt => new
            {
                attempt.NotificationId,
                attempt.Channel,
                attempt.DestinationReference
            })
            .IsUnique()
            .HasDatabaseName("UX_NotificationDeliveryAttempts_StableDestination");
        builder.HasIndex(attempt => new
            {
                attempt.Status,
                attempt.NextAttemptAtUtc,
                attempt.ClaimedUntilUtc,
                attempt.CreatedAtUtc
            })
            .HasFilter("[Status] IN (1, 2, 3)")
            .HasDatabaseName("IX_NotificationDeliveryAttempts_Pending");
        builder.HasOne<DomainNotification>()
            .WithMany()
            .HasForeignKey(attempt => attempt.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
