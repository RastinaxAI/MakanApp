using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class AccountDeletionStepConfiguration : IEntityTypeConfiguration<AccountDeletionStep>
{
    public void Configure(EntityTypeBuilder<AccountDeletionStep> b)
    {
        b.ToTable("AccountDeletionSteps", "identity", t =>
        {
            t.HasCheckConstraint("CK_AccountDeletionSteps_Step", "[Step] BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_AccountDeletionSteps_Status", "[Status] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_AccountDeletionSteps_Attempts", "[AttemptCount]>=0");
            t.HasCheckConstraint("CK_AccountDeletionSteps_Completed", "([Status]=3 AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc]>=[StartedAtUtc]) OR ([Status]<>3 AND [CompletedAtUtc] IS NULL)");
        });
        b.HasKey(s => new { s.RequestId, s.Step });
        b.Property(s => s.FailureCode).HasMaxLength(100); b.Property(s => s.RowVersion).IsRowVersion();
        b.HasOne<AccountDeletionRequest>().WithMany().HasForeignKey(s => s.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
