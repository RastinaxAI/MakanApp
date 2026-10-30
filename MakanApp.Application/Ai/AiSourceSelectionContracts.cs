using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Ai;

public enum AiSourceOperation { Chat = 1, StudyPlan = 2, HomeworkExplain = 3, TeacherActivitySuggestions = 4 }
public sealed record AiSourcePreflightCommand(AiSourceOperation Operation, IReadOnlyCollection<Guid>? SelectedSourceIds,
    Guid? ClassId = null, Guid? AssignmentId = null);
public sealed record AiSourceContext(SourceActor Actor, AiSourceOperation Operation, Guid? ClassId, Guid? AssignmentId);
public sealed record AiSourceVersionIdentity(Guid SourceId, int Number);
public sealed record AuthorizedAiSourceManifest(AiSourceVersionIdentity SourceVersionId, EducationalSourceScope Scope,
    Guid? OwnerId, string Title, string AiReadiness, string AuthorizationRevision);
public sealed record AuthorizedAiRetrievalPlan(IReadOnlyList<AuthorizedAiSourceManifest> Sources, bool Replayed)
{
    public bool RetrievalAvailable => false;
    public bool RetrievalExecuted => false;
}
public sealed record AiSourcePreflightResult(bool Authorized, bool RetrievalAvailable, int SelectedCount,
    string? ErrorCode, string CommercialStatus, IReadOnlyList<AiSourceVersionIdentity> Versions);

public interface IAiSourceAuthorizationService
{
    Task<IReadOnlyList<AuthorizedAiSourceManifest>> AuthorizeAsync(AiSourceContext context,
        IReadOnlyList<Guid> sourceIds, IReadOnlyList<AiSourceVersionIdentity>? pinned, CancellationToken ct);
}
public interface IAiSourceSelectionStore
{
    Task<IReadOnlyList<AiSourceVersionIdentity>?> FindPinnedAsync(Guid userId, Guid operationId, byte[] payloadHash, CancellationToken ct);
    Task EnsureUnselectedAsync(Guid userId, Guid operationId, CancellationToken ct);
    Task<(IReadOnlyList<AiSourceVersionIdentity> Pins, bool Replayed)> PinAsync(Guid userId, Guid operationId,
        byte[] payloadHash, IReadOnlyList<AiSourceVersionIdentity> candidates, CancellationToken ct);
}
public interface IAiSourceSelectionService
{
    Task GuardAsync(Guid userId, Guid sessionId, AiSourceOperation operation, IReadOnlyCollection<Guid>? selectedSourceIds,
        Guid? operationId, object payload, Guid? classId, Guid? assignmentId, CancellationToken ct);
    Task<AiSourcePreflightResult> PreflightAsync(Guid userId, Guid sessionId, AiSourcePreflightCommand command, CancellationToken ct);
}

public static class AiSourceSelection
{
    public const int MaximumSources = 32;
    public static Guid[] Canonicalize(IReadOnlyCollection<Guid>? ids)
    {
        if (ids is null) return [];
        if (ids.Count > MaximumSources || ids.Contains(Guid.Empty)) throw Error(AiSourceErrorCodes.Invalid);
        return ids.Distinct().OrderBy(id => id).ToArray();
    }
    // متن فقط برای محاسبهٔ اثرانگشت در حافظه است؛ در جدول یا لاگ ذخیره نمی‌شود.
    public static byte[] Hash(AiSourceContext context, object payload, IReadOnlyCollection<Guid>? ids) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            context.Operation, context.ClassId, context.AssignmentId,
            context.Actor.UserId, context.Actor.Account.WorkspaceType,
            Organization = context.Actor.Organization is { } org
                ? new { org.OrganizationId, org.MembershipId, org.ActiveRole, org.SubjectOrganizationPersonId } : null,
            Payload = payload, Sources = Canonicalize(ids)
        })));
    public static AiServiceException Error(string code) => new(code, "انتخاب منبع آموزشی با مجوز یا وضعیت جاری سازگار نیست.");
}
public static class AiSourceErrorCodes
{
    public const string Invalid = "AI_SOURCE_REQUEST_INVALID";
    public const string NotFound = "AI_SOURCE_NOT_FOUND";
    public const string NotAllowed = "AI_SOURCE_NOT_ALLOWED";
    public const string NotReady = "AI_SOURCE_NOT_READY";
    public const string ContextMismatch = "AI_SOURCE_CONTEXT_MISMATCH";
    public const string RetrievalUnavailable = "AI_SOURCE_RETRIEVAL_NOT_AVAILABLE";
    public const string OperationConflict = "AI_SOURCE_OPERATION_CONFLICT";
}
