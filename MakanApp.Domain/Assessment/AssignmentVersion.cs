namespace MakanApp.Domain.Assessment;

public sealed class AssignmentVersion : IAcademicScoped
{
    private readonly List<AssignmentVersionAttachment> _attachments = [];
    public IReadOnlyCollection<AssignmentVersionAttachment> Attachments => _attachments.AsReadOnly();
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 10_000;
    public const decimal MaximumScoreValue = 9_999_999.99m;

    private AssignmentVersion()
    {
    }

    private AssignmentVersion(
        Guid id,
        AcademicScope scope,
        Guid classId,
        Guid assignmentId,
        int versionNumber,
        string title,
        string description,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        int maxAttempts,
        decimal maxScore,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        scope.EnsureClass(classId);
        ClassId = classId;
        AssignmentId = assignmentId;
        VersionNumber = versionNumber;
        Title = title;
        Description = description;
        DueAtUtc = dueAtUtc;
        AllowLateSubmission = allowLateSubmission;
        MaxAttempts = maxAttempts;
        MaxScore = maxScore;
        CreatedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? createdByMembershipId : null;
        CreatedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? createdByMembershipId : null;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ClassId { get; private set; }
    public AcademicClassRef ClassRef => new(Scope, ClassId);
    public Guid AssignmentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime DueAtUtc { get; private set; }
    public bool AllowLateSubmission { get; private set; }
    public int MaxAttempts { get; private set; }
    public decimal MaxScore { get; private set; }
    public Guid? CreatedByMembershipId { get; private set; }
    public Guid? CreatedByPersonalMembershipId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsPublished => PublishedAtUtc.HasValue;

    public static AssignmentVersion CreateDraft(
        AcademicScope scope,
        Guid classId,
        Guid assignmentId,
        int versionNumber,
        string title,
        string description,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        int maxAttempts,
        decimal maxScore,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Validate(title, description, dueAtUtc, maxAttempts, maxScore, createdAtUtc);
        return new AssignmentVersion(
            Guid.NewGuid(),
            scope,
            classId,
            assignmentId,
            versionNumber,
            title.Trim(),
            description.Trim(),
            dueAtUtc,
            allowLateSubmission,
            maxAttempts,
            maxScore,
            createdByMembershipId,
            createdAtUtc);
    }

    public void UpdateDraft(
        string title,
        string description,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        int maxAttempts,
        decimal maxScore,
        DateTime nowUtc)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("نسخه منتشرشده تکلیف قابل ویرایش نیست.");
        }

        Validate(title, description, dueAtUtc, maxAttempts, maxScore, nowUtc);
        Title = title.Trim();
        Description = description.Trim();
        DueAtUtc = dueAtUtc;
        AllowLateSubmission = allowLateSubmission;
        MaxAttempts = maxAttempts;
        MaxScore = maxScore;
    }

    public void Publish(DateTime publishedAtUtc)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("نسخه تکلیف قبلاً منتشر شده است.");
        }

        if (DueAtUtc <= publishedAtUtc)
        {
            throw new InvalidOperationException("مهلت تکلیف هنگام انتشار باید در آینده باشد.");
        }

        PublishedAtUtc = publishedAtUtc;
    }

    public void ReplaceAttachments(IReadOnlyCollection<Guid> fileIds, DateTime now)
    {
        if (IsPublished) throw new InvalidOperationException("پیوست نسخهٔ منتشرشده ثابت است.");
        if (fileIds.Count > 20 || fileIds.Contains(Guid.Empty) || fileIds.Distinct().Count() != fileIds.Count)
            throw new ArgumentException("فهرست پیوست‌ها معتبر نیست.", nameof(fileIds));
        _attachments.RemoveAll(a => !fileIds.Contains(a.FileAssetId));
        foreach (var id in fileIds.Where(id => _attachments.All(a => a.FileAssetId != id)))
            _attachments.Add(new AssignmentVersionAttachment(this, id, now));
    }

    private static void Validate(
        string title,
        string description,
        DateTime dueAtUtc,
        int maxAttempts,
        decimal maxScore,
        DateTime nowUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException("عنوان تکلیف معتبر نیست.", nameof(title));
        }

        var normalizedDescription = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription) ||
            normalizedDescription.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException("توضیح تکلیف معتبر نیست.", nameof(description));
        }

        if (dueAtUtc.Kind != DateTimeKind.Utc || nowUtc.Kind != DateTimeKind.Utc || dueAtUtc <= nowUtc)
        {
            throw new ArgumentException("مهلت تکلیف باید یک زمان UTC در آینده باشد.", nameof(dueAtUtc));
        }

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "تعداد تلاش مجاز باید بیشتر از صفر باشد.");
        }

        if (maxScore <= 0 ||
            maxScore > MaximumScoreValue ||
            decimal.Round(maxScore, 2) != maxScore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxScore),
                "سقف نمره باید مثبت، حداکثر ۹٬۹۹۹٬۹۹۹٫۹۹ و دارای حداکثر دو رقم اعشار باشد.");
        }
    }
}
