namespace MakanApp.Domain.Knowledge;

public sealed class EducationalSourceVersion
{
    private EducationalSourceVersion() { }
    public Guid SourceId { get; private set; }
    public int Number { get; private set; }
    public Guid FileAssetId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string NormalizedTitle { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public EducationalSourceType SourceType { get; private set; }
    public SourcePublicationStatus PublicationStatus { get; private set; }
    public SourceSafetyStatus SafetyStatus { get; private set; }
    public SourceProcessingStatus ProcessingStatus { get; private set; }
    public string? ProcessingFailureCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static string NormalizeTitle(string value) => value.Trim().Replace('ي', 'ی').Replace('ك', 'ک').Replace('\u200c', ' ');
    public static EducationalSourceVersion Create(Guid source, int number, Guid file, Guid actor, string title, string? description,
        EducationalSourceType type, DateTime now)
    {
        if (source == Guid.Empty || file == Guid.Empty || actor == Guid.Empty || number < 1 || !Enum.IsDefined(type) || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("مشخصات نسخه معتبر نیست.");
        var version = new EducationalSourceVersion { SourceId = source, Number = number, FileAssetId = file, CreatedByUserId = actor,
            SourceType = type, PublicationStatus = SourcePublicationStatus.Draft, SafetyStatus = SourceSafetyStatus.PendingValidation,
            ProcessingStatus = SourceProcessingStatus.NotRequested, CreatedAtUtc = now };
        version.UpdateDraft(title, description); return version;
    }
    public void UpdateDraft(string title, string? description)
    {
        if (PublicationStatus != SourcePublicationStatus.Draft) throw new InvalidOperationException("نسخهٔ منتشرشده immutable است.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 || description?.Trim().Length > 2000)
            throw new ArgumentException("عنوان یا توضیح منبع معتبر نیست.");
        Title = title.Trim(); NormalizedTitle = NormalizeTitle(title); Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
    public void RecordFileValidation(SourceSafetyStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentException("وضعیت اعتبارسنجی معتبر نیست.");
        // این متد مجوز عبور از ReviewRequired یا Rejected به‌صورت خودکار نیست.
        if (SafetyStatus is SourceSafetyStatus.ReviewRequired or SourceSafetyStatus.Rejected && status == SourceSafetyStatus.FileValidated)
            throw new InvalidOperationException("تصمیم Safety نیازمند فرایند مستقل مصوب است.");
        SafetyStatus = status;
    }
    public bool IsSourceReady(bool fileReady) => fileReady && SafetyStatus == SourceSafetyStatus.FileValidated;
    public void Publish(bool fileReady, DateTime now)
    {
        if (!IsSourceReady(fileReady) || now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("منبع آمادهٔ انتشار نیست.");
        if (PublicationStatus == SourcePublicationStatus.Published) return;
        PublicationStatus = SourcePublicationStatus.Published; PublishedAtUtc = now;
    }
    public void RecordProcessing(SourceProcessingStatus status, string? failureCode)
    {
        if (!Enum.IsDefined(status) || status == SourceProcessingStatus.Failed && failureCode != "SOURCE_PROCESSING_FAILED" ||
            status != SourceProcessingStatus.Failed && failureCode is not null) throw new ArgumentException("وضعیت پردازش معتبر نیست.");
        ProcessingStatus = status; ProcessingFailureCode = failureCode;
    }
}
