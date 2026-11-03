using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Application.Assessment;

public interface IAssignmentService
{
    Task<AssignmentResult> CreateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CreateAssignmentDraftCommand command,
        CancellationToken cancellationToken, AcademicClassKind classKind = AcademicClassKind.OrganizationClass);

    Task<AssignmentResult> UpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        UpdateAssignmentDraftCommand command,
        CancellationToken cancellationToken);

    Task<PublishAssignmentResult> PublishAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        PublishAssignmentCommand command,
        CancellationToken cancellationToken);

    Task<AssignmentResult> GetAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AssignmentResult>> GetForClassAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken, AcademicClassKind classKind = AcademicClassKind.OrganizationClass);
}

public interface IAssessmentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IAssignmentStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task<AcademicClassRecord?> GetClassAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<AcademicClassRecord?> GetClassForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(AcademicScope scope, Guid classId, Guid membershipId, CancellationToken cancellationToken);
    Task<AssignmentWithVersionRecord?> GetAssignmentAsync(AcademicScope scope, Guid assignmentId, CancellationToken cancellationToken);
    Task<AssignmentWithVersionRecord?> GetAssignmentForUpdateAsync(AcademicScope scope, Guid assignmentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetActiveParticipantIdsForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetActiveLearnerUserIdsForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<bool> IsRecipientForStudentAsync(AcademicScope scope, Guid assignmentVersionId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsRecipientForLearnerAsync(AcademicScope scope, Guid assignmentVersionId, Guid learnerOrganizationPersonId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForManagerAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForTeacherAsync(AcademicScope scope, Guid classId, Guid membershipId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForStudentAsync(AcademicScope scope, Guid classId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForLearnerAsync(AcademicScope scope, Guid classId, Guid learnerOrganizationPersonId, CancellationToken cancellationToken);
    void SetOriginalRowVersion(Assignment assignment, byte[] rowVersion);
    void SetOriginalRowVersion(AssignmentVersion version, byte[] rowVersion);
    void Add(Assignment assignment);
    void Add(AssignmentVersion version);
    void AddRange(IEnumerable<AssignmentRecipient> recipients);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> IsFileAvailableForAssignmentAsync(Guid fileId, Guid assignmentId, CancellationToken ct);
    Task<int> CountRecipientsAsync(Guid versionId, CancellationToken ct);
}
