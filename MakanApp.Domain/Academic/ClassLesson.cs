namespace MakanApp.Domain.Academic;

public enum LessonStatus { Draft = 1, Published = 2 }

public sealed class ClassLesson
{
    private readonly List<ClassLessonAttachment> _attachments = [];
    private ClassLesson() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string CreationRequestHash { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public LessonStatus Status { get; private set; }
    public int RevisionNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<ClassLessonAttachment> Attachments => _attachments;

    public static ClassLesson Create(Guid organizationId, Guid classId, Guid actorId, Guid operationId, string requestHash,
        string title, string? body, int displayOrder, IReadOnlyCollection<Guid> files, DateTime now)
    {
        if (organizationId == Guid.Empty || classId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty || requestHash.Length != 64)
            throw new ArgumentException("شناسه‌ها و fingerprint ایجاد درس معتبر نیستند.");
        Utc(now);
        var lesson = new ClassLesson { Id = Guid.NewGuid(), OrganizationId = organizationId, ClassId = classId,
            CreatedByUserId = actorId, ClientOperationId = operationId, CreationRequestHash = requestHash,
            Status = LessonStatus.Draft, RevisionNumber = 1, CreatedAtUtc = now };
        lesson.SetContent(title, body, displayOrder, files);
        return lesson;
    }

    public void UpdateDraft(string title, string? body, int order, IReadOnlyCollection<Guid> files, DateTime now)
    {
        if (Status != LessonStatus.Draft) throw new InvalidOperationException("محتوای منتشرشده درجا ویرایش نمی‌شود.");
        Utc(now); SetContent(title, body, order, files); RevisionNumber++; UpdatedAtUtc = now;
    }

    public bool Publish(DateTime now)
    {
        Utc(now);
        if (Status == LessonStatus.Published) return false;
        if (string.IsNullOrWhiteSpace(Body) && _attachments.Count == 0)
            throw new ArgumentException("انتشار درس به متن یا فایل پیوست نیاز دارد.");
        Status = LessonStatus.Published; PublishedAtUtc = now; UpdatedAtUtc = now; RevisionNumber++;
        return true;
    }

    private void SetContent(string title, string? body, int order, IReadOnlyCollection<Guid> files)
    {
        var normalizedTitle = title?.Trim(); var normalizedBody = body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 200 || normalizedBody.Length > 50_000 || order is < 0 or > 100_000 ||
            files.Count > 10 || files.Any(id => id == Guid.Empty) || files.Distinct().Count() != files.Count)
            throw new ArgumentException("عنوان، متن، ترتیب یا پیوست‌های درس معتبر نیستند.");
        Title = normalizedTitle; Body = normalizedBody; DisplayOrder = order;
        // مرجع فایل‌ها تعویض می‌شود؛ خود فایل یا blob در این عملیات حذف نمی‌شود.
        var removed = _attachments.Where(x => !files.Contains(x.FileAssetId)).ToArray();
        foreach (var item in removed) _attachments.Remove(item);
        var position = 0;
        foreach (var id in files)
        {
            var existing = _attachments.SingleOrDefault(x => x.FileAssetId == id);
            if (existing is null) _attachments.Add(ClassLessonAttachment.Create(OrganizationId, Id, id, position));
            else existing.Reorder(position);
            position++;
        }
    }

    private static void Utc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان درس باید UTC باشد.");
    }
}

public sealed class ClassLessonAttachment
{
    private ClassLessonAttachment() { }
    public Guid OrganizationId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public int DisplayOrder { get; private set; }
    internal static ClassLessonAttachment Create(Guid organizationId, Guid lessonId, Guid fileAssetId, int order) =>
        new() { OrganizationId = organizationId, LessonId = lessonId, FileAssetId = fileAssetId, DisplayOrder = order };
    internal void Reorder(int order) => DisplayOrder = order;
}
