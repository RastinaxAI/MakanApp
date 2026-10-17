using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationChecklistConfiguration : IEntityTypeConfiguration<ConversationChecklist>
{
    public void Configure(EntityTypeBuilder<ConversationChecklist> b)
    {
        b.ToTable("ConversationChecklists", "messaging", t =>
        {
            t.HasCheckConstraint("CK_ConversationChecklists_Definition", "[Mode] IN (1,2) AND [DefinitionRevision]>0 AND DATALENGTH([CreationHash])=32 AND [UpdatedAtUtc]>=[CreatedAtUtc]");
        });
        b.HasKey(c => c.Id); b.Property(c => c.Id).ValueGeneratedNever(); b.HasAlternateKey(c => new { c.Id, c.Mode });
        b.Property(c => c.CreationHash).HasColumnType("binary(32)").IsRequired(); b.Property(c => c.RowVersion).IsRowVersion();
        b.Property(c => c.CreatedAtUtc).HasColumnType("datetime2(7)"); b.Property(c => c.UpdatedAtUtc).HasColumnType("datetime2(7)");
        b.HasOne<Message>().WithMany().HasForeignKey(c => new { c.Id, c.ConversationId }).HasPrincipalKey(m => new { m.Id, m.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.ConversationId, c.CreatedAtUtc, c.Id });
    }
}
