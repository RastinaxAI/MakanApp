using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> builder)
    {
        builder.ToTable("ModerationActions", "messaging", table =>
            table.HasCheckConstraint("CK_ModerationActions_Type", "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(item => new { item.ModerationCaseId, item.CreatedAtUtc, item.Id })
            .HasDatabaseName("IX_ModerationActions_Case_Created");
        builder.HasOne<ModerationCase>()
            .WithMany()
            .HasForeignKey(item => item.ModerationCaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
