namespace MakanApp.Domain.Assessment;

public sealed class ExamGradeRelease : IAcademicScoped
{
    public const int RequestHashLength = 64;

    private ExamGradeRelease()
    {
    }

    private ExamGradeRelease(
        Guid id,
        AcademicScope scope,
        Guid examAttemptId,
        Guid examGradeRevisionId,
        Guid releasedByMembershipId,
        Guid clientOperationId,
        string requestHash,
        DateTime releasedAtUtc)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        ExamAttemptId = examAttemptId;
        ExamGradeRevisionId = examGradeRevisionId;
        ReleasedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? releasedByMembershipId : null;
        ReleasedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? releasedByMembershipId : null;
        ClientOperationId = clientOperationId;
        RequestHash = requestHash;
        ReleasedAtUtc = releasedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamGradeRevisionId { get; private set; }
    public Guid? ReleasedByMembershipId { get; private set; }
    public Guid? ReleasedByPersonalMembershipId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public DateTime ReleasedAtUtc { get; private set; }

    public static ExamGradeRelease Create(
        ExamGradeRevision gradeRevision,
        Guid releasedByMembershipId,
        Guid clientOperationId,
        string requestHash,
        DateTime releasedAtUtc)
    {
        if (!gradeRevision.IsReleased ||
            releasedByMembershipId == Guid.Empty ||
            clientOperationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌ها و وضعیت انتشار نمره معتبر نیستند.");
        }

        if (requestHash is null || requestHash.Length != RequestHashLength)
        {
            throw new ArgumentException("هش درخواست انتشار معتبر نیست.", nameof(requestHash));
        }

        if (releasedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان انتشار باید UTC باشد.", nameof(releasedAtUtc));
        }

        return new ExamGradeRelease(
            Guid.NewGuid(),
            gradeRevision.Scope,
            gradeRevision.ExamAttemptId,
            gradeRevision.Id,
            releasedByMembershipId,
            clientOperationId,
            requestHash,
            releasedAtUtc);
    }
}
