using System.Text.Json;
using System.Text.Json.Serialization;

namespace MakanApp.Application.Ai;

public sealed record AiActor(string UserId, string Role);

public sealed record AiScope(
    string? OrganizationId,
    string? SchoolId,
    string? ClassId,
    string? StudentId);

public sealed record AiMessage(string Text, string Language = "fa");

public sealed record AiAuthorizedContext(
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Grades,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Homeworks,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Exams,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Activity)
{
    public static AiAuthorizedContext Empty { get; } = new([], [], [], [], []);
}

public sealed record AiChatRequest(
    string RequestId,
    string? CorrelationId,
    string? ConversationId,
    AiActor Actor,
    AiScope Scope,
    AiMessage Message,
    AiAuthorizedContext Context);

[JsonConverter(typeof(JsonStringEnumConverter<AiSeverity>))]
public enum AiSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public sealed record AiInsight(
    string Type,
    AiSeverity Severity,
    string? EntityType,
    string? EntityId,
    string? Title,
    string? Description,
    double? Confidence);

public sealed record AiRecommendation(string Title, string? Description);
public sealed record AiSourceReference(string Type, string Id);
public sealed record AiResponseMetadata(string ModelAlias, int ProcessingTimeMs);

public sealed record AiChatResponse(
    string RequestId,
    string? CorrelationId,
    string? ConversationId,
    AiMessage Answer,
    IReadOnlyCollection<AiInsight> Insights,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata);
