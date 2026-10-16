using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationPollOptionConfiguration : IEntityTypeConfiguration<ConversationPollOption>
{
    public void Configure(EntityTypeBuilder<ConversationPollOption> b)
    {
        b.ToTable("ConversationPollOptions", "messaging", t => t.HasCheckConstraint("CK_ConversationPollOptions_Value", "[Number] BETWEEN 1 AND 10 AND LEN(LTRIM(RTRIM([Text])))>0"));
        b.HasKey(o => new { o.PollId, o.Number }); b.Property(o => o.Text).HasMaxLength(ConversationPollDefinition.MaximumOptionLength).IsRequired();
        b.HasOne<ConversationPoll>().WithMany().HasForeignKey(o => o.PollId).OnDelete(DeleteBehavior.Restrict);
    }
}
