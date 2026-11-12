using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRequest> b)
    {
        b.ToTable("AccountDeletionRequests", "identity", t => t.HasCheckConstraint("CK_AccountDeletionRequests_Policy", "[PolicyStatus] = N'PRODUCT_LEGAL_DECISION_REQUIRED'"));
        b.HasKey(r => r.Id); b.Property(r => r.PolicyStatus).HasMaxLength(64); b.Property(r => r.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserSession>().WithMany().HasForeignKey(r => new { r.RequestedBySessionId, r.UserId })
            .HasPrincipalKey(s => new { s.Id, s.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => r.UserId).IsUnique(); b.HasIndex(r => new { r.UserId, r.ClientOperationId }).IsUnique();
    }
}
