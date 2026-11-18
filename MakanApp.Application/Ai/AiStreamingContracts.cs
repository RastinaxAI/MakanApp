using System.Text.Json.Serialization;

namespace MakanApp.Application.Ai;

public abstract record AiChatStreamEvent([property: JsonIgnore] string EventName);
public sealed record AiChatStreamStart(
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] string? CorrelationId,
    [property: JsonRequired] string? ConversationId,
    [property: JsonRequired] string ModelAlias) : AiChatStreamEvent("start")
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiChatProvenance? Provenance { get; init; }
}
public sealed record AiChatStreamDelta(
    [property: JsonRequired] int Sequence,
    [property: JsonRequired] string Text) : AiChatStreamEvent("delta");
public sealed record AiChatStreamDone(
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] string? CorrelationId,
    [property: JsonRequired] string? ConversationId,
    [property: JsonRequired] AiResponseMetadata Metadata) : AiChatStreamEvent("done");
public sealed record AiStreamErrorDetail(
    [property: JsonRequired] string Code,
    [property: JsonRequired] string Message,
    [property: JsonRequired] string? RequestId);
public sealed record AiChatStreamError([property: JsonRequired] AiStreamErrorDetail Error) : AiChatStreamEvent("error");
