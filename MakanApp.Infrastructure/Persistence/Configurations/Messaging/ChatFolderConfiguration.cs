using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ChatFolderConfiguration : IEntityTypeConfiguration<ChatFolder>
{
    public void Configure(EntityTypeBuilder<ChatFolder> builder)
    {
        builder.ToTable("ChatFolders", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ChatFolders_SortOrder", "[SortOrder] BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_ChatFolders_Types", "([IncludedTypes] & ~15) = 0 AND ([ExcludedTypes] & ~15) = 0");
            table.HasCheckConstraint("CK_ChatFolders_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_ChatFolders_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        builder.HasKey(f => f.Id); builder.Property(f => f.Name).HasMaxLength(ChatFolder.MaximumNameLength);
        builder.Property(f => f.RowVersion).IsRowVersion();
        builder.HasIndex(f => new { f.OwnerUserId, f.SortOrder, f.Id });
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FolderConversationOverrideConfiguration : IEntityTypeConfiguration<FolderConversationOverride>
{
    public void Configure(EntityTypeBuilder<FolderConversationOverride> builder)
    {
        builder.ToTable("FolderConversationOverrides", "messaging", table =>
            table.HasCheckConstraint("CK_FolderConversationOverrides_Pin", "[PinOrder] IS NULL OR ([Included] = 1 AND [PinOrder] BETWEEN 0 AND 199)"));
        builder.HasKey(o => new { o.FolderId, o.ConversationId });
        builder.HasOne<ChatFolder>().WithMany().HasForeignKey(o => o.FolderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Conversation>().WithMany().HasForeignKey(o => o.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.FolderId, o.PinOrder });
    }
}
