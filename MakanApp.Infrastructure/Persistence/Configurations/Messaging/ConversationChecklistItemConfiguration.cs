using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationChecklistItemConfiguration : IEntityTypeConfiguration<ConversationChecklistItem>
{
    public void Configure(EntityTypeBuilder<ConversationChecklistItem> b)
    {
        b.ToTable("ConversationChecklistItems", "messaging", t =>
        {
            t.HasCheckConstraint("CK_ConversationChecklistItems_Definition", "LEN(LTRIM(RTRIM([Text])))>0 AND [Position] BETWEEN 1 AND 20 AND [CreatedAtRevision]>0 AND ([RetiredAtRevision] IS NULL OR [RetiredAtRevision]>[CreatedAtRevision])");
            t.HasCheckConstraint("CK_ConversationChecklistItems_State", "[StateRevision]>0 AND ([Mode]=2 OR ([Mode]=1 AND [IsChecked]=0 AND [StateRevision]=1))");
        });
        b.HasKey(i => new { i.ChecklistId, i.Id }); b.Property(i => i.Id).ValueGeneratedNever();
        b.HasAlternateKey(i => new { i.ChecklistId, i.Id, i.Mode });
        b.Ignore(i => i.IsActive); b.Property(i => i.Text).HasMaxLength(ConversationChecklistDefinition.MaximumItemLength).IsRequired();
        b.Property(i => i.RowVersion).IsRowVersion();
        b.HasOne<ConversationChecklist>().WithMany().HasForeignKey(i => new { i.ChecklistId, i.Mode }).HasPrincipalKey(c => new { c.Id, c.Mode }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => new { i.ChecklistId, i.RetiredAtRevision, i.Position });
    }
}
