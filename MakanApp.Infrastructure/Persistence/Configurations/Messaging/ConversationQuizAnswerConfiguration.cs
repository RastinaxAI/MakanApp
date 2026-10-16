using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationQuizAnswerConfiguration : IEntityTypeConfiguration<ConversationQuizAnswer>
{
    public void Configure(EntityTypeBuilder<ConversationQuizAnswer> b)
    {
        b.ToTable("ConversationQuizAnswers", "messaging", t => t.HasCheckConstraint("CK_ConversationQuizAnswers_IsQuiz", "[IsQuiz]=1"));
        b.HasKey(k => k.PollId); b.Property(k => k.PollId).ValueGeneratedNever();
        b.Property(k => k.Explanation).HasMaxLength(ConversationPollDefinition.MaximumExplanationLength);
        b.HasOne<ConversationPoll>().WithMany().HasForeignKey(k => new { k.PollId, k.IsQuiz }).HasPrincipalKey(p => new { p.Id, p.IsQuiz }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ConversationPollOption>().WithMany().HasForeignKey(k => new { k.PollId, k.CorrectOptionNumber }).OnDelete(DeleteBehavior.Restrict);
    }
}
