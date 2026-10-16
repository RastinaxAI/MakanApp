using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationPollSelectionConfiguration : IEntityTypeConfiguration<ConversationPollSelection>
{
    public void Configure(EntityTypeBuilder<ConversationPollSelection> b)
    {
        b.ToTable("ConversationPollSelections", "messaging"); b.HasKey(s => new { s.PollId, s.UserId, s.OptionNumber });
        b.HasOne<ConversationPollVote>().WithMany().HasForeignKey(s => new { s.PollId, s.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ConversationPollOption>().WithMany().HasForeignKey(s => new { s.PollId, s.OptionNumber }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.PollId, s.OptionNumber, s.UserId });
    }
}
