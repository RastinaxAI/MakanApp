using MakanApp.Application.Organization;

namespace MakanApp.Application.Ai;

public interface IAiChatService
{
    Task<AiChatResult> ChatAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        SendAiChatCommand command,
        CancellationToken cancellationToken);
}

public interface IAiScopeStore
{
    Task<Guid?> GetActiveLearnerOrganizationPersonIdAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<AiSelectedHomework?> GetPublishedHomeworkAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<AiSelectedExam?> GetPublishedExamAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        Guid examId,
        CancellationToken cancellationToken);

    Task<AiLearnerClassScope?> GetActiveLearnerClassAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        Guid classId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AiLearnerClassScope>> GetActiveLearnerClassesAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        int maximumCount,
        CancellationToken cancellationToken);
}
