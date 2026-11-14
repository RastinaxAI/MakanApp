using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRequest> b)
    {
        b.ToTable("AccountDeletionRequests", "identity", t =>
        {
            t.HasCheckConstraint("CK_AccountDeletionRequests_Policy", "[PolicyStatus] IN (N'PRODUCT_LEGAL_DECISION_REQUIRED',N'EXECUTION_DISABLED',N'EXECUTION_SCHEDULED',N'EXECUTION_COMPLETED')");
            t.HasCheckConstraint("CK_AccountDeletionRequests_Status", "[Status] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_AccountDeletionRequests_Attempts", "[AttemptCount]>=0 AND [ConsecutiveFailures]>=0");
            t.HasCheckConstraint("CK_AccountDeletionRequests_Claim", "([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL) OR ([Status]=3 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_AccountDeletionRequests_Schedule", "([Status]=1 AND [ScheduledExecutionAtUtc] IS NULL) OR ([Status]<>1 AND [ScheduledExecutionAtUtc] IS NOT NULL AND [ScheduledExecutionAtUtc]>=[RequestedAtUtc])");
            t.HasCheckConstraint("CK_AccountDeletionRequests_Completed", "([Status]=5 AND [CompletedAtUtc] IS NOT NULL AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc]>=[StartedAtUtc]) OR ([Status]<>5 AND [CompletedAtUtc] IS NULL)");
        });
        b.HasKey(r => r.Id); b.Property(r => r.PolicyStatus).HasMaxLength(64); b.Property(r => r.PolicyVersion).HasMaxLength(64);
        b.Property(r => r.Status).HasDefaultValue(AccountDeletionStatus.Requested);
        b.Property(r => r.PolicyVersion).HasDefaultValue(AccountDeletionRequest.CurrentPolicyVersion);
        b.Property(r => r.FailureCode).HasMaxLength(100); b.Property(r => r.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserSession>().WithMany().HasForeignKey(r => new { r.RequestedBySessionId, r.UserId })
            .HasPrincipalKey(s => new { s.Id, s.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => r.UserId).IsUnique(); b.HasIndex(r => new { r.UserId, r.ClientOperationId }).IsUnique();
        b.HasIndex(r => new { r.Status, r.ScheduledExecutionAtUtc, r.ClaimedUntilUtc, r.NextAttemptAtUtc });
    }
}
