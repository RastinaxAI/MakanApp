namespace MakanApp.Domain.Messaging;

public enum ConversationInvitationStatus { Pending = 1, Accepted = 2, Declined = 3, Revoked = 4, Expired = 5 }

public sealed class ConversationInvitation
{
    private ConversationInvitation() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public ConversationInvitationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static ConversationInvitation Create(Guid conversationId, Guid inviterId, Guid targetId, DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty || inviterId == Guid.Empty || targetId == Guid.Empty || inviterId == targetId ||
            nowUtc.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= nowUtc || expiresAtUtc > nowUtc.AddDays(30))
            throw new ArgumentException("دعوت و زمان انقضای حداکثر ۳۰ روزه معتبر لازم است.");
        return new ConversationInvitation { Id = Guid.NewGuid(), ConversationId = conversationId, InviterUserId = inviterId,
            TargetUserId = targetId, Status = ConversationInvitationStatus.Pending, CreatedAtUtc = nowUtc, ExpiresAtUtc = expiresAtUtc };
    }
    public void Complete(ConversationInvitationStatus status, DateTime nowUtc)
    {
        if (Status == status) return;
        if (Status != ConversationInvitationStatus.Pending || status == ConversationInvitationStatus.Pending || !Enum.IsDefined(status) ||
            nowUtc.Kind != DateTimeKind.Utc || status == ConversationInvitationStatus.Accepted && nowUtc >= ExpiresAtUtc)
            throw new InvalidOperationException("وضعیت دعوت اجازه این انتقال را نمی‌دهد.");
        Status = status;
        CompletedAtUtc = nowUtc;
    }
}
