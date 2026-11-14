using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Knowledge;

public sealed class AccountDeletionKnowledgePurgeConfiguration : IEntityTypeConfiguration<AccountDeletionKnowledgePurge>
{
    public void Configure(EntityTypeBuilder<AccountDeletionKnowledgePurge> b)
    {
        b.ToTable("AccountDeletionKnowledgePurges", "knowledge", t =>
        {
            t.HasCheckConstraint("CK_AccountDeletionKnowledgePurges_Attempts", "[AttemptCount]>=0 AND [Version]>0");
            t.HasCheckConstraint("CK_AccountDeletionKnowledgePurges_Outcome", "([CompletedAtUtc] IS NULL AND [Outcome] IS NULL) OR ([CompletedAtUtc] IS NOT NULL AND [Outcome] IS NOT NULL AND [Outcome] IN (N'Deleted',N'NotIngested'))");
        });
        b.HasKey(p => new { p.RequestId, p.SourceId, p.Version });
        b.Property(p => p.CommandRequestId).HasMaxLength(32); b.Property(p => p.Outcome).HasMaxLength(32);
        b.Property(p => p.RowVersion).IsRowVersion(); b.HasIndex(p => p.CommandRequestId).IsUnique();
        b.HasOne<AccountDeletionRequest>().WithMany().HasForeignKey(p => new { p.RequestId, p.OwnerUserId })
            .HasPrincipalKey(r => new { r.Id, r.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EducationalSourceVersion>().WithMany().HasForeignKey(p => new { p.SourceId, p.Version })
            .HasPrincipalKey(v => new { v.SourceId, v.Number }).OnDelete(DeleteBehavior.Restrict);
    }
}
