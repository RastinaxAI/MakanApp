namespace MakanApp.Domain.Assessment;

public sealed class GradeRelease : IAcademicScoped
{
    private GradeRelease()
    {
    }

    private GradeRelease(
        Guid id,
        AcademicScope scope,
        Guid submissionAttemptId,
        Guid evaluationRevisionId,
        Guid releasedByMembershipId,
        DateTime releasedAtUtc)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        SubmissionAttemptId = submissionAttemptId;
        EvaluationRevisionId = evaluationRevisionId;
        ReleasedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? releasedByMembershipId : null;
        ReleasedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? releasedByMembershipId : null;
        ReleasedAtUtc = releasedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid SubmissionAttemptId { get; private set; }
    public Guid EvaluationRevisionId { get; private set; }
    public Guid? ReleasedByMembershipId { get; private set; }
    public Guid? ReleasedByPersonalMembershipId { get; private set; }
    public DateTime ReleasedAtUtc { get; private set; }

    public static GradeRelease Create(
        AcademicScope scope,
        Guid submissionAttemptId,
        Guid evaluationRevisionId,
        Guid releasedByMembershipId,
        DateTime releasedAtUtc)
    {
        if (scope.Id == Guid.Empty ||
            submissionAttemptId == Guid.Empty ||
            evaluationRevisionId == Guid.Empty ||
            releasedByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های انتشار نمره الزامی هستند.");
        }

        if (releasedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان انتشار باید UTC باشد.", nameof(releasedAtUtc));
        }

        return new GradeRelease(
            Guid.NewGuid(),
            scope,
            submissionAttemptId,
            evaluationRevisionId,
            releasedByMembershipId,
            releasedAtUtc);
    }
}
