namespace MakanApp.Domain.Messaging;

public sealed class ModerationAction
{
    private ModerationAction()
    {
    }

    public Guid Id { get; private set; }
    public Guid ModerationCaseId { get; private set; }
    public ModerationActionType Type { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static ModerationAction Create(
        Guid moderationCaseId,
        ModerationActionType type,
        Guid? actorUserId,
        DateTime createdAtUtc)
    {
        if (moderationCaseId == Guid.Empty || !Enum.IsDefined(type) ||
            actorUserId == Guid.Empty || createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("اقدام تعدیل معتبر نیست.");
        }

        return new ModerationAction
        {
            Id = Guid.NewGuid(),
            ModerationCaseId = moderationCaseId,
            Type = type,
            ActorUserId = actorUserId,
            CreatedAtUtc = createdAtUtc
        };
    }
}
