using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformMfaCredentialConfiguration : IEntityTypeConfiguration<PlatformMfaCredential>
{
    public void Configure(EntityTypeBuilder<PlatformMfaCredential> builder)
    {
        builder.ToTable("PlatformMfaCredentials", "platform", table =>
        {
            table.HasCheckConstraint("CK_PlatformMfa_Active", "([ProtectedSecret] IS NULL AND [ActivatedAtUtc] IS NULL) OR ([ProtectedSecret] IS NOT NULL AND [ActivatedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_PlatformMfa_Pending", "([PendingProtectedSecret] IS NULL AND [EnrollmentId] IS NULL AND [EnrollmentSessionId] IS NULL AND [EnrollmentExpiresAtUtc] IS NULL) OR ([PendingProtectedSecret] IS NOT NULL AND [EnrollmentId] IS NOT NULL AND [EnrollmentSessionId] IS NOT NULL AND [EnrollmentExpiresAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_PlatformMfa_Attempts", "[FailedAttempts] >= 0 AND ([LastAcceptedTimeStep] IS NULL OR [LastAcceptedTimeStep] >= 0)");
        });
        builder.HasKey(c => c.UserId);
        builder.Property(c => c.ProtectedSecret).HasMaxLength(2048);
        builder.Property(c => c.PendingProtectedSecret).HasMaxLength(2048);
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.Ignore(c => c.IsActive);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>().WithMany().HasForeignKey(c => new { c.EnrollmentSessionId, c.UserId })
            .HasPrincipalKey(s => new { s.Id, s.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.RecoveryCodes).WithOne().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.RecoveryCodes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class PlatformMfaRecoveryCodeConfiguration : IEntityTypeConfiguration<PlatformMfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<PlatformMfaRecoveryCode> builder)
    {
        builder.ToTable("PlatformMfaRecoveryCodes", "platform");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasIndex(c => new { c.UserId, c.CodeHash }).IsUnique();
    }
}
