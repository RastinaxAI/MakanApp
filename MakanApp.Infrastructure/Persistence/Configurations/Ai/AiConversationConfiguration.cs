using System.Text.Json;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Intelligence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MakanApp.Infrastructure.Persistence.Configurations.Ai;

public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations", "intelligence", table => {
            table.HasCheckConstraint("CK_AiConversations_Sequence", "[NextSequence] >= 1 AND [NextSequence] % 2 = 1");
            table.HasCheckConstraint("CK_AiConversations_Sources", "ISJSON([SourcePins]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.OwnerUserId });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ContextFingerprint).HasColumnType("binary(32)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        var converter = new ValueConverter<IReadOnlyList<AiConversationSourcePin>, string>(
            value => Serialize(value), value => Deserialize(value));
        var comparer = new ValueComparer<IReadOnlyList<AiConversationSourcePin>>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, pin) => HashCode.Combine(hash, pin.GetHashCode())),
            value => value.ToArray());
        var sources = builder.Property(x => x.SourcePins).HasConversion(converter).HasMaxLength(4000).IsRequired();
        sources.Metadata.SetValueComparer(comparer);
        builder.HasIndex(x => new { x.OwnerUserId, x.UpdatedAtUtc, x.Id });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
    private static string Serialize(IReadOnlyList<AiConversationSourcePin> pins) => JsonSerializer.Serialize(pins);
    private static IReadOnlyList<AiConversationSourcePin> Deserialize(string json) => JsonSerializer.Deserialize<AiConversationSourcePin[]>(json)!;
}

public sealed class AiConversationTurnConfiguration : IEntityTypeConfiguration<AiConversationTurn>
{
    public void Configure(EntityTypeBuilder<AiConversationTurn> builder)
    {
        builder.ToTable("AiConversationTurns", "intelligence", table => {
            table.HasCheckConstraint("CK_AiConversationTurns_RoleSequence", "([Role]=1 AND [Sequence]>0 AND [Sequence]%2=1) OR ([Role]=2 AND [Sequence]>0 AND [Sequence]%2=0)");
            table.HasCheckConstraint("CK_AiConversationTurns_Completion", "([IsCompleted]=0 AND [Role]=1 AND [Text] IS NULL AND [CompletedAtUtc] IS NULL) OR ([IsCompleted]=1 AND [Text] IS NOT NULL AND DATALENGTH([Text])>0 AND [CompletedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_AiConversationTurns_TextBound", "[Text] IS NULL OR ([Role]=1 AND DATALENGTH([Text])<=16000) OR ([Role]=2 AND DATALENGTH([Text])<=131072)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Role).HasConversion<int>();
        builder.Property(x => x.Text).HasColumnType("nvarchar(max)");
        builder.Property(x => x.IntentHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");
        builder.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.OwnerUserId, x.OperationId, x.Role }).IsUnique();
        builder.HasOne<AiConversation>().WithMany().HasForeignKey(x => new { x.ConversationId, x.OwnerUserId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
