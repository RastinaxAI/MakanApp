using System.Text.Json;

namespace MakanApp.AiIntegrationProbe;

internal static class LiveAiErrorContract
{
    private static readonly HashSet<string> Codes =
    [
        "AI_INVALID_REQUEST", "AI_INVALID_CONTEXT", "AI_UNSUPPORTED_ROLE", "AI_MODEL_UNAVAILABLE",
        "AI_PROVIDER_TIMEOUT", "AI_RATE_LIMITED", "AI_CONTEXT_TOO_LARGE", "AI_RESPONSE_INVALID",
        "AI_SAFETY_BLOCKED", "AI_INTERNAL_ERROR", "AI_SERVICE_UNAUTHORIZED", "AI_SERVICE_FORBIDDEN"
    ];

    // فقط code از allowlist برمی‌گردد؛ message، body و exception هرگز وارد گزارش نمی‌شوند.
    public static (string Code, bool HasMessage) Read(string json, string? mediaType)
    {
        if (mediaType is null || !(mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
            return ("NON_CONTRACT_RESPONSE", false);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String && Codes.Contains(code.GetString()!))
                return (code.GetString()!, error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(message.GetString()));
        }
        catch (JsonException) { /* بدنه نامعتبر فقط یک نتیجهٔ تشخیصی امن دارد. */ }
        return ("NON_CONTRACT_RESPONSE", false);
    }
}
