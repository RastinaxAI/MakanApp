using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PaymentAttemptEventConfiguration : IEntityTypeConfiguration<PaymentAttemptEvent>
{
    public void Configure(EntityTypeBuilder<PaymentAttemptEvent> builder)
    {
        builder.ToTable("PaymentAttemptEvents", "commerce", t => t.HasCheckConstraint("CK_PaymentAttemptEvents_Type", "[Type] BETWEEN 1 AND 8"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ErrorCode).HasMaxLength(100);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.OccurredAtUtc).HasColumnType("datetime2(7)");
        builder.HasOne<PaymentAttempt>().WithMany().HasForeignKey(e => e.PaymentAttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.PaymentAttemptId, e.OccurredAtUtc, e.Id });
    }
}
