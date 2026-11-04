namespace MakanApp.Domain.Academic;

public sealed class Class
{
    private Class()
    {
    }

    private Class(
        Guid id,
        Guid organizationId,
        Guid academicPeriodId,
        Guid courseId,
        string title,
        int capacity,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        AcademicPeriodId = academicPeriodId;
        CourseId = courseId;
        Title = title;
        Capacity = capacity;
        Status = ClassStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AcademicPeriodId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public ClassStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public Guid? CreatedByUserId { get; private set; }
    public Guid? ClientOperationId { get; private set; }
    public string? CreationRequestHash { get; private set; }

    public bool IsActive => Status == ClassStatus.Active && !EndedAtUtc.HasValue;

    public void RecordCreationIntent(Guid actorId, Guid operationId, string requestHash)
    {
        if (CreatedByUserId.HasValue || actorId == Guid.Empty || operationId == Guid.Empty ||
            requestHash.Length != 64 || !requestHash.All(Uri.IsHexDigit))
            throw new ArgumentException("قصد ایجاد کلاس معتبر نیست یا قبلاً ثبت شده است.");
        CreatedByUserId = actorId; ClientOperationId = operationId; CreationRequestHash = requestHash;
    }

    public void UpdateSettings(string title, int capacity, int activeLearnerCount)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 200 || capacity <= 0 || activeLearnerCount < 0)
            throw new ArgumentException("عنوان یا ظرفیت کلاس معتبر نیست.");
        if (Status is ClassStatus.Completed or ClassStatus.Archived || capacity < activeLearnerCount)
            throw new InvalidOperationException("تغییر تنظیمات کلاس پایان‌یافته یا ظرفیت کمتر از فراگیران فعال مجاز نیست.");
        Title = normalized; Capacity = capacity;
    }

    public static Class CreateDraft(
        Guid organizationId,
        Guid academicPeriodId,
        Guid courseId,
        string title,
        int capacity,
        DateTime createdAtUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 200)
        {
            throw new ArgumentException("عنوان کلاس معتبر نیست.", nameof(title));
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "ظرفیت کلاس باید مثبت باشد.");
        }

        return new Class(
            Guid.NewGuid(),
            organizationId,
            academicPeriodId,
            courseId,
            normalizedTitle,
            capacity,
            createdAtUtc);
    }

    public void Activate(DateTime activatedAtUtc)
    {
        if (Status != ClassStatus.Draft)
        {
            throw new InvalidOperationException("فقط کلاس پیش‌نویس قابل فعال‌سازی است.");
        }

        Status = ClassStatus.Active;
        ActivatedAtUtc = activatedAtUtc;
    }

    public void Complete(DateTime completedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("فقط کلاس فعال قابل تکمیل است.");
        }

        Status = ClassStatus.Completed;
        EndedAtUtc = completedAtUtc;
    }

    public void Archive(DateTime archivedAtUtc)
    {
        if (Status == ClassStatus.Archived)
        {
            return;
        }

        Status = ClassStatus.Archived;
        EndedAtUtc ??= archivedAtUtc;
    }
}
