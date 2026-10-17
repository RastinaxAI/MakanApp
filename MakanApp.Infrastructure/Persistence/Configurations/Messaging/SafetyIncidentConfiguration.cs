using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class SafetyIncidentConfiguration : IEntityTypeConfiguration<SafetyIncident>
{
    public void Configure(EntityTypeBuilder<SafetyIncident> builder)
    {
        builder.ToTable("SafetyIncidents", "messaging", table =>
        {
            table.HasCheckConstraint("CK_SafetyIncidents_ContentKind", "[ContentKind] IN (1, 2, 3, 4, 5, 6, 7, 8)");
            table.HasCheckConstraint("CK_SafetyIncidents_Decision", "[Decision] IN (2, 3)");
            table.HasCheckConstraint("CK_SafetyIncidents_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_SafetyIncidents_Lifecycle",
                "([Status] = 1 AND [ClosedAtUtc] IS NULL) OR ([Status] IN (2, 3) AND [ClosedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RiskLevel)
            .HasMaxLength(SafetyIncident.MaximumRiskLevelLength);
        builder.Property(item => item.ProviderReference)
            .HasMaxLength(SafetyIncident.MaximumProviderReferenceLength);
        builder.Property(item => item.PolicyCodes)
            .HasMaxLength(SafetyIncident.MaximumPolicyCodesLength);
        builder.Property(item => item.ContentSnapshot)
            .HasMaxLength(Message.StorageMaximumTextLength);
        builder.Property(item => item.ContentHash)
            .HasColumnType("binary(32)")
            .ValueGeneratedNever();
        builder.Property(item => item.RequestPayloadHash)
            .HasColumnType("binary(32)")
            .ValueGeneratedNever();
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.ClosedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new
        {
            item.ConversationId,
            item.ActorUserId,
            item.ClientMessageId
        })
            .IsUnique()
            .HasDatabaseName("UX_SafetyIncidents_Conversation_Actor_ClientMessageId");
        builder.HasIndex(item => new { item.ConversationId, item.CreatedAtUtc })
            .HasDatabaseName("IX_SafetyIncidents_Conversation_Created");
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(item => item.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
