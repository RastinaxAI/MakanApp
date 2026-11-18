using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Domain.Intelligence;

namespace MakanApp.Infrastructure.Ai;

internal static class AiSseReader
{
    // یک کاراکتر Unicode در JSON می‌تواند دوازده کاراکتر escape داشته باشد؛ کل frame نیز محدود است.
    private const int MaximumFrameCharacters = AiConversationTurn.MaximumAssistantCharacters * 12 + 4096;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict, PropertyNameCaseInsensitive = false };

    public static async IAsyncEnumerable<AiChatStreamEvent> ReadAsync(Stream stream, AiChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var started = false; var lastSequence = 0; var terminal = false; var characters = 0;
        var meaningful = false; AiChatStreamDone? done = null;
        await foreach (var frame in Frames(stream, ct))
        {
            if (terminal) throw Invalid();
            using var document = JsonDocument.Parse(frame.Data);
            UniqueProperties(document.RootElement);
            switch (frame.Name)
            {
                case "start":
                    if (started) throw Invalid();
                    if (document.RootElement.TryGetProperty("provenance", out _)) throw Invalid();
                    var start = JsonSerializer.Deserialize<AiChatStreamStart>(frame.Data, Json) ?? throw Invalid();
                    if (start.RequestId != request.RequestId || start.CorrelationId != request.CorrelationId ||
                        start.ConversationId != request.ConversationId || start.ModelAlias != "education-fast") throw Invalid();
                    started = true;
                    yield return start;
                    break;
                case "delta":
                    if (!started) throw Invalid();
                    var delta = JsonSerializer.Deserialize<AiChatStreamDelta>(frame.Data, Json) ?? throw Invalid();
                    if (delta.Sequence != lastSequence + 1 || string.IsNullOrEmpty(delta.Text)) throw Invalid();
                    characters += AiConversationTurn.CharacterCount(delta.Text);
                    if (characters > AiConversationTurn.MaximumAssistantCharacters) throw Invalid();
                    meaningful |= !string.IsNullOrWhiteSpace(delta.Text);
                    lastSequence = delta.Sequence;
                    yield return delta;
                    break;
                case "done":
                    if (!started || !meaningful || lastSequence == 0) throw Invalid();
                    done = JsonSerializer.Deserialize<AiChatStreamDone>(frame.Data, Json) ?? throw Invalid();
                    if (done.RequestId != request.RequestId || done.CorrelationId != request.CorrelationId ||
                        done.ConversationId != request.ConversationId || done.Metadata?.ModelAlias != "education-fast" ||
                        done.Metadata.ProcessingTimeMs < 0) throw Invalid();
                    terminal = true;
                    // done تا EOF معتبر نگه داشته می‌شود تا event پس از terminal موفق گزارش نشود.
                    break;
                case "error":
                    if (!started) throw Invalid();
                    var error = JsonSerializer.Deserialize<AiChatStreamError>(frame.Data, Json) ?? throw Invalid();
                    if (error.Error is null || error.Error.RequestId != request.RequestId) throw Invalid();
                    throw Map(error.Error.Code);
                default: throw Invalid();
            }
        }
        if (done is null) throw Invalid();
        yield return done;
    }

    private static async IAsyncEnumerable<(string Name, string Data)> Frames(Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var bytes = new byte[2048]; var chars = new char[4096];
        var decoder = new UTF8Encoding(false, true).GetDecoder();
        var line = new StringBuilder(); var data = new StringBuilder(); string? name = null;
        var previousCr = false; var frameLength = 0;
        while (true)
        {
            var count = await stream.ReadAsync(bytes, ct);
            var decoded = decoder.GetChars(bytes, 0, count, chars, 0, count == 0);
            for (var index = 0; index < decoded; index++)
            {
                var character = chars[index];
                if (character == '\n' && previousCr) { previousCr = false; continue; }
                previousCr = character == '\r';
                if (++frameLength > MaximumFrameCharacters) throw Invalid();
                if (character is not ('\r' or '\n')) { line.Append(character); continue; }
                if (line.Length == 0)
                {
                    if (name is not null || data.Length != 0)
                    {
                        if (name is null || data.Length == 0) throw Invalid();
                        yield return (name, data.ToString());
                    }
                    name = null; data.Clear(); frameLength = 0;
                    continue;
                }
                var value = line.ToString(); line.Clear();
                if (value.StartsWith(':')) continue;
                var separator = value.IndexOf(':');
                if (separator < 0) throw Invalid();
                var field = value[..separator]; var content = value[(separator + 1)..];
                if (content.StartsWith(' ')) content = content[1..];
                if (field == "event")
                {
                    if (name is not null || content is not ("start" or "delta" or "done" or "error")) throw Invalid();
                    name = content;
                }
                else if (field == "data")
                {
                    if (data.Length != 0) data.Append('\n');
                    data.Append(content);
                }
                else if (field is not ("id" or "retry")) throw Invalid();
            }
            if (count == 0) break;
        }
        if (line.Length != 0 || name is not null || data.Length != 0) throw Invalid();
    }

    private static void UniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid();
                UniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) UniqueProperties(child);
    }

    private static AiServiceException Map(string code) => code switch {
        "AI_PROVIDER_TIMEOUT" => new(AiServiceErrorCodes.ServiceTimeout, "مهلت پاسخ AI تمام شد.", 504, upstreamErrorCode: code),
        "AI_RATE_LIMITED" => new(AiServiceErrorCodes.RateLimited, "سرویس AI محدودیت موقت دارد.", 429, upstreamErrorCode: code),
        "AI_MODEL_UNAVAILABLE" => new(AiServiceErrorCodes.ServiceUnavailable, "سرویس AI موقتاً در دسترس نیست.", 503, upstreamErrorCode: code),
        "AI_RESPONSE_INVALID" or "AI_INTERNAL_ERROR" => new(AiServiceErrorCodes.ResponseInvalid, "پاسخ AI معتبر نیست.", 502, upstreamErrorCode: code),
        _ => Invalid() };
    private static AiServiceException Invalid() => new(AiServiceErrorCodes.ResponseInvalid, "جریان پاسخ AI معتبر نیست.");
}
