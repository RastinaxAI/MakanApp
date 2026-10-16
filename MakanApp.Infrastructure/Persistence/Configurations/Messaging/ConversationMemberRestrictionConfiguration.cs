using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationMemberRestrictionConfiguration : IEntityTypeConfiguration<ConversationMemberRestriction>
{
    public void Configure(EntityTypeBuilder<ConversationMemberRestriction> builder)
    {
        builder.ToTable("ConversationMemberRestrictions", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationMemberRestrictions_Capabilities", "[DeniedCapabilities] BETWEEN 0 AND 7");
            table.HasCheckConstraint("CK_ConversationMemberRestrictions_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_ConversationMemberRestrictions_Clear", "[IsBanned] = 1 OR [DeniedCapabilities] <> 0 OR [UntilUtc] IS NULL");
        });
        builder.HasKey(r => new { r.ConversationId, r.UserId }); builder.Property(r => r.RowVersion).IsRowVersion();
        builder.Property(r => r.UntilUtc).HasColumnType("datetime2(7)");
        builder.HasOne<Conversation>().WithMany().HasForeignKey(r => r.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
