using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationInviteLinkConfiguration : IEntityTypeConfiguration<ConversationInviteLink>
{
    public void Configure(EntityTypeBuilder<ConversationInviteLink> builder)
    {
        builder.ToTable("ConversationInviteLinks", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationInviteLinks_Uses", "[MaximumUses] BETWEEN 1 AND 10000 AND [UsedCount] BETWEEN 0 AND [MaximumUses]");
            table.HasCheckConstraint("CK_ConversationInviteLinks_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            table.HasCheckConstraint("CK_ConversationInviteLinks_CreatorContext", "([CreatorMembershipId] IS NULL AND [CreatorRole] IS NULL) OR ([CreatorMembershipId] IS NOT NULL AND [CreatorRole] IS NOT NULL AND [CreatorRole] IN (1,2,3,4))");
            table.HasCheckConstraint("CK_ConversationInviteLinks_Hash", "LEN([TokenHash]) = 64");
        });
        builder.HasKey(l => l.Id); builder.HasAlternateKey(l => new { l.Id, l.ConversationId });
        builder.Property(l => l.TokenHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(l => l.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(l => l.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(l => l.RevokedAtUtc).HasColumnType("datetime2(7)"); builder.Property(l => l.RowVersion).IsRowVersion();
        builder.HasIndex(l => l.TokenHash).IsUnique(); builder.HasIndex(l => new { l.ConversationId, l.CreatedAtUtc, l.Id });
        builder.HasOne<Conversation>().WithMany().HasForeignKey(l => l.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>().WithMany().HasForeignKey(l => l.CreatorMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ConversationJoinRequestConfiguration : IEntityTypeConfiguration<ConversationJoinRequest>
{
    public void Configure(EntityTypeBuilder<ConversationJoinRequest> builder)
    {
        builder.ToTable("ConversationJoinRequests", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationJoinRequests_Status", "[Status] IN (1,2,3,4,5)");
            table.HasCheckConstraint("CK_ConversationJoinRequests_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            table.HasCheckConstraint("CK_ConversationJoinRequests_Lifecycle", "([Status] = 1 AND [CompletedAtUtc] IS NULL) OR ([Status] <> 1 AND [CompletedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_ConversationJoinRequests_RequesterContext", "([RequesterMembershipId] IS NULL AND [RequesterRole] IS NULL) OR ([RequesterMembershipId] IS NOT NULL AND [RequesterRole] IS NOT NULL AND [RequesterRole] IN (1,2,3,4))");
        });
        builder.HasKey(r => r.Id); builder.Property(r => r.RowVersion).IsRowVersion();
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetime2(7)"); builder.Property(r => r.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(r => r.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(r => new { r.ConversationId, r.UserId }).IsUnique().HasFilter("[Status] = 1");
        builder.HasIndex(r => new { r.UserId, r.CreatedAtUtc, r.Id }); builder.HasIndex(r => new { r.ConversationId, r.CreatedAtUtc, r.Id });
        builder.HasOne<ConversationInviteLink>().WithMany().HasForeignKey(r => new { r.InviteLinkId, r.ConversationId })
            .HasPrincipalKey(l => new { l.Id, l.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Conversation>().WithMany().HasForeignKey(r => r.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>().WithMany().HasForeignKey(r => r.RequesterMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
