using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationPollVoteConfiguration : IEntityTypeConfiguration<ConversationPollVote>
{
    public void Configure(EntityTypeBuilder<ConversationPollVote> b)
    {
        b.ToTable("ConversationPollVotes", "messaging", t => t.HasCheckConstraint("CK_ConversationPollVotes_State", "[Revision]>0 AND ([IsQuiz]=0 OR [Revision]=1) AND [UpdatedAtUtc]>=[CreatedAtUtc]"));
        b.HasKey(v => new { v.PollId, v.UserId }); b.Property(v => v.RowVersion).IsRowVersion();
        b.Property(v => v.CreatedAtUtc).HasColumnType("datetime2(7)"); b.Property(v => v.UpdatedAtUtc).HasColumnType("datetime2(7)");
        b.HasOne<ConversationPoll>().WithMany().HasForeignKey(v => new { v.PollId, v.IsQuiz }).HasPrincipalKey(p => new { p.Id, p.IsQuiz }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(v => new { v.PollId, v.CreatedAtUtc, v.UserId });
    }
}
