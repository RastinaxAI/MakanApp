namespace MakanApp.Domain.Messaging;

public sealed class ModerationCase
{
    public const int MaximumInternalNoteLength = 1000;

    private ModerationCase()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public ModerationCaseSource Source { get; private set; }
    public Guid? AbuseReportId { get; private set; }
    public Guid? SafetyIncidentId { get; private set; }
    public ModerationQueueScope QueueScope { get; private set; }
    public ModerationPriority Priority { get; private set; }
    public ModerationCaseStatus Status { get; private set; }
    public Guid? AssignedReviewerUserId { get; private set; }
    public string? InternalNote { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ReviewStartedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime? DismissedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ModerationCase CreateForReport(
        Guid conversationId,
        Guid abuseReportId,
        ModerationQueueScope queueScope,
        ModerationPriority priority,
        DateTime createdAtUtc) =>
        Create(
            conversationId,
            ModerationCaseSource.UserReport,
            abuseReportId,
            null,
            queueScope,
            priority,
            createdAtUtc);

    public static ModerationCase CreateForSafetyIncident(
        Guid conversationId,
        Guid safetyIncidentId,
        DateTime createdAtUtc) =>
        Create(
            conversationId,
            ModerationCaseSource.AutomatedSafety,
            null,
            safetyIncidentId,
            ModerationQueueScope.PlatformSafety,
            ModerationPriority.Critical,
            createdAtUtc);

    public void StartReview(Guid reviewerUserId, DateTime startedAtUtc)
    {
        EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        if (reviewerUserId == Guid.Empty || Status != ModerationCaseStatus.Pending)
        {
            throw new InvalidOperationException("پرونده در وضعیت قابل شروع بررسی نیست.");
        }

        Status = ModerationCaseStatus.InReview;
        AssignedReviewerUserId = reviewerUserId;
        ReviewStartedAtUtc = startedAtUtc;
        UpdatedAtUtc = startedAtUtc;
    }

    public void Resolve(Guid reviewerUserId, string? internalNote, DateTime resolvedAtUtc)
    {
        EnsureReviewer(reviewerUserId);
        EnsureUtc(resolvedAtUtc, nameof(resolvedAtUtc));
        var normalizedNote = NormalizeNote(internalNote);
        InternalNote = normalizedNote;
        Status = ModerationCaseStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
        UpdatedAtUtc = resolvedAtUtc;
    }

    public void Dismiss(Guid reviewerUserId, string? internalNote, DateTime dismissedAtUtc)
    {
        EnsureReviewer(reviewerUserId);
        EnsureUtc(dismissedAtUtc, nameof(dismissedAtUtc));
        var normalizedNote = NormalizeNote(internalNote);
        InternalNote = normalizedNote;
        Status = ModerationCaseStatus.Dismissed;
        DismissedAtUtc = dismissedAtUtc;
        UpdatedAtUtc = dismissedAtUtc;
    }

    public void Escalate(Guid reviewerUserId, DateTime nowUtc)
    {
        EnsureReviewer(reviewerUserId);
        EnsureUtc(nowUtc, nameof(nowUtc));
        Priority = ModerationPriority.Critical;
        Status = ModerationCaseStatus.Pending;
        AssignedReviewerUserId = null;
        ReviewStartedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void RecordSafetyAction(Guid reviewerUserId, DateTime nowUtc)
    {
        EnsureReviewer(reviewerUserId);
        EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = nowUtc;
    }

    private static ModerationCase Create(
        Guid conversationId,
        ModerationCaseSource source,
        Guid? abuseReportId,
        Guid? safetyIncidentId,
        ModerationQueueScope queueScope,
        ModerationPriority priority,
        DateTime createdAtUtc)
    {
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        if (conversationId == Guid.Empty ||
            abuseReportId.HasValue && abuseReportId.Value == Guid.Empty ||
            safetyIncidentId.HasValue && safetyIncidentId.Value == Guid.Empty ||
            source == ModerationCaseSource.UserReport && !abuseReportId.HasValue ||
            source == ModerationCaseSource.AutomatedSafety && !safetyIncidentId.HasValue ||
            abuseReportId.HasValue == safetyIncidentId.HasValue ||
            !Enum.IsDefined(source) || !Enum.IsDefined(queueScope) || !Enum.IsDefined(priority))
        {
            throw new ArgumentException("منبع پرونده تعدیل معتبر نیست.");
        }

        return new ModerationCase
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Source = source,
            AbuseReportId = abuseReportId,
            SafetyIncidentId = safetyIncidentId,
            QueueScope = queueScope,
            Priority = priority,
            Status = ModerationCaseStatus.Pending,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    private void EnsureReviewer(Guid reviewerUserId)
    {
        if (reviewerUserId == Guid.Empty || Status != ModerationCaseStatus.InReview ||
            AssignedReviewerUserId != reviewerUserId)
        {
            throw new InvalidOperationException("فقط بازبین مسئول می‌تواند پرونده در حال بررسی را ببندد.");
        }
    }

    private static string? NormalizeNote(string? note)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized?.Length > MaximumInternalNoteLength)
        {
            throw new ArgumentException("یادداشت داخلی از طول مجاز بیشتر است.", nameof(note));
        }

        return normalized;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پرونده تعدیل باید UTC باشد.", parameterName);
        }
    }
}
