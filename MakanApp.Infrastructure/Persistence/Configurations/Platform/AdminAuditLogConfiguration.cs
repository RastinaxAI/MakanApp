using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Platform;

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs", "platform", table =>
            table.HasCheckConstraint("CK_AdminAuditLogs_Result", "[Result] IN (1, 2, 3)"));
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Action).HasMaxLength(AdminAuditLog.MaximumActionLength).IsRequired();
        builder.Property(audit => audit.TargetType).HasMaxLength(AdminAuditLog.MaximumTargetTypeLength).IsRequired();
        builder.Property(audit => audit.TargetId).HasMaxLength(AdminAuditLog.MaximumTargetIdLength).IsRequired();
        builder.Property(audit => audit.Reason).HasMaxLength(AdminAuditLog.MaximumReasonLength).IsRequired();
        builder.Property(audit => audit.CorrelationId).HasMaxLength(AdminAuditLog.MaximumCorrelationIdLength).IsRequired();
        builder.Property(audit => audit.BeforeSummary).HasMaxLength(AdminAuditLog.MaximumSummaryLength);
        builder.Property(audit => audit.AfterSummary).HasMaxLength(AdminAuditLog.MaximumSummaryLength);
        builder.Property(audit => audit.OccurredAtUtc).HasColumnType("datetime2(7)");

        builder.HasIndex(audit => audit.OccurredAtUtc)
            .IsDescending()
            .HasDatabaseName("IX_AdminAuditLogs_OccurredAtUtc");
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AdminAuditLogs_Actor_OccurredAtUtc");
        builder.HasIndex(audit => new { audit.Action, audit.OccurredAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AdminAuditLogs_Action_OccurredAtUtc");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(audit => audit.ActorPersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(audit => audit.ActorSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(audit => audit.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
