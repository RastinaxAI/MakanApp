namespace MakanApp.Domain.Messaging;

public enum AcademicActivityDeliveryState { Pending = 1, Delivered = 2, Superseded = 3 }

public sealed class AcademicActivityDelivery
{
    private AcademicActivityDelivery() { }
    public Guid PublicationId { get; private set; }
    public ConversationType Purpose { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? MessageId { get; private set; }
    public AcademicActivityDeliveryState State { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public string? LastFailureCategory { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AcademicActivityDelivery Pending(Guid publicationId, ConversationType purpose)
    {
        if (publicationId == Guid.Empty || purpose is not ConversationType.Group and not ConversationType.Channel)
            throw new ArgumentException("مقصد فعالیت معتبر نیست.");
        return new() { PublicationId = publicationId, Purpose = purpose, State = AcademicActivityDeliveryState.Pending };
    }
    public void Attempt(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد.");
        AttemptCount = checked(AttemptCount + 1); LastAttemptAtUtc = now;
    }
    public void WaitForSpace() { LastFailureCategory = "OfficialSpaceNotReady"; }
    public void Supersede() { State = AcademicActivityDeliveryState.Superseded; LastFailureCategory = "SourceSuperseded"; }
    public void Deliver(Guid conversationId, Guid messageId)
    {
        if (conversationId == Guid.Empty || messageId == Guid.Empty || State == AcademicActivityDeliveryState.Superseded ||
            State == AcademicActivityDeliveryState.Delivered && (ConversationId != conversationId || MessageId != messageId))
            throw new InvalidOperationException("تحویل فعالیت با نتیجهٔ قبلی سازگار نیست.");
        ConversationId = conversationId; MessageId = messageId; State = AcademicActivityDeliveryState.Delivered; LastFailureCategory = null;
    }
}
