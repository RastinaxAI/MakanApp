using MakanApp.Domain.Identity;
using MakanApp.Domain.Storage;
using MakanApp.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Support;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> b)
    {
        b.ToTable("SupportTickets", "support", t => {
            t.HasCheckConstraint("CK_SupportTickets_Status", "[Status] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_SupportTickets_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_SupportTickets_Resolution", "([Status]=5 AND [ResolvedAtUtc] IS NOT NULL) OR ([Status]<>5 AND [ResolvedAtUtc] IS NULL)");
        });
        b.HasKey(t => t.Id); b.Property(t => t.Subject).HasMaxLength(200); b.Property(t => t.Category).HasMaxLength(64);
        b.Property(t => t.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(t => t.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(t => t.AssignedStaffUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => new { t.OwnerUserId, t.UpdatedAtUtc }); b.HasIndex(t => new { t.Status, t.UpdatedAtUtc });
    }
}
public sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> b)
    {
        b.ToTable("SupportTicketMessages", "support"); b.HasKey(t => t.Id);
        b.HasAlternateKey(t => new { t.TicketId, t.Id });
        b.Property(t => t.Body).HasMaxLength(8000);
        b.HasOne<SupportTicket>().WithMany().HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(t => t.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => new { t.TicketId, t.TicketVersion }).IsUnique();
    }
}
public sealed class SupportTicketAttachmentConfiguration : IEntityTypeConfiguration<SupportTicketAttachment>
{
    public void Configure(EntityTypeBuilder<SupportTicketAttachment> b)
    {
        b.ToTable("SupportTicketAttachments", "support"); b.HasKey(a => new { a.MessageId, a.FileAssetId });
        b.HasOne<SupportTicketMessage>().WithMany().HasForeignKey(a => new { a.TicketId, a.MessageId }).HasPrincipalKey(m => new { m.TicketId, m.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileAsset>().WithMany().HasForeignKey(a => a.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => a.FileAssetId).IsUnique();
    }
}
public sealed class SupportTicketOperationConfiguration : IEntityTypeConfiguration<SupportTicketOperation>
{
    public void Configure(EntityTypeBuilder<SupportTicketOperation> b)
    {
        b.ToTable("SupportTicketOperations", "support"); b.HasKey(o => new { o.ActorUserId, o.ClientOperationId });
        b.Property(o => o.Fingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasOne<User>().WithMany().HasForeignKey(o => o.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupportTicket>().WithMany().HasForeignKey(o => o.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
