using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Notification;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", "notification", table =>
        {
            table.HasCheckConstraint("CK_NotificationPreferences_Category", "[Category] BETWEEN 1 AND 7");
            table.HasCheckConstraint(
                "CK_NotificationPreferences_QuietHours",
                "([QuietHoursEnabled] = 0 AND [QuietHoursStartLocal] IS NULL AND [QuietHoursEndLocal] IS NULL AND [TimeZoneId] IS NULL) OR " +
                "([QuietHoursEnabled] = 1 AND [QuietHoursStartLocal] IS NOT NULL AND [QuietHoursEndLocal] IS NOT NULL AND [QuietHoursStartLocal] <> [QuietHoursEndLocal] AND [TimeZoneId] IS NOT NULL)");
        });
        builder.HasKey(preference => preference.Id);
        builder.Property(preference => preference.QuietHoursStartLocal).HasColumnType("time(0)");
        builder.Property(preference => preference.QuietHoursEndLocal).HasColumnType("time(0)");
        builder.Property(preference => preference.TimeZoneId)
            .HasMaxLength(NotificationPreference.MaximumTimeZoneIdLength);
        builder.Property(preference => preference.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(preference => preference.RowVersion).IsRowVersion();
        builder.HasIndex(preference => new
            {
                preference.UserId,
                preference.OrganizationId,
                preference.Category
            })
            .IsUnique()
            .HasFilter("[OrganizationId] IS NOT NULL")
            .HasDatabaseName("UX_NotificationPreferences_User_Organization_Category");
        builder.HasIndex(preference => new
            {
                preference.UserId,
                preference.Category
            })
            .IsUnique()
            .HasFilter("[OrganizationId] IS NULL")
            .HasDatabaseName("UX_NotificationPreferences_User_Personal_Category");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(preference => preference.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
