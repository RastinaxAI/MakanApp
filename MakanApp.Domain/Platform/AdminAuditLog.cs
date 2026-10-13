namespace MakanApp.Domain.Platform;

public enum AdminAuditResult
{
    Succeeded = 1,
    Denied = 2,
    Failed = 3
}

public sealed class AdminAuditLog
{
    public const int MaximumActionLength = 150;
    public const int MaximumTargetTypeLength = 100;
    public const int MaximumTargetIdLength = 100;
    public const int MaximumReasonLength = 500;
    public const int MaximumCorrelationIdLength = 100;
    public const int MaximumSummaryLength = 2000;

    private AdminAuditLog()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? ActorPersonId { get; private set; }
    public Guid? ActorSessionId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public Guid? OrganizationId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public AdminAuditResult Result { get; private set; }
    public string? BeforeSummary { get; private set; }
    public string? AfterSummary { get; private set; }

    public static AdminAuditLog Create(
        Guid? actorUserId,
        Guid? actorPersonId,
        Guid? actorSessionId,
        string action,
        string targetType,
        string targetId,
        Guid? organizationId,
        string reason,
        DateTime occurredAtUtc,
        string correlationId,
        AdminAuditResult result,
        string? beforeSummary,
        string? afterSummary)
    {
        if (actorUserId == Guid.Empty || actorPersonId == Guid.Empty || actorSessionId == Guid.Empty ||
            organizationId == Guid.Empty || !Enum.IsDefined(result))
        {
            throw new ArgumentException("شناسه یا نتیجه رویداد ممیزی معتبر نیست.");
        }

        EnsureRequired(action, MaximumActionLength, nameof(action));
        EnsureRequired(targetType, MaximumTargetTypeLength, nameof(targetType));
        EnsureRequired(targetId, MaximumTargetIdLength, nameof(targetId));
        EnsureRequired(reason, MaximumReasonLength, nameof(reason));
        EnsureRequired(correlationId, MaximumCorrelationIdLength, nameof(correlationId));
        EnsureOptional(beforeSummary, MaximumSummaryLength, nameof(beforeSummary));
        EnsureOptional(afterSummary, MaximumSummaryLength, nameof(afterSummary));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان ممیزی مدیریتی باید UTC باشد.", nameof(occurredAtUtc));
        }

        return new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            ActorPersonId = actorPersonId,
            ActorSessionId = actorSessionId,
            Action = action.Trim(),
            TargetType = targetType.Trim(),
            TargetId = targetId.Trim(),
            OrganizationId = organizationId,
            Reason = reason.Trim(),
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId.Trim(),
            Result = result,
            BeforeSummary = NormalizeOptional(beforeSummary),
            AfterSummary = NormalizeOptional(afterSummary)
        };
    }

    private static void EnsureRequired(string value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException("مقدار الزامی ممیزی مدیریتی معتبر نیست.", parameterName);
        }
    }

    private static void EnsureOptional(string? value, int maximumLength, string parameterName)
    {
        if (value?.Trim().Length > maximumLength)
        {
            throw new ArgumentException("خلاصه ممیزی مدیریتی بیش از حد مجاز است.", parameterName);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
