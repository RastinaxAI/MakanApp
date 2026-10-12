using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationUserStateConfiguration : IEntityTypeConfiguration<ConversationUserState>
{
    public void Configure(EntityTypeBuilder<ConversationUserState> builder)
    {
        builder.ToTable("ConversationUserStates", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationUserStates_MuteMode", "[MuteMode] IN (0, 1, 2)");
            table.HasCheckConstraint(
                "CK_ConversationUserStates_MuteShape",
                "([MuteMode] IN (0, 1) AND [MutedUntilUtc] IS NULL) OR ([MuteMode] = 2 AND [MutedUntilUtc] IS NOT NULL)");
        });
        builder.HasKey(state => new { state.ConversationId, state.UserId });
        builder.Property(state => state.ArchivedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.PinnedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.MutedUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.RowVersion).IsRowVersion();
        builder.HasIndex(state => new
        {
            state.UserId,
            state.ArchivedAtUtc,
            state.PinnedAtUtc,
            state.UpdatedAtUtc
        }).HasDatabaseName("IX_ConversationUserStates_User_List");
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(state => state.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(state => state.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
