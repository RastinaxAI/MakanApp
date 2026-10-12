namespace MakanApp.Domain.Notification;

public sealed class Notification
{
    public const int MaximumTitleLength = 200;
    public const int MaximumBodyLength = 500;

    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid sourceEventId,
        Guid recipientUserId,
        Guid? organizationId,
        NotificationCategory category,
        NotificationType type,
        string title,
        string body,
        NotificationResourceType resourceType,
        Guid resourceId,
        DateTime createdAtUtc)
    {
        Id = id;
        SourceEventId = sourceEventId;
        RecipientUserId = recipientUserId;
        OrganizationId = organizationId;
        Category = category;
        Type = type;
        Title = title;
        Body = body;
        ResourceType = resourceType;
        ResourceId = resourceId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid SourceEventId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationResourceType ResourceType { get; private set; }
    public Guid ResourceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Notification Create(
        Guid sourceEventId,
        Guid recipientUserId,
        Guid? organizationId,
        NotificationCategory category,
        NotificationType type,
        string title,
        string body,
        NotificationResourceType resourceType,
        Guid resourceId,
        DateTime createdAtUtc)
    {
        EnsureIdentifier(sourceEventId, nameof(sourceEventId));
        EnsureIdentifier(recipientUserId, nameof(recipientUserId));
        EnsureIdentifier(resourceId, nameof(resourceId));
        if (!Enum.IsDefined(category) || !Enum.IsDefined(type) || !Enum.IsDefined(resourceType))
        {
            throw new ArgumentOutOfRangeException(nameof(category), "نوع اعلان معتبر نیست.");
        }
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه سازمان در صورت ارائه باید معتبر باشد.", nameof(organizationId));
        }

        title = ValidateText(title, MaximumTitleLength, nameof(title));
        body = ValidateText(body, MaximumBodyLength, nameof(body));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        return new Notification(
            Guid.NewGuid(), sourceEventId, recipientUserId, organizationId,
            category, type, title, body, resourceType, resourceId, createdAtUtc);
    }

    public void MarkRead(DateTime readAtUtc)
    {
        EnsureUtc(readAtUtc, nameof(readAtUtc));
        if (readAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(readAtUtc), "زمان خواندن نمی‌تواند پیش از ایجاد اعلان باشد.");
        }

        if (!ReadAtUtc.HasValue)
        {
            ReadAtUtc = readAtUtc;
        }
    }

    private static string ValidateText(string value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException("متن اعلان نامعتبر است.", parameterName);
        }

        return normalized;
    }

    private static void EnsureIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("شناسه اعلان الزامی است.", parameterName);
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان اعلان باید UTC باشد.", parameterName);
        }
    }
}
