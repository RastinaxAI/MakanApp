namespace MakanApp.Domain.Messaging;

public sealed class ConversationChecklist
{
    private ConversationChecklist() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid CreatorUserId { get; private set; }
    public ConversationChecklistMode Mode { get; private set; }
    public byte[] CreationHash { get; private set; } = [];
    public long DefinitionRevision { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ConversationChecklist Create(Message message, ConversationChecklistDefinition definition, byte[] creationHash)
    {
        if (message.Kind != MessageKind.Checklist || message.IsDeleted || message.ForwardedFromMessageId.HasValue ||
            message.Text != definition.Title || creationHash.Length != 32)
            throw new ArgumentException("چک‌لیست باید به پیام واقعی همین تعریف متصل باشد.");
        return new() { Id = message.Id, ConversationId = message.ConversationId, CreatorUserId = message.SenderUserId,
            Mode = definition.Mode, CreationHash = creationHash.ToArray(), DefinitionRevision = 1,
            CreatedAtUtc = message.SentAtUtc, UpdatedAtUtc = message.SentAtUtc };
    }

    // نسخهٔ تعریف با تیک شخصی یا مشترک تغییر نمی‌کند؛ فعالیت خصوصی نباید از آن قابل استنتاج باشد.
    public void AdvanceDefinition(ConversationChecklistMode mode, DateTime nowUtc)
    {
        if (mode != Mode) throw new InvalidOperationException("مالکیت وضعیت پس از انتشار قابل تغییر نیست.");
        if (nowUtc.Kind != DateTimeKind.Utc || nowUtc < UpdatedAtUtc || DefinitionRevision == long.MaxValue)
            throw new ArgumentException("زمان یا نسخهٔ تعریف معتبر نیست.");
        DefinitionRevision++; UpdatedAtUtc = nowUtc;
    }
}
