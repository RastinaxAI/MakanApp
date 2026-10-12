using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class UserContactConfiguration : IEntityTypeConfiguration<UserContact>
{
    public void Configure(EntityTypeBuilder<UserContact> builder)
    {
        builder.ToTable("UserContacts", "messaging", table =>
            table.HasCheckConstraint(
                "CK_UserContacts_DistinctUsers",
                "[OwnerUserId] <> [ContactUserId]"));
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.SavedName)
            .HasMaxLength(UserContact.MaximumSavedNameLength);
        builder.Property(contact => contact.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(contact => contact.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(contact => contact.RowVersion).IsRowVersion();

        builder.HasIndex(contact => new { contact.OwnerUserId, contact.ContactUserId })
            .IsUnique()
            .HasDatabaseName("UX_UserContacts_Owner_Contact");
        builder.HasIndex(contact => new { contact.OwnerUserId, contact.IsFavorite })
            .HasDatabaseName("IX_UserContacts_Owner_IsFavorite");
        builder.HasIndex(contact => new { contact.OwnerUserId, contact.UpdatedAtUtc })
            .HasDatabaseName("IX_UserContacts_Owner_UpdatedAtUtc");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(contact => contact.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(contact => contact.ContactUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
