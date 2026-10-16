namespace MakanApp.Domain.Messaging;

[Flags]
public enum FolderConversationTypes { None = 0, Direct = 1, Group = 2, Channel = 4, Saved = 8, All = 15 }

public sealed class ChatFolder
{
    public const int MaximumNameLength = 80;
    public const int MaximumFoldersPerUser = 20;
    public const int MaximumOverrides = 200;
    private ChatFolder() { }
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public FolderConversationTypes IncludedTypes { get; private set; }
    public FolderConversationTypes ExcludedTypes { get; private set; }
    public bool UnreadOnly { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ChatFolder Create(Guid id, Guid ownerUserId)
    {
        if (id == Guid.Empty || ownerUserId == Guid.Empty) throw new ArgumentException("شناسهٔ پوشه و مالک الزامی است.");
        return new() { Id = id, OwnerUserId = ownerUserId };
    }

    public void Update(string name, int sortOrder, FolderConversationTypes included, FolderConversationTypes excluded, bool unreadOnly)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaximumNameLength || name.Any(char.IsControl) ||
            sortOrder is < 0 or > 10000 || (included & ~FolderConversationTypes.All) != 0 || (excluded & ~FolderConversationTypes.All) != 0)
            throw new ArgumentException("تنظیم پوشه معتبر نیست.");
        Name = name.Trim(); SortOrder = sortOrder; IncludedTypes = included; ExcludedTypes = excluded; UnreadOnly = unreadOnly;
        Revision = checked(Revision + 1);
    }

    public ConversationType[] IncludedConversationTypes() => Types(IncludedTypes);
    public ConversationType[] ExcludedConversationTypes() => Types(ExcludedTypes);
    private static ConversationType[] Types(FolderConversationTypes mask) => Enum.GetValues<ConversationType>()
        .Where(type => (mask & (FolderConversationTypes)(1 << ((int)type - 1))) != 0).ToArray();
}

public sealed class FolderConversationOverride
{
    private FolderConversationOverride() { }
    public Guid FolderId { get; private set; }
    public Guid ConversationId { get; private set; }
    public bool Included { get; private set; }
    public int? PinOrder { get; private set; }
    public static FolderConversationOverride Create(Guid folderId, Guid conversationId, bool included, int? pinOrder)
    {
        if (folderId == Guid.Empty || conversationId == Guid.Empty || pinOrder is < 0 or >= ChatFolder.MaximumOverrides || !included && pinOrder.HasValue)
            throw new ArgumentException("انتخاب گفتگوی پوشه معتبر نیست.");
        return new() { FolderId = folderId, ConversationId = conversationId, Included = included, PinOrder = pinOrder };
    }
}
