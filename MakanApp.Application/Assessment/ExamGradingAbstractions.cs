using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface IExamGradingService
{
    Task<IReadOnlyCollection<ExamGradingQueueItemDto>> GetQueueAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken);

    Task<ExamGradeDto> GetForGradingAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamGradeDto> CreateOrGetDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamGradeDto> GradeQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid attemptQuestionId,
        GradeExamQuestionCommand command,
        CancellationToken cancellationToken);

    Task<ExamGradeDto> CompleteReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CompleteExamGradeCommand command,
        CancellationToken cancellationToken);

    Task<ExamGradeReleaseDto> ReleaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        ReleaseExamGradeCommand command,
        CancellationToken cancellationToken);

    Task<ExamGradeDto> CorrectReleasedAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CorrectReleasedExamGradeCommand command,
        CancellationToken cancellationToken);

    Task<StudentExamResultDto> GetStudentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<GuardianExamResultDto> GetGuardianResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);
}

public interface IExamGradingStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);

    Task<ExamGradeAggregateRecord?> GetAggregateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamGradeAggregateRecord?> GetAggregateForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueForManagerAsync(
        AcademicScope scope,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueForTeacherAsync(
        AcademicScope scope,
        Guid membershipId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken);

    Task<bool> HasActiveTeacherAssignmentAsync(
        AcademicScope scope,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetActiveGuardianUserIdsAsync(
        AcademicScope scope,
        Guid learnerOrganizationPersonId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    void Add(ExamGradeRevision revision);
    void AddRange(IEnumerable<ExamQuestionGrade> questionGrades);
    void Add(ExamGradeRelease release);
    void SetOriginalRowVersion(ExamGradeRevision revision, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
