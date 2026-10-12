namespace MakanApp.Domain.Messaging;

public sealed class MessageUserVisibility
{
    private MessageUserVisibility()
    {
    }

    private MessageUserVisibility(Guid messageId, Guid userId, DateTime hiddenAtUtc)
    {
        MessageId = messageId;
        UserId = userId;
        HiddenAtUtc = hiddenAtUtc;
    }

    public Guid MessageId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime HiddenAtUtc { get; private set; }

    public static MessageUserVisibility Create(Guid messageId, Guid userId, DateTime hiddenAtUtc)
    {
        if (messageId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه پیام و کاربر برای پنهان‌سازی الزامی است.");
        }

        if (hiddenAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پنهان‌سازی پیام باید UTC باشد.", nameof(hiddenAtUtc));
        }

        return new MessageUserVisibility(messageId, userId, hiddenAtUtc);
    }
}
