using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class SavedMessageOrganizationConfiguration : IEntityTypeConfiguration<SavedMessageOrganization>
{
    public void Configure(EntityTypeBuilder<SavedMessageOrganization> builder)
    {
        builder.ToTable("SavedMessageOrganizations", "messaging", table => table.HasCheckConstraint("CK_SavedMessageOrganizations_Revision", "[Revision] > 0"));
        builder.HasKey(o => o.MessageId); builder.Property(o => o.RowVersion).IsRowVersion();
        builder.HasOne<Message>().WithOne().HasForeignKey<SavedMessageOrganization>(o => o.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.OwnerUserId, o.MessageId });
    }
}

public sealed class SavedMessageLabelConfiguration : IEntityTypeConfiguration<SavedMessageLabel>
{
    public void Configure(EntityTypeBuilder<SavedMessageLabel> builder)
    {
        builder.ToTable("SavedMessageLabels", "messaging", table => table.HasCheckConstraint("CK_SavedMessageLabels_Name", "LEN(LTRIM(RTRIM([Name]))) > 0"));
        builder.HasKey(l => new { l.MessageId, l.Name });
        builder.Property(l => l.Name).HasMaxLength(SavedMessageLabel.MaximumLength).UseCollation("Latin1_General_100_BIN2");
        builder.HasOne<SavedMessageOrganization>().WithMany().HasForeignKey(l => l.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => new { l.Name, l.MessageId });
    }
}
