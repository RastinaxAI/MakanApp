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
}
