using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;

namespace MakanApp.Application.Ai;

public sealed record AiConversationState(Guid ConversationId, IReadOnlyList<AiConversationSourcePin> SourcePins);
public sealed record AiConversationHistoryResult(string ConversationId, IReadOnlyList<AiHistoryTurn> Turns);

public interface IAiConversationStore
{
    Task<AiConversationState> OpenAsync(Guid user, Guid? conversation, Guid operation, byte[] binding, byte[] intentHash,
        Func<AiConversationState, CancellationToken, Task> reauthorize, CancellationToken ct);
    Task<(AiConversationState State, IReadOnlyList<AiHistoryTurn> History)> ReadHistoryAsync(Guid user, Guid conversation,
        Guid? excludeOperation, byte[] binding, Func<AiConversationState, CancellationToken, Task> reauthorize, CancellationToken ct);
    Task CompleteAsync(Guid user, Guid conversation, Guid operation, byte[] binding, string userText, string assistantText,
        IReadOnlyList<AiConversationSourcePin> sourcePins, Func<AiConversationState, CancellationToken, Task> reauthorize, CancellationToken ct);
}

public static class AiConversationIdentity
{
    // نشست در binding نیست تا همان مالک در نشست تازه نیز تاریخچهٔ همان بافت را ببیند.
    public static byte[] Fingerprint(AccessContext context, Guid? learner) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        context.UserId, context.WorkspaceType, context.OrganizationId, context.MembershipId,
        context.ActiveRole, context.SubjectOrganizationPersonId, context.AssistantClassId, Learner = learner }));
}

public static class AiConversationErrorCodes
{
    public const string NotFound = "AI_CONVERSATION_NOT_FOUND";
    public const string OperationConflict = MakanApp.Application.Commerce.CommerceErrorCodes.UsageConflict;
    public const string SourceBindingConflict = "AI_CONVERSATION_SOURCE_BINDING_CONFLICT";
    public const string StreamingNotSupported = "AI_SOURCE_STREAMING_NOT_SUPPORTED";
    public const string InsufficientEvidence = "AI_INSUFFICIENT_EVIDENCE";
}
