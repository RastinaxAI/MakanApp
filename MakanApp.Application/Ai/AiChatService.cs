using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public sealed class AiChatService(
    IAccessContextResolver accessContextResolver,
    IAiScopeStore scopeStore,
    IAiServiceClient client,
    IAiSourceSelectionService sourceSelection) : IAiChatService
{
    private const int MaximumMessageLength = 4_000;

    public async Task<AiChatResult> ChatAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        SendAiChatCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command, correlationId);
        await sourceSelection.GuardAsync(userId, sessionId, AiSourceOperation.Chat, command.SelectedSourceIds,
            command.OperationId, new { Message = command.Message.Trim(), command.ConversationId }, null, null, cancellationToken);
        var context = await accessContextResolver.ResolveAsync(
            userId,
            sessionId,
            cancellationToken);
        EnsureOrganizationRole(context);

        Guid? learnerId = context.SubjectOrganizationPersonId;
        if (context.ActiveRole == OrganizationRole.Student)
        {
            learnerId = await scopeStore.GetActiveLearnerOrganizationPersonIdAsync(
                context.OrganizationId!.Value,
                context.UserId,
                cancellationToken);
            if (!learnerId.HasValue)
            {
                throw ContextNotAllowed();
            }
        }

        var request = new AiChatRequest(
            Guid.NewGuid().ToString("N"),
            correlationId,
            command.ConversationId?.ToString("N"),
            new AiActor(context.UserId.ToString("D"), context.ActiveRole!.Value.ToString()),
            new AiScope(
                context.OrganizationId!.Value.ToString("D"),
                context.OrganizationId.Value.ToString("D"),
                null,
                learnerId?.ToString("D")),
            new AiMessage(command.Message.Trim()),
            AiAuthorizedContext.Empty);

        var response = await client.ChatAsync(request, cancellationToken);
        return new AiChatResult(
            response.RequestId,
            response.CorrelationId,
            response.ConversationId,
            response.Answer,
            response.Insights,
            response.Risks,
            response.Recommendations,
            response.Sources);
    }

    private static void Validate(SendAiChatCommand command, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(command.Message) ||
            command.Message.Trim().Length > MaximumMessageLength ||
            string.IsNullOrWhiteSpace(correlationId) ||
            correlationId.Length > 128)
        {
            throw new AiServiceException(
                AiServiceErrorCodes.InvalidRequest,
                "درخواست گفت‌وگوی هوش مصنوعی معتبر نیست.");
        }
    }

    private static void EnsureOrganizationRole(AccessContext context)
    {
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole is not (
                OrganizationRole.Student or
                OrganizationRole.Parent or
                OrganizationRole.Teacher or
                OrganizationRole.Manager))
        {
            throw ContextNotAllowed();
        }
    }

    private static AiServiceException ContextNotAllowed() => new(
        AiServiceErrorCodes.ContextNotAllowed,
        "بافت سازمانی فعلی برای استفاده از هوش مصنوعی مجاز نیست.");
}
