using MakanApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "platform", table =>
        {
            table.HasCheckConstraint("CK_Outbox_AttemptCount", "[AttemptCount] >= 0");
            table.HasCheckConstraint(
                "CK_Outbox_Lifecycle",
                "[ProcessedAtUtc] IS NULL OR ([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)");
        });
        builder.HasKey(message => message.Id);
        builder.Property(message => message.MessageType).HasMaxLength(OutboxMessage.MaximumMessageTypeLength);
        builder.Property(message => message.Payload).HasMaxLength(OutboxMessage.MaximumPayloadLength);
        builder.Property(message => message.LastErrorCode).HasMaxLength(OutboxMessage.MaximumErrorCodeLength);
        builder.Property(message => message.OccurredAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.ClaimedUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.NextAttemptAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.ProcessedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.RowVersion).IsRowVersion();
        builder.HasIndex(message => new
        {
            message.ProcessedAtUtc,
            message.NextAttemptAtUtc,
            message.ClaimedUntilUtc,
            message.OccurredAtUtc
        })
            .HasFilter("[ProcessedAtUtc] IS NULL")
            .HasDatabaseName("IX_Outbox_Pending");
    }
}
