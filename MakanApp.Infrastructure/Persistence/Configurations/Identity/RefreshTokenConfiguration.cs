using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "identity", table =>
        {
            table.HasCheckConstraint(
                "CK_RefreshTokens_Lifecycle",
                "[UsedAtUtc] IS NULL OR [ReplacedByTokenId] IS NOT NULL");
            table.HasCheckConstraint(
                "CK_RefreshTokens_Expiry",
                "[ExpiresAtUtc] > [CreatedAtUtc]");
        });
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(token => token.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(token => token.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(token => token.UsedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(token => token.RevokedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(token => token.RowVersion).IsRowVersion();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_TokenHash");
        builder.HasIndex(token => new { token.SessionId, token.ExpiresAtUtc })
            .HasDatabaseName("IX_RefreshTokens_Session_ExpiresAtUtc");

        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
