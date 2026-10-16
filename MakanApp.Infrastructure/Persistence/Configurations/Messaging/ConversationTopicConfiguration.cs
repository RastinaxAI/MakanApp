using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationTopicConfiguration : IEntityTypeConfiguration<ConversationTopic>
{
    public void Configure(EntityTypeBuilder<ConversationTopic> builder)
    {
        builder.ToTable("ConversationTopics", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationTopics_Status", "[Status] IN (1,2)");
            table.HasCheckConstraint("CK_ConversationTopics_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_ConversationTopics_Title", "LEN(LTRIM(RTRIM([Title]))) > 0");
        });
        builder.HasKey(t => t.Id); builder.HasAlternateKey(t => new { t.Id, t.ConversationId });
        builder.Property(t => t.Title).HasMaxLength(Conversation.TitleMaximumLength);
        builder.Property(t => t.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(t => t.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(t => t.RowVersion).IsRowVersion();
        builder.HasIndex(t => new { t.ConversationId, t.CreatedAtUtc, t.Id });
        builder.HasOne<Conversation>().WithMany().HasForeignKey(t => t.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
