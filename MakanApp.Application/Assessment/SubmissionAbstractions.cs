using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface ISubmissionService
{
    Task<SubmissionAttemptResult> CreateOrResumeDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> SaveDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        SaveSubmissionDraftCommand command,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> AttachFileAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        AttachSubmissionFileCommand command,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> RemoveFileAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid fileAssetId,
        string expectedRowVersion,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionAttemptResult>> GetMyAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> GetAttemptAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionAttemptResult>> GetSubmittedAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<SubmissionReceipt> FinalSubmitAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        FinalSubmitAssignmentCommand command,
        CancellationToken cancellationToken);
}

public interface ISubmissionStore
{
    Task<bool> IsFileAvailableForAttemptAsync(Guid fileId, Guid attemptId, CancellationToken ct);
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task<SubmissionEligibilityRecord?> GetEligibilityForStudentForUpdateAsync(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<SubmissionEligibilityRecord?> GetEligibilityForStudentAsync(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<Assignment?> GetAssignmentAsync(
        AcademicScope scope,
        Guid assignmentId,
        CancellationToken cancellationToken);
    Task<SubmissionAttemptRecord?> GetAttemptForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<SubmissionAttemptRecord?> GetAttemptAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<SubmissionAttempt?> GetDraftForUpdateAsync(
        AcademicScope scope,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken);
    Task<int> CountSubmittedAttemptsForUpdateAsync(
        AcademicScope scope,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetAttemptsForStudentAsync(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetSubmittedAttemptsAsync(
        AcademicScope scope,
        Guid assignmentId,
        CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(
        AcademicScope scope,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken);
    void Add(SubmissionAttempt attempt);
    void Add(SubmissionAttachment attachment);
    void Remove(SubmissionAttachment attachment);
    void SetOriginalRowVersion(SubmissionAttempt attempt, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
