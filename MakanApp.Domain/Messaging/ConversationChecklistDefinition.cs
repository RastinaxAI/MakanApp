namespace MakanApp.Domain.Messaging;

public enum ConversationChecklistMode { Personal = 1, Collaborative = 2 }

public sealed record ConversationChecklistItemDefinition(Guid ItemId, string Text);

public sealed class ConversationChecklistDefinition
{
    public const int MaximumTitleLength = 300;
    public const int MaximumItemLength = 150;
    public const int MaximumItems = 20;

    private ConversationChecklistDefinition(string title, ConversationChecklistMode mode, ConversationChecklistItemDefinition[] items)
    {
        Title = title; Mode = mode; Items = Array.AsReadOnly(items);
    }

    public string Title { get; }
    public ConversationChecklistMode Mode { get; }
    public IReadOnlyList<ConversationChecklistItemDefinition> Items { get; }
    public string ContentText => Title + "\n" + string.Join('\n', Items.Select(i => i.Text));

    public static ConversationChecklistDefinition Create(string title, ConversationChecklistMode mode,
        IReadOnlyCollection<ConversationChecklistItemDefinition> items)
    {
        if (!Enum.IsDefined(mode) || items is null || items.Count is < 1 or > MaximumItems ||
            items.Any(i => i is null || i.ItemId == Guid.Empty) || items.Select(i => i.ItemId).Distinct().Count() != items.Count)
            throw new ArgumentException("تعریف چک‌لیست معتبر نیست.");
        return new(Normalize(title, MaximumTitleLength), mode,
            items.Select(i => new ConversationChecklistItemDefinition(i.ItemId, Normalize(i.Text, MaximumItemLength))).ToArray());
    }

    private static string Normalize(string value, int maximum)
    {
        if (value is null || value.Length > Message.StorageMaximumTextLength) throw new ArgumentException("متن خام معتبر نیست.");
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum || normalized.Any(char.IsControl))
            throw new ArgumentException("متن چک‌لیست معتبر نیست.");
        return normalized;
    }
}
