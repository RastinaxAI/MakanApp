using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public enum AcademicResourceKind { Organization, OrganizationClass, PersonalClass, Assignment, SubmissionAttempt, Exam, ExamAttempt }
public enum AcademicOperation { Read, AuthorAssignment, AuthorExam, Grade, Learn, OwnResult, Upload }

public sealed record AcademicAccessContext(AccessContext Account, AcademicScope Scope, Guid? ClassId,
    Guid? PersonalMembershipId, PersonalClassLocalRoles LocalRoles, PersonalClassAcademicCapabilities AcademicCapabilities,
    Guid? TargetPersonalMembershipId, AcademicResourceKind ResourceKind, Guid ResourceId, AcademicOperation Operation)
{
    public Guid? PersonalClassHostUserId { get; init; }
    public Guid UserId => Account.UserId;
    public Guid SessionId => Account.SessionId;
    public Guid? OrganizationId => Scope.OrganizationId;
    public Guid? MembershipId => Account.MembershipId;
    public OrganizationRole? ActiveRole => Account.ActiveRole;
    public Guid? SubjectOrganizationPersonId => Account.SubjectOrganizationPersonId;
    public WorkspaceType WorkspaceType => Account.WorkspaceType;
    public bool IsPersonal => Scope.Kind == AcademicClassKind.PersonalClass;
    public Guid ActorMembershipId => PersonalMembershipId ?? MembershipId ?? throw new InvalidOperationException("عضویت معتبر لازم است.");
    public bool IsLocalLearner => IsPersonal && (LocalRoles & PersonalClassLocalRoles.Learner) != 0 &&
        (!TargetPersonalMembershipId.HasValue || TargetPersonalMembershipId == PersonalMembershipId);
    public bool Has(PersonalClassAcademicCapabilities capability) =>
        IsPersonal && capability != PersonalClassAcademicCapabilities.None && (AcademicCapabilities & capability) == capability;
}

public interface IAcademicContextResolver
{
    Task<AcademicAccessContext> ResolveAsync(Guid userId, Guid sessionId, AcademicResourceKind kind, Guid resourceId,
        AcademicOperation operation, bool forUpdate, CancellationToken ct);
    Task<AcademicAccessContext> RevalidateAsync(AcademicAccessContext context, CancellationToken ct);
}
