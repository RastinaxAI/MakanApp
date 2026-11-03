namespace MakanApp.Domain.Academic;

public enum PersonalClassRequestStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    NeedsChanges = 4,
    Approved = 5,
    Rejected = 6,
    Cancelled = 7
}

public sealed class PersonalClassRequest
{
    public const int MaximumTitleLength = 200;
    public const int MaximumPurposeLength = 2000;
    public const int MaximumDescriptionLength = 4000;
    public const int MaximumReasonLength = 500;

    private PersonalClassRequest() { }

    public Guid Id { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string CreationPayloadHash { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SuggestedCapacity { get; private set; }
    public Guid? SuggestedManagerUserId { get; private set; }
    public Guid? SuggestedTeacherUserId { get; private set; }
    public PersonalClassRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid? FinalManagerUserId { get; private set; }
    public Guid? CreatedClassId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool CanEdit => Status is PersonalClassRequestStatus.Draft or PersonalClassRequestStatus.NeedsChanges;
    public bool CanCancel => Status is PersonalClassRequestStatus.Draft or PersonalClassRequestStatus.Submitted
        or PersonalClassRequestStatus.UnderReview or PersonalClassRequestStatus.NeedsChanges;

    public static PersonalClassRequest Create(Guid requesterUserId, Guid operationId, string payloadHash,
        string title, string purpose, string? description, int suggestedCapacity,
        Guid? suggestedManagerUserId, Guid? suggestedTeacherUserId, DateTime nowUtc)
    {
        if (requesterUserId == Guid.Empty || operationId == Guid.Empty || payloadHash is null ||
            payloadHash.Length != 64 || !payloadHash.All(Uri.IsHexDigit))
            throw new ArgumentException("شناسه و اثرانگشت درخواست تشکیل معتبر لازم است.");
        EnsureTime(nowUtc);
        var request = new PersonalClassRequest
        {
            Id = Guid.NewGuid(), RequesterUserId = requesterUserId, ClientOperationId = operationId,
            CreationPayloadHash = payloadHash, Status = PersonalClassRequestStatus.Draft,
            CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
        };
        request.Edit(title, purpose, description, suggestedCapacity, suggestedManagerUserId, suggestedTeacherUserId, nowUtc);
        return request;
    }

    public void Edit(string title, string purpose, string? description, int suggestedCapacity,
        Guid? suggestedManagerUserId, Guid? suggestedTeacherUserId, DateTime nowUtc)
    {
        if (!CanEdit) throw new InvalidOperationException("درخواست در این وضعیت قابل ویرایش نیست.");
        EnsureTime(nowUtc, CreatedAtUtc);
        var normalizedTitle = Required(title, MaximumTitleLength);
        var normalizedPurpose = Required(purpose, MaximumPurposeLength);
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > MaximumDescriptionLength || suggestedCapacity <= 0 ||
            suggestedManagerUserId == Guid.Empty || suggestedTeacherUserId == Guid.Empty)
            throw new ArgumentException("مشخصات یا ظرفیت پیشنهادی کلاس معتبر نیست.");
        Title = normalizedTitle;
        Purpose = normalizedPurpose;
        Description = normalizedDescription;
        SuggestedCapacity = suggestedCapacity;
        SuggestedManagerUserId = suggestedManagerUserId;
        SuggestedTeacherUserId = suggestedTeacherUserId;
        UpdatedAtUtc = nowUtc;
    }

    public bool Submit(DateTime nowUtc)
    {
        EnsureTime(nowUtc, UpdatedAtUtc);
        if (Status is PersonalClassRequestStatus.Submitted or PersonalClassRequestStatus.UnderReview) return false;
        if (!CanEdit) throw new InvalidOperationException("ارسال این درخواست مجاز نیست.");
        Status = PersonalClassRequestStatus.Submitted;
        SubmittedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        // نتیجهٔ آخر در تاریخچهٔ تصمیم باقی می‌ماند؛ ارسال مجدد، تصمیم تازه محسوب نمی‌شود.
        DecidedAtUtc = null;
        DecidedByUserId = null;
        DecisionReason = null;
        return true;
    }

    public bool BeginReview(DateTime nowUtc)
    {
        EnsureTime(nowUtc, UpdatedAtUtc);
        if (Status == PersonalClassRequestStatus.UnderReview) return false;
        if (Status != PersonalClassRequestStatus.Submitted)
            throw new InvalidOperationException("فقط درخواست ارسال‌شده قابل بررسی است.");
        Status = PersonalClassRequestStatus.UnderReview;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void Decide(PersonalClassRequestStatus decision, Guid reviewerUserId, string reason, DateTime nowUtc,
        Guid? classId = null, Guid? finalManagerUserId = null)
    {
        EnsureTime(nowUtc, UpdatedAtUtc);
        if (Status is not (PersonalClassRequestStatus.Submitted or PersonalClassRequestStatus.UnderReview) ||
            decision is not (PersonalClassRequestStatus.NeedsChanges or PersonalClassRequestStatus.Rejected or PersonalClassRequestStatus.Approved))
            throw new InvalidOperationException("گذار تصمیم درخواست معتبر نیست.");
        if (reviewerUserId == Guid.Empty || classId == Guid.Empty || finalManagerUserId == Guid.Empty ||
            (decision == PersonalClassRequestStatus.Approved ? !classId.HasValue || !finalManagerUserId.HasValue : classId.HasValue || finalManagerUserId.HasValue))
            throw new ArgumentException("تصمیم تأیید باید به یک کلاس و مدیر نهایی معتبر متصل باشد.");
        var normalizedReason = Required(reason, MaximumReasonLength);
        Status = decision;
        DecidedByUserId = reviewerUserId;
        DecidedAtUtc = nowUtc;
        DecisionReason = normalizedReason;
        CreatedClassId = classId;
        FinalManagerUserId = finalManagerUserId;
        UpdatedAtUtc = nowUtc;
    }

    public bool Cancel(DateTime nowUtc)
    {
        EnsureTime(nowUtc, UpdatedAtUtc);
        if (Status == PersonalClassRequestStatus.Cancelled) return false;
        if (!CanCancel) throw new InvalidOperationException("درخواست نهایی قابل لغو نیست.");
        Status = PersonalClassRequestStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    private static string Required(string value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
            throw new ArgumentException("متن الزامی درخواست معتبر نیست.");
        return normalized;
    }

    private static void EnsureTime(DateTime nowUtc, DateTime? minimum = null)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || minimum.HasValue && nowUtc < minimum.Value)
            throw new ArgumentException("زمان معتبر سرور لازم است.");
    }
}
