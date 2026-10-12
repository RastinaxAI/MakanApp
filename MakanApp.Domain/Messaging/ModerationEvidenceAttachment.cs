namespace MakanApp.Domain.Messaging;

public sealed class ModerationEvidenceAttachment
{
    private ModerationEvidenceAttachment()
    {
    }

    public Guid Id { get; private set; }
    public Guid ModerationCaseId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static ModerationEvidenceAttachment Create(
        Guid moderationCaseId,
        Guid fileAssetId,
        DateTime createdAtUtc)
    {
        if (moderationCaseId == Guid.Empty || fileAssetId == Guid.Empty ||
            createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("پیوست شاهد تعدیل معتبر نیست.");
        }

        return new ModerationEvidenceAttachment
        {
            Id = Guid.NewGuid(),
            ModerationCaseId = moderationCaseId,
            FileAssetId = fileAssetId,
            CreatedAtUtc = createdAtUtc
        };
    }
}
