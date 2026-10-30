namespace MakanApp.Domain.Messaging;

public enum AcademicActivityKind { Lesson = 1, Assignment = 2, Exam = 3 }

public sealed class AcademicActivityPublication
{
    private AcademicActivityPublication() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public AcademicActivityKind Kind { get; private set; }
    public Guid? LessonId { get; private set; }
    public Guid? AssignmentVersionId { get; private set; }
    public Guid? ExamVersionId { get; private set; }
    public int SourceRevision { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime? DueAtUtc { get; private set; }
    public DateTime? AvailableFromUtc { get; private set; }
    public DateTime? AvailableUntilUtc { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static AcademicActivityPublication Create(AcademicActivityKind kind, Guid organizationId, Guid classId,
        Guid sourceVersionId, int sourceRevision, Guid actorId, string title, DateTime occurredAtUtc,
        DateTime? dueAtUtc = null, DateTime? availableFromUtc = null, DateTime? availableUntilUtc = null)
    {
        if (!Enum.IsDefined(kind) || organizationId == Guid.Empty || classId == Guid.Empty || sourceVersionId == Guid.Empty || actorId == Guid.Empty ||
            sourceRevision < 1 || string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 || occurredAtUtc.Kind != DateTimeKind.Utc ||
            new[] { dueAtUtc, availableFromUtc, availableUntilUtc }.Any(x => x.HasValue && x.Value.Kind != DateTimeKind.Utc))
            throw new ArgumentException("مشخصات انتشار فعالیت معتبر نیست.");
        return new() { Id = Guid.NewGuid(), Kind = kind, OrganizationId = organizationId, ClassId = classId,
            LessonId = kind == AcademicActivityKind.Lesson ? sourceVersionId : null,
            AssignmentVersionId = kind == AcademicActivityKind.Assignment ? sourceVersionId : null,
            ExamVersionId = kind == AcademicActivityKind.Exam ? sourceVersionId : null,
            SourceRevision = sourceRevision, InitiatedByUserId = actorId, Title = title.Trim(), OccurredAtUtc = occurredAtUtc,
            DueAtUtc = dueAtUtc, AvailableFromUtc = availableFromUtc, AvailableUntilUtc = availableUntilUtc };
    }
}
