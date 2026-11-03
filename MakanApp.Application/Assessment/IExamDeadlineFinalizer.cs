namespace MakanApp.Application.Assessment;

public interface IExamDeadlineFinalizer
{
    Task<bool> FinalizeDueAttemptAsync(Guid attemptId, CancellationToken ct);
    Task FinalizeDueForExamAsync(Guid examId, Guid learnerUserId, CancellationToken ct);
}
