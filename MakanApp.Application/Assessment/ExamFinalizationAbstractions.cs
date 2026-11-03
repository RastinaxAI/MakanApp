using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface IExamFinalizationService
{
    Task<ExamFinalReceiptDto> FinalizeAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        FinalizeExamCommand command,
        CancellationToken cancellationToken);

    Task<ExamFinalReceiptDto> GetReceiptAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);
}

public interface IExamFinalizationStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken);

    Task<ExamAttempt?> GetOwnedAttemptForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsSessionActiveForUpdateAsync(
        Guid sessionId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExamFinalizationQuestionRecord>> GetQuestionsForFinalizationAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamFinalizationRecord?> GetOwnedFinalizationAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<ExamFinalAnswer> finalAnswers);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
