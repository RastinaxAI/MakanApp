using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationPollConfiguration : IEntityTypeConfiguration<ConversationPoll>
{
    public void Configure(EntityTypeBuilder<ConversationPoll> b)
    {
        b.ToTable("ConversationPolls", "messaging", t =>
        {
            t.HasCheckConstraint("CK_ConversationPolls_Definition", "[OptionCount] BETWEEN 2 AND 10 AND DATALENGTH([DefinitionHash])=32 AND ([IsQuiz]=0 OR [AllowsMultipleAnswers]=0)");
            t.HasCheckConstraint("CK_ConversationPolls_State", "[Revision]>0 AND [UpdatedAtUtc]>=[CreatedAtUtc] AND (([Status]=1 AND [ClosedAtUtc] IS NULL AND [ClosedByUserId] IS NULL) OR ([Status]=2 AND [ClosedAtUtc] IS NOT NULL AND [ClosedAtUtc]>=[CreatedAtUtc] AND [ClosedByUserId] IS NOT NULL))");
        });
        b.HasKey(p => p.Id); b.Property(p => p.Id).ValueGeneratedNever(); b.HasAlternateKey(p => new { p.Id, p.IsQuiz });
        b.Property(p => p.DefinitionHash).HasColumnType("binary(32)").IsRequired(); b.Property(p => p.RowVersion).IsRowVersion();
        b.Property(p => p.CreatedAtUtc).HasColumnType("datetime2(7)"); b.Property(p => p.UpdatedAtUtc).HasColumnType("datetime2(7)"); b.Property(p => p.ClosedAtUtc).HasColumnType("datetime2(7)");
        b.HasOne<Message>().WithMany().HasForeignKey(p => new { p.Id, p.ConversationId }).HasPrincipalKey(m => new { m.Id, m.ConversationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.ConversationId, p.CreatedAtUtc, p.Id });
    }
}
