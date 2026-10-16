namespace MakanApp.Domain.Messaging;

public enum ConversationTopicStatus { Open = 1, Closed = 2 }

public sealed class ConversationTopic
{
    private ConversationTopic() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public ConversationTopicStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static ConversationTopic Create(Guid id, Conversation conversation, Guid creatorId, string title, DateTime nowUtc)
    {
        if (id == Guid.Empty || creatorId == Guid.Empty || conversation.Type != ConversationType.Group || conversation.Status != ConversationStatus.Active || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("موضوع فقط در گروه فعال و با زمان UTC ساخته می‌شود.");
        return new() { Id = id, ConversationId = conversation.Id, CreatedByUserId = creatorId, Title = Conversation.NormalizeTitle(title),
            Status = ConversationTopicStatus.Open, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc, Revision = 1 };
    }
    public void Update(string title, ConversationTopicStatus status, DateTime nowUtc)
    {
        if (!Enum.IsDefined(status) || nowUtc.Kind != DateTimeKind.Utc || nowUtc < UpdatedAtUtc) throw new ArgumentException("وضعیت یا زمان موضوع معتبر نیست.");
        Title = Conversation.NormalizeTitle(title); Status = status; UpdatedAtUtc = nowUtc; Revision = checked(Revision + 1);
    }
}
