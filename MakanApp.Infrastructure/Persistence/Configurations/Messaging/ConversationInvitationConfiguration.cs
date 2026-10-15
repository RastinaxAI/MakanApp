using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationInvitationConfiguration : IEntityTypeConfiguration<ConversationInvitation>
{
    public void Configure(EntityTypeBuilder<ConversationInvitation> builder)
    {
        builder.ToTable("ConversationInvitations", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationInvitations_Status", "[Status] IN (1,2,3,4,5)");
            table.HasCheckConstraint("CK_ConversationInvitations_Pair", "[InviterUserId] <> [TargetUserId]");
            table.HasCheckConstraint("CK_ConversationInvitations_Times", "[ExpiresAtUtc] > [CreatedAtUtc] AND (([Status]=1 AND [CompletedAtUtc] IS NULL) OR ([Status]<>1 AND [CompletedAtUtc] IS NOT NULL))");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(i => i.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(i => i.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(i => i.RowVersion).IsRowVersion();
        builder.HasIndex(i => new { i.ConversationId, i.TargetUserId }).IsUnique().HasFilter("[Status]=1");
        builder.HasIndex(i => new { i.TargetUserId, i.Status, i.CreatedAtUtc });
        builder.HasOne<Conversation>().WithMany().HasForeignKey(i => i.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.InviterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.TargetUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
