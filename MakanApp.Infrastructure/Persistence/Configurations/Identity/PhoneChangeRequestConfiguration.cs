using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class PhoneChangeRequestConfiguration : IEntityTypeConfiguration<PhoneChangeRequest>
{
    public void Configure(EntityTypeBuilder<PhoneChangeRequest> b)
    {
        b.ToTable("PhoneChangeRequests", "identity", t => {
            t.HasCheckConstraint("CK_PhoneChangeRequests_State", "[State] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_PhoneChangeRequests_Attempts", "[FailedAttempts] >= 0 AND [MaxFailedAttempts] > 0 AND [FailedAttempts] <= [MaxFailedAttempts]");
            t.HasCheckConstraint("CK_PhoneChangeRequests_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            t.HasCheckConstraint("CK_PhoneChangeRequests_Hash", "[State] IN (1,4) OR (DATALENGTH([CodeHash]) = 32 AND DATALENGTH([Salt]) >= 16)");
            t.HasCheckConstraint("CK_PhoneChangeRequests_Completed", "([State] = 3 AND [CompletedAtUtc] IS NOT NULL) OR ([State] <> 3 AND [CompletedAtUtc] IS NULL)");
        });
        b.HasKey(r => r.Id); b.Property(r => r.TargetPhone).HasMaxLength(32).IsRequired();
        b.Property(r => r.CodeHash).HasMaxLength(32); b.Property(r => r.Salt).HasMaxLength(64);
        b.Property(r => r.CredentialVersion).HasColumnType("varbinary(8)"); b.Property(r => r.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserSession>().WithMany().HasForeignKey(r => new { r.SessionId, r.UserId }).HasPrincipalKey(s => new { s.Id, s.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserCredential>().WithMany().HasForeignKey(r => r.CredentialId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.UserId, r.ClientOperationId }).IsUnique();
        b.HasIndex(r => new { r.UserId, r.CreatedAtUtc }); b.HasIndex(r => new { r.TargetPhone, r.CreatedAtUtc });
        b.HasIndex(r => r.UserId).IsUnique().HasFilter("[State] IN (1, 2)").HasDatabaseName("UX_PhoneChangeRequests_PendingUser");
    }
}
