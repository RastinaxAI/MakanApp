namespace MakanApp.Domain.Assessment;

public sealed class AssignmentRecipient : IAcademicScoped
{
    private AssignmentRecipient()
    {
    }

    private AssignmentRecipient(
        Guid id,
        AcademicScope scope,
        Guid classId,
        Guid assignmentId,
        Guid assignmentVersionId,
        AcademicParticipantRef participation,
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
        AssignmentVersionId = assignmentVersionId;
        participation.EnsureScope(scope);
        EnrollmentId = participation.EnrollmentId;
        PersonalClassMembershipId = participation.PersonalClassMembershipId;
        ParticipantId = participation.Id;
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
    public Guid AssignmentVersionId { get; private set; }
    public Guid? EnrollmentId { get; private set; }
    public Guid? PersonalClassMembershipId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public AcademicParticipantRef Participation => new(ContextKind, ParticipantId);
    public DateTime CreatedAtUtc { get; private set; }

    public static AssignmentRecipient Create(
        AcademicScope scope,
        Guid classId,
        Guid assignmentId,
        Guid assignmentVersionId,
        AcademicParticipantRef participation,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            scope,
            classId,
            assignmentId,
            assignmentVersionId,
            participation,
            createdAtUtc);
}
