using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Notification;

public sealed class PushDeviceRegistrationConfiguration : IEntityTypeConfiguration<PushDeviceRegistration>
{
    public void Configure(EntityTypeBuilder<PushDeviceRegistration> builder)
    {
        builder.ToTable("PushDeviceRegistrations", "notification", table =>
            table.HasCheckConstraint("CK_PushDeviceRegistrations_Platform", "[Platform] IN (1, 2, 3)"));
        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.PushToken)
            .HasMaxLength(PushDeviceRegistration.MaximumTokenLength)
            .IsRequired();
        builder.Property(registration => registration.TokenHash)
            .HasMaxLength(PushDeviceRegistration.TokenHashLength)
            .IsRequired();
        builder.Property(registration => registration.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(registration => registration.LastSeenAtUtc).HasColumnType("datetime2(7)");
        builder.Property(registration => registration.RevokedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(registration => registration.RowVersion).IsRowVersion();
        builder.HasIndex(registration => registration.TokenHash)
            .IsUnique()
            .HasFilter("[RevokedAtUtc] IS NULL")
            .HasDatabaseName("UX_PushDeviceRegistrations_ActiveToken");
        builder.HasIndex(registration => new { registration.UserId, registration.RevokedAtUtc })
            .HasDatabaseName("IX_PushDeviceRegistrations_User_Active");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(registration => registration.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(registration => new { registration.UserSessionId, registration.UserId })
            .HasPrincipalKey(session => new { session.Id, session.UserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
