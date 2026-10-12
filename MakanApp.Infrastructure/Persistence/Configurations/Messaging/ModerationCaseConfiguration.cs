using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ModerationCaseConfiguration : IEntityTypeConfiguration<ModerationCase>
{
    public void Configure(EntityTypeBuilder<ModerationCase> builder)
    {
        builder.ToTable("ModerationCases", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ModerationCases_Source", "[Source] IN (1, 2)");
            table.HasCheckConstraint("CK_ModerationCases_QueueScope", "[QueueScope] IN (1, 2)");
            table.HasCheckConstraint("CK_ModerationCases_Priority", "[Priority] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ModerationCases_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_ModerationCases_SourceShape",
                "([Source] = 1 AND [AbuseReportId] IS NOT NULL AND [SafetyIncidentId] IS NULL) OR " +
                "([Source] = 2 AND [AbuseReportId] IS NULL AND [SafetyIncidentId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ModerationCases_Lifecycle",
                "([Status] = 1 AND [AssignedReviewerUserId] IS NULL AND [ReviewStartedAtUtc] IS NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NULL) OR " +
                "([Status] = 2 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NULL) OR " +
                "([Status] = 3 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NOT NULL AND [DismissedAtUtc] IS NULL) OR " +
                "([Status] = 4 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.InternalNote)
            .HasMaxLength(ModerationCase.MaximumInternalNoteLength);
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.ReviewStartedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.ResolvedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.DismissedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.AbuseReportId)
            .IsUnique()
            .HasFilter("[AbuseReportId] IS NOT NULL")
            .HasDatabaseName("UX_ModerationCases_AbuseReportId");
        builder.HasIndex(item => item.SafetyIncidentId)
            .IsUnique()
            .HasFilter("[SafetyIncidentId] IS NOT NULL")
            .HasDatabaseName("UX_ModerationCases_SafetyIncidentId");
        builder.HasIndex(item => new
        {
            item.QueueScope,
            item.Status,
            item.Priority,
            item.CreatedAtUtc,
            item.Id
        }).HasDatabaseName("IX_ModerationCases_Queue_Status_Priority_Created");
        builder.HasIndex(item => new { item.ConversationId, item.Status, item.CreatedAtUtc })
            .HasDatabaseName("IX_ModerationCases_Conversation_Status_Created");
        builder.HasIndex(item => new { item.AssignedReviewerUserId, item.Status })
            .HasFilter("[AssignedReviewerUserId] IS NOT NULL")
            .HasDatabaseName("IX_ModerationCases_Assigned_Status");
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(item => item.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AbuseReport>()
            .WithMany()
            .HasForeignKey(item => item.AbuseReportId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SafetyIncident>()
            .WithMany()
            .HasForeignKey(item => item.SafetyIncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.AssignedReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
