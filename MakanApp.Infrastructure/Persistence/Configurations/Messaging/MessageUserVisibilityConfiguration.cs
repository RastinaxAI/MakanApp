using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessageUserVisibilityConfiguration : IEntityTypeConfiguration<MessageUserVisibility>
{
    public void Configure(EntityTypeBuilder<MessageUserVisibility> builder)
    {
        builder.ToTable("MessageUserVisibilities", "messaging");
        builder.HasKey(visibility => new { visibility.MessageId, visibility.UserId });
        builder.Property(visibility => visibility.HiddenAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(visibility => new { visibility.UserId, visibility.MessageId })
            .HasDatabaseName("IX_MessageUserVisibilities_User_Message");
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(visibility => visibility.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(visibility => visibility.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
