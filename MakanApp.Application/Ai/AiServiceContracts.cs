using System.Text.Json;
using System.Text.Json.Serialization;

namespace MakanApp.Application.Ai;

public interface IAiServiceRequest
{
    string RequestId { get; }
    string? CorrelationId { get; }
    AiActor Actor { get; }
}

public interface IAiServiceResponse
{
    string RequestId { get; }
    string? CorrelationId { get; }
    AiResponseMetadata Metadata { get; }
}

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
    AiAuthorizedContext Context) : IAiServiceRequest;

[JsonConverter(typeof(AiSeverityJsonConverter))]
public enum AiSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

internal sealed class AiSeverityJsonConverter : JsonConverter<AiSeverity>
{
    public override AiSeverity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !Enum.TryParse<AiSeverity>(reader.GetString(), out var severity) ||
            reader.GetString() != severity.ToString() || !Enum.IsDefined(severity))
            throw new JsonException("مقدار severity با قرارداد متنی AI سازگار نیست.");
        return severity;
    }

    public override void Write(Utf8JsonWriter writer, AiSeverity value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
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
public sealed record AiResponseMetadata(
    [property: JsonRequired] string ModelAlias,
    [property: JsonRequired] int ProcessingTimeMs);

public sealed record AiChatResponse(
    string RequestId,
    string? CorrelationId,
    string? ConversationId,
    AiMessage Answer,
    IReadOnlyCollection<AiInsight> Insights,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;
