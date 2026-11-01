namespace MakanApp.Domain.Messaging;

public enum ConversationInitialTargetOutcome
{
    Added = 1,
    InvitationPending = 2,
    NotAvailable = 3
}

// رسید immutable ساخت است؛ وضعیت جاری عضویت و دعوت جداگانه خوانده می‌شود.
public sealed class ConversationInitialTarget
{
    private ConversationInitialTarget() { }

    public Guid ConversationId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public ConversationInitialTargetOutcome Outcome { get; private set; }
    public Guid? InvitationId { get; private set; }
    public DateTime? InvitationExpiresAtUtc { get; private set; }

    public static ConversationInitialTarget Create(Guid conversationId, Guid targetUserId,
        ConversationInitialTargetOutcome outcome, ConversationInvitation? invitation = null)
    {
        if (conversationId == Guid.Empty || targetUserId == Guid.Empty || !Enum.IsDefined(outcome) ||
            (outcome == ConversationInitialTargetOutcome.InvitationPending) != (invitation is not null) ||
            invitation is not null && (invitation.ConversationId != conversationId || invitation.TargetUserId != targetUserId))
            throw new ArgumentException("رسید مخاطب اولیه معتبر نیست.");
        return new() { ConversationId = conversationId, TargetUserId = targetUserId, Outcome = outcome,
            InvitationId = invitation?.Id, InvitationExpiresAtUtc = invitation?.ExpiresAtUtc };
    }
}
