namespace MakanApp.Domain.Messaging;

public sealed class ConversationChecklistItem
{
    private ConversationChecklistItem() { }
    public Guid ChecklistId { get; private set; }
    public Guid Id { get; private set; }
    public ConversationChecklistMode Mode { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Position { get; private set; }
    public long CreatedAtRevision { get; private set; }
    public long? RetiredAtRevision { get; private set; }
    public bool IsChecked { get; private set; }
    public long StateRevision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsActive => !RetiredAtRevision.HasValue;

    public static ConversationChecklistItem Create(ConversationChecklist checklist, ConversationChecklistItemDefinition definition, int position)
    {
        if (definition.ItemId == Guid.Empty || position is < 1 or > ConversationChecklistDefinition.MaximumItems ||
            string.IsNullOrWhiteSpace(definition.Text) || definition.Text.Length > ConversationChecklistDefinition.MaximumItemLength ||
            definition.Text.Any(char.IsControl)) throw new ArgumentException("آیتم معتبر نیست.");
        return new() { ChecklistId = checklist.Id, Id = definition.ItemId, Mode = checklist.Mode, Text = definition.Text,
            Position = position, CreatedAtRevision = checklist.DefinitionRevision, StateRevision = 1 };
    }

    public void Move(int position)
    {
        if (!IsActive || position is < 1 or > ConversationChecklistDefinition.MaximumItems) throw new ArgumentException("ترتیب آیتم معتبر نیست.");
        Position = position;
    }

    public void Retire(long definitionRevision)
    {
        if (!IsActive || definitionRevision <= CreatedAtRevision) throw new ArgumentException("نسخهٔ بازنشستگی معتبر نیست.");
        RetiredAtRevision = definitionRevision;
    }

    public bool SetSharedChecked(bool isChecked)
    {
        if (Mode != ConversationChecklistMode.Collaborative || !IsActive) throw new InvalidOperationException("وضعیت مشترک قابل تغییر نیست.");
        if (IsChecked == isChecked) return false;
        if (StateRevision == long.MaxValue) throw new InvalidOperationException("ظرفیت نسخه پایان یافته است.");
        IsChecked = isChecked; StateRevision++; return true;
    }
}
