using System.Globalization;

namespace MakanApp.KnowledgeIntegrationProbe;

// رفتار پیش‌فرض Lifecycle منجمد است؛ پذیرش Test فقط برای شناسه/نسخهٔ QA ساخته‌شده در همان اجرا cleanup دارد.
internal sealed class KnowledgeAcceptanceHandler(HttpMessageHandler inner, Func<Guid, int, bool>? mayDeleteOwnQaSource = null) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is not null)
            throw new InvalidOperationException("KNOWLEDGE_ACCEPTANCE_AUTH_HEADER_FORBIDDEN");
        var path = request.RequestUri?.AbsolutePath;
        var query = request.Method == HttpMethod.Post && path is "/api/v1/knowledge/retrieve" or "/api/v1/knowledge/rag/answer";
        var segments = path?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var source = segments is { Length: 8 } && segments[0] == "api" && segments[1] == "v1" &&
            segments[2] == "knowledge" && segments[3] == "sources" &&
            Guid.TryParseExact(segments[4], "D", out var id) && id != Guid.Empty && segments[5] == "versions" &&
            int.TryParse(segments[6], NumberStyles.None, CultureInfo.InvariantCulture, out var version) && version > 0 &&
            (request.Method == HttpMethod.Get && segments[7] == "status" || request.Method == HttpMethod.Post &&
                (segments[7] == "ingest" || segments[7] == "delete" && mayDeleteOwnQaSource?.Invoke(id, version) == true));
        if (!query && !source) throw new InvalidOperationException("KNOWLEDGE_ACCEPTANCE_OPERATION_FORBIDDEN");
        return base.SendAsync(request, ct);
    }
}
