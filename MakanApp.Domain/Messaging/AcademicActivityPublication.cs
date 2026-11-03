using MakanApp.Domain.Assessment;

namespace MakanApp.Domain.Messaging;

public enum AcademicActivityKind { Lesson = 1, Assignment = 2, Exam = 3, Session = 4 }
public enum SessionActivityAction { Scheduled = 1, Rescheduled = 2, Cancelled = 3, Completed = 4 }

public sealed class AcademicActivityPublication : IAcademicScoped
{
    private AcademicActivityPublication() { }
    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public AcademicClassRef ClassRef => new(Scope, ClassId);
    public Guid ClassId { get; private set; }
    public AcademicActivityKind Kind { get; private set; }
    public Guid? LessonId { get; private set; }
    public Guid? AssignmentVersionId { get; private set; }
    public Guid? ExamVersionId { get; private set; }
    public Guid? SessionId { get; private set; }
    public SessionActivityAction? SessionAction { get; private set; }
    public int SourceRevision { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime? DueAtUtc { get; private set; }
    public DateTime? AvailableFromUtc { get; private set; }
    public DateTime? AvailableUntilUtc { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static AcademicActivityPublication Create(AcademicActivityKind kind, AcademicScope scope, Guid classId,
        Guid sourceVersionId, int sourceRevision, Guid actorId, string title, DateTime occurredAtUtc,
        DateTime? dueAtUtc = null, DateTime? availableFromUtc = null, DateTime? availableUntilUtc = null,
        SessionActivityAction? sessionAction = null)
    {
        if (!Enum.IsDefined(kind) || scope.Id == Guid.Empty || classId == Guid.Empty || sourceVersionId == Guid.Empty || actorId == Guid.Empty ||
            sourceRevision < 1 || string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 || occurredAtUtc.Kind != DateTimeKind.Utc ||
            new[] { dueAtUtc, availableFromUtc, availableUntilUtc }.Any(x => x.HasValue && x.Value.Kind != DateTimeKind.Utc) ||
            (kind == AcademicActivityKind.Session) != sessionAction.HasValue || sessionAction.HasValue && !Enum.IsDefined(sessionAction.Value))
            throw new ArgumentException("مشخصات انتشار فعالیت معتبر نیست.");
        scope.EnsureClass(classId);
        if (scope.Kind == AcademicClassKind.PersonalClass && kind is not (AcademicActivityKind.Assignment or AcademicActivityKind.Exam))
            throw new ArgumentException("این نوع فعالیت برای کلاس مستقل در این مرحله تعریف نشده است.");
        return new() { Id = Guid.NewGuid(), Kind = kind, OrganizationId = scope.OrganizationId, PersonalClassId = scope.PersonalClassId, ContextKind = scope.Kind, ContextId = scope.Id, ClassId = classId,
            LessonId = kind == AcademicActivityKind.Lesson ? sourceVersionId : null,
            AssignmentVersionId = kind == AcademicActivityKind.Assignment ? sourceVersionId : null,
            ExamVersionId = kind == AcademicActivityKind.Exam ? sourceVersionId : null,
            SessionId = kind == AcademicActivityKind.Session ? sourceVersionId : null, SessionAction = sessionAction,
            SourceRevision = sourceRevision, InitiatedByUserId = actorId, Title = title.Trim(), OccurredAtUtc = occurredAtUtc,
            DueAtUtc = dueAtUtc, AvailableFromUtc = availableFromUtc, AvailableUntilUtc = availableUntilUtc };
    }
}
