namespace MakanApp.Domain.Messaging;

public sealed class ConversationChecklistPersonalState
{
    private ConversationChecklistPersonalState() { }
    public Guid ChecklistId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationChecklistMode Mode { get; private set; }
    public bool IsChecked { get; private set; }
    public long Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ConversationChecklistPersonalState Create(ConversationChecklistItem item, Guid userId, bool isChecked)
    {
        if (item.Mode != ConversationChecklistMode.Personal || !item.IsActive || userId == Guid.Empty)
            throw new ArgumentException("وضعیت شخصی به آیتم فعال و مالک معتبر نیاز دارد.");
        return new() { ChecklistId = item.ChecklistId, ItemId = item.Id, UserId = userId,
            Mode = ConversationChecklistMode.Personal, IsChecked = isChecked, Revision = 1 };
    }

    public bool SetChecked(bool isChecked)
    {
        if (IsChecked == isChecked) return false;
        if (Revision == long.MaxValue) throw new InvalidOperationException("ظرفیت نسخه پایان یافته است.");
        IsChecked = isChecked; Revision++; return true;
    }
}
