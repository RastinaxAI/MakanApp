using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationChecklistPersonalStateConfiguration : IEntityTypeConfiguration<ConversationChecklistPersonalState>
{
    public void Configure(EntityTypeBuilder<ConversationChecklistPersonalState> b)
    {
        b.ToTable("ConversationChecklistPersonalStates", "messaging", t => t.HasCheckConstraint("CK_ConversationChecklistPersonalStates_Mode", "[Mode]=1 AND [Revision]>0"));
        b.HasKey(s => new { s.ChecklistId, s.ItemId, s.UserId }); b.Property(s => s.RowVersion).IsRowVersion();
        b.HasOne<ConversationChecklistItem>().WithMany().HasForeignKey(s => new { s.ChecklistId, s.ItemId, s.Mode })
            .HasPrincipalKey(i => new { i.ChecklistId, i.Id, i.Mode }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.ChecklistId, s.UserId, s.ItemId });
    }
}
