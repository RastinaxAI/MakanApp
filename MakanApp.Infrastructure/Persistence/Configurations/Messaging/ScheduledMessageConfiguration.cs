using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ScheduledMessageConfiguration : IEntityTypeConfiguration<ScheduledMessage>
{
    public void Configure(EntityTypeBuilder<ScheduledMessage> b)
    {
        b.ToTable("ScheduledMessages", "messaging", t =>
        {
            t.HasCheckConstraint("CK_ScheduledMessages_Status", "[Status] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_ScheduledMessages_Kind", "[Kind] IN (1,2,3,4,5,6)");
            t.HasCheckConstraint("CK_ScheduledMessages_Hash", "DATALENGTH([PayloadHash]) = 32");
            t.HasCheckConstraint("CK_ScheduledMessages_UTCOrder", "[ScheduledAtUtc] > [CreatedAtUtc]");
            t.HasCheckConstraint("CK_ScheduledMessages_Attempts", "[AttemptCount] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_ScheduledMessages_Lease", "([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL) OR ([Status]=1 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_ScheduledMessages_Lifecycle", "([Status]=1 AND [CompletedAtUtc] IS NULL AND [DispatchedMessageId] IS NULL) OR ([Status]=2 AND [CompletedAtUtc] IS NOT NULL AND [DispatchedMessageId] IS NOT NULL) OR ([Status] IN (3,4) AND [CompletedAtUtc] IS NOT NULL AND [DispatchedMessageId] IS NULL)");
            t.HasCheckConstraint("CK_ScheduledMessages_Context", "([WorkspaceType]=1 AND [OrganizationId] IS NULL AND [MembershipId] IS NULL AND [Role] IS NULL AND [SubjectOrganizationPersonId] IS NULL) OR ([WorkspaceType]=2 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [Role] IN (1,2,3,4))");
        });
        b.HasKey(s => s.Id); b.Property(s => s.Text).HasMaxLength(Message.StorageMaximumTextLength);
        b.Property(s => s.PayloadHash).HasMaxLength(32).IsFixedLength(); b.Property(s => s.LastFailureCode).HasMaxLength(100);
        b.Property(s => s.RowVersion).IsRowVersion();
        b.Property(s => s.CreatedAtUtc).HasColumnType("datetime2(7)"); b.Property(s => s.ScheduledAtUtc).HasColumnType("datetime2(7)");
        b.Property(s => s.CompletedAtUtc).HasColumnType("datetime2(7)"); b.Property(s => s.ClaimedUntilUtc).HasColumnType("datetime2(7)"); b.Property(s => s.NextAttemptAtUtc).HasColumnType("datetime2(7)");
        b.HasIndex(s => new { s.ConversationId, s.UserId, s.ClientMessageId }).IsUnique();
        b.HasIndex(s => new { s.Status, s.ScheduledAtUtc, s.Id }); b.HasIndex(s => new { s.ConversationId, s.UserId, s.Status });
        b.HasOne<Conversation>().WithMany().HasForeignKey(s => s.ConversationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.ContactUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Organization.Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Membership>().WithMany().HasForeignKey(s => s.MembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrganizationPerson>().WithMany().HasForeignKey(s => s.SubjectOrganizationPersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ConversationTopic>().WithMany().HasForeignKey(s => new { s.TopicId, s.ConversationId }).HasPrincipalKey(t => new { t.Id, t.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Message>().WithMany().HasForeignKey(s => new { s.ReplyToMessageId, s.ConversationId }).HasPrincipalKey(m => new { m.Id, m.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Message>().WithMany().HasForeignKey(s => new { s.DispatchedMessageId, s.ConversationId }).HasPrincipalKey(m => new { m.Id, m.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Attachments).WithOne().HasForeignKey(a => a.ScheduledMessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Mentions).WithOne().HasForeignKey(m => m.ScheduledMessageId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(s => s.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field); b.Navigation(s => s.Mentions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class ScheduledMessageAttachmentConfiguration : IEntityTypeConfiguration<ScheduledMessageAttachment>
{
    public void Configure(EntityTypeBuilder<ScheduledMessageAttachment> b)
    {
        b.ToTable("ScheduledMessageAttachments", "messaging"); b.HasKey(a => new { a.ScheduledMessageId, a.FileAssetId });
        b.HasOne<FileAsset>().WithMany().HasForeignKey(a => a.FileAssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ScheduledMessageMentionConfiguration : IEntityTypeConfiguration<ScheduledMessageMention>
{
    public void Configure(EntityTypeBuilder<ScheduledMessageMention> b)
    {
        b.ToTable("ScheduledMessageMentions", "messaging"); b.HasKey(m => new { m.ScheduledMessageId, m.UserId });
        b.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
