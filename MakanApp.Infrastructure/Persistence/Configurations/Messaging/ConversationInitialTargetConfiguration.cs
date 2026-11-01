using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationInitialTargetConfiguration : IEntityTypeConfiguration<ConversationInitialTarget>
{
    public void Configure(EntityTypeBuilder<ConversationInitialTarget> builder)
    {
        builder.ToTable("ConversationInitialTargets", "messaging", table =>
            table.HasCheckConstraint("CK_ConversationInitialTargets_Outcome",
                "([Outcome]=2 AND [InvitationId] IS NOT NULL AND [InvitationExpiresAtUtc] IS NOT NULL) OR ([Outcome] IN (1,3) AND [InvitationId] IS NULL AND [InvitationExpiresAtUtc] IS NULL)"));
        builder.HasKey(item => new { item.ConversationId, item.TargetUserId });
        builder.HasOne<Conversation>().WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConversationInvitation>().WithMany().HasForeignKey(item => item.InvitationId).OnDelete(DeleteBehavior.Restrict);
        // TargetUserId ممکن است selector ناموجود باشد؛ FK هویت یا افشای وجود حساب از آن ساخته نمی‌شود.
    }
}
