using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessagingPrivacySettingsConfiguration : IEntityTypeConfiguration<MessagingPrivacySettings>
{
    public void Configure(EntityTypeBuilder<MessagingPrivacySettings> builder)
    {
        builder.ToTable("MessagingPrivacySettings", "messaging", table =>
        {
            table.HasCheckConstraint(
                "CK_MessagingPrivacySettings_DirectMessagesAudience",
                "[DirectMessagesAudience] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_MessagingPrivacySettings_PhoneNumberVisibilityAudience",
                "[PhoneNumberVisibilityAudience] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_MessagingPrivacySettings_PhoneNumberDiscoveryAudience",
                "[PhoneNumberDiscoveryAudience] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_MessagingPrivacySettings_GroupInvitesAudience",
                "[GroupInvitesAudience] IN (1, 2, 3)");
        });
        builder.HasKey(settings => settings.UserId);
        builder.Property(settings => settings.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(settings => settings.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(settings => settings.RowVersion).IsRowVersion();
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<MessagingPrivacySettings>(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
