using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Domain.Intelligence;

namespace MakanApp.Application.Ai;

public enum AiChatMode { General, Auto, SourcesOnly }
public enum AiSourceIntent { General, SourceFocused }
[JsonConverter(typeof(AiAssistantProficiencyConverter))]
public enum AiAssistantProficiency { Beginner, Intermediate, Advanced }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AiAssistantProfile(
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] int? GradeLevel = null,
    [property: JsonConverter(typeof(AiAssistantProficiencyConverter))] AiAssistantProficiency? Proficiency = null,
    IReadOnlyList<string>? Goals = null)
{
    public AiAssistantProfile Validate()
    {
        if (GradeLevel is < 1 or > 12 || Proficiency.HasValue && !Enum.IsDefined(Proficiency.Value) ||
            Goals?.Count > 8 || Goals?.Any(goal => string.IsNullOrWhiteSpace(goal) || CharacterCount(goal) > 80) == true)
            throw new AiServiceException(AiServiceErrorCodes.InvalidRequest, "پروفایل اختیاری Assistant معتبر نیست.");
        // DTO عمومی null را می‌پذیرد؛ wire رسمی goals را فقط به صورت آرایه می‌فرستد.
        return this with { Goals = Goals?.ToArray() ?? [] };
    }

    private static int CharacterCount(string value)
    {
        try { return AiConversationTurn.CharacterCount(value); }
        catch (ArgumentException) { throw new AiServiceException(AiServiceErrorCodes.InvalidRequest, "متن پروفایل معتبر نیست."); }
    }
}

internal sealed class AiAssistantProficiencyConverter : JsonConverter<AiAssistantProficiency>
{
    public override AiAssistantProficiency Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !Enum.TryParse<AiAssistantProficiency>(reader.GetString(), out var value) ||
            !Enum.IsDefined(value) || reader.GetString() != value.ToString()) throw new JsonException("سطح مهارت معتبر نیست.");
        return value;
    }
    public override void Write(Utf8JsonWriter writer, AiAssistantProficiency value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

public sealed record AiHistoryTurn(string Role, string Text);
public sealed record AiCompletedHistoryPair(long Sequence, string UserText, string AssistantText);

public static class AiHistoryWindow
{
    public const int MaximumTurns = 20;
    public const int MaximumTurnCharacters = 4_000;
    public const int MaximumTotalCharacters = 12_000;

    public static IReadOnlyList<AiHistoryTurn> Select(IEnumerable<AiCompletedHistoryPair> newestFirst)
    {
        var pairs = new List<AiCompletedHistoryPair>(); var total = 0;
        foreach (var pair in newestFirst)
        {
            var user = AiConversationTurn.CharacterCount(pair.UserText);
            var assistant = AiConversationTurn.CharacterCount(pair.AssistantText);
            if (user > MaximumTurnCharacters || assistant > MaximumTurnCharacters) continue;
            if (pairs.Count == MaximumTurns / 2 || total + user + assistant > MaximumTotalCharacters) break;
            pairs.Add(pair); total += user + assistant;
        }
        return pairs.OrderBy(p => p.Sequence).SelectMany(p => new[] { new AiHistoryTurn("User", p.UserText), new AiHistoryTurn("Assistant", p.AssistantText) }).ToArray();
    }
}

public enum AiChatProvenanceKind { General, GeneralFallback, KnowledgeRag }
public sealed record AiChatProvenance(AiChatProvenanceKind Kind, string? Reason = null);
