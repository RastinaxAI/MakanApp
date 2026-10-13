using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Persistence.Configurations.Notification;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<DomainNotification>
{
    public void Configure(EntityTypeBuilder<DomainNotification> builder)
    {
        builder.ToTable("Notifications", "notification", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Category", "[Category] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Notifications_Type", "[Type] IN (1, 2, 3, 4, 5, 6, 7)");
            table.HasCheckConstraint("CK_Notifications_ResourceType", "[ResourceType] IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint("CK_Notifications_ReadAt", "[ReadAtUtc] IS NULL OR [ReadAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Title).HasMaxLength(DomainNotification.MaximumTitleLength);
        builder.Property(notification => notification.Body).HasMaxLength(DomainNotification.MaximumBodyLength);
        builder.Property(notification => notification.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(notification => notification.ReadAtUtc).HasColumnType("datetime2(7)");
        builder.Property(notification => notification.RowVersion).IsRowVersion();
        builder.HasIndex(notification => new { notification.SourceEventId, notification.RecipientUserId })
            .IsUnique()
            .HasDatabaseName("UX_Notifications_SourceEvent_Recipient");
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notifications_Recipient_Created");
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc })
            .HasDatabaseName("IX_Notifications_Recipient_ReadAt");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(notification => notification.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
