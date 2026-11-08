using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Infrastructure.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MakanApp.Infrastructure.Knowledge;

public sealed partial class AiKnowledgeClient(HttpClient http, IOptions<AiKnowledgeOptions> configuration,
    IKnowledgeRuntimeAvailability runtime, IAiServiceCredentialProvider credentials, TimeProvider clock,
    ILogger<AiKnowledgeClient> logger) : IAiKnowledgeClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private AiKnowledgeOptions Options => configuration.Value;

    public async Task<KnowledgeIngestResult> IngestAsync(KnowledgeIngestMetadata metadata, ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        ValidateIdentity(new(metadata.SourceId, metadata.Version));
        ValidateEnvelope(metadata.RequestId, metadata.CorrelationId);
        if (content.Length == 0 || content.Length > Options.MaximumContentBytes ||
            metadata.ContentLengthBytes != content.Length || metadata.ContentHash != Convert.ToHexStringLower(SHA256.HashData(content.Span)) ||
            metadata.ContentType is not ("application/pdf" or "image/jpeg" or "image/png" or "text/plain") ||
            string.IsNullOrWhiteSpace(metadata.Title) || metadata.ContentAccess?.Mode != "InlineMultipart" || !ValidScope(metadata.Scope))
            throw InvalidRequest();
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(JsonSerializer.Serialize(metadata, Json), Encoding.UTF8, "application/json"), "metadata");
        var bytes = new ReadOnlyMemoryContent(content);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(metadata.ContentType);
        multipart.Add(bytes, "content", "source");
        var response = await SendAsync<KnowledgeProcessingStatus>("ingest", HttpMethod.Post,
            Path(new(metadata.SourceId, metadata.Version), "ingest"), metadata.RequestId, metadata.CorrelationId, multipart,
            [200, 202], result => ValidateStatus(new(metadata.SourceId, metadata.Version), result), ct);
        if (response.Status == 202 && response.Value.State != "Queued") throw InvalidResponse();
        return new(response.Status, response.Value);
    }

    public async Task<KnowledgeProcessingStatus> StatusAsync(KnowledgeSourceVersion source, CancellationToken ct, string? correlationId = null)
    {
        ValidateIdentity(source);
        if (correlationId is not null && !ValidIdentifier(correlationId)) throw InvalidRequest();
        return (await SendAsync<KnowledgeProcessingStatus>("status", HttpMethod.Get, Path(source, "status"),
            Guid.NewGuid().ToString("N"), correlationId, null, [200], result => ValidateStatus(source, result), ct)).Value;
    }

    public Task<KnowledgeProcessingStatus> DeleteAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct) =>
        CommandAsync(source, request, "delete", "Deleted", 200, ct);
    public Task<KnowledgeProcessingStatus> SupersedeAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct) =>
        CommandAsync(source, request, "supersede", "Superseded", 200, ct);
    public Task<KnowledgeProcessingStatus> ReindexAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct) =>
        CommandAsync(source, request, "reindex", "Queued", 202, ct);

    private async Task<KnowledgeProcessingStatus> CommandAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request,
        string action, string expectedState, int expectedStatus, CancellationToken ct)
    {
        ValidateIdentity(source); ValidateEnvelope(request.RequestId, request.CorrelationId);
        using var content = Content(request);
        var response = await SendAsync<KnowledgeProcessingStatus>(action, HttpMethod.Post, Path(source, action),
            request.RequestId, request.CorrelationId, content, [expectedStatus], result =>
            {
                ValidateStatus(source, result);
                if (result.State != expectedState) throw InvalidResponse();
            }, ct);
        return response.Value;
    }

    public async Task<KnowledgeProcessingStatus> WaitForCompletionAsync(KnowledgeSourceVersion source, CancellationToken ct, string? correlationId = null)
    {
        runtime.EnsureEnabled();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(Options.StatusPollTimeoutSeconds));
        try
        {
            for (var attempt = 0; attempt < Options.StatusPollAttempts; attempt++)
            {
                var result = await StatusAsync(source, deadline.Token, correlationId);
                if (result.State is "NotIngested" or "Ready" or "ExtractFailed" or "OcrFailed" or "ChunkFailed" or
                    "EmbeddingFailed" or "IndexFailed" or "Deleted" or "Superseded" or "ReindexRequired") return result;
                if (attempt + 1 < Options.StatusPollAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(Options.StatusPollIntervalMilliseconds), clock, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        throw new AiKnowledgeException(KnowledgeErrorCodes.PollingTimeout, 504);
    }

    public async Task<KnowledgeRetrieveResponse> RetrieveAsync(KnowledgeRetrieveRequest request, CancellationToken ct)
    {
        ValidateQuery(request.RequestId, request.CorrelationId, request.Query, request.AllowedSourceVersions, request.MaxResults);
        using var content = Content(request);
        return (await SendAsync<KnowledgeRetrieveResponse>("retrieve", HttpMethod.Post, "/api/v1/knowledge/retrieve",
            request.RequestId, request.CorrelationId, content, [200], result => ValidateRetrieval(request, result), ct)).Value;
    }

    public async Task<KnowledgeRagResponse> AnswerAsync(KnowledgeRagRequest request, KnowledgeRetrieveResponse evidence, CancellationToken ct)
    {
        ValidateQuery(request.RequestId, request.CorrelationId, request.Query, request.AllowedSourceVersions, request.MaxResults);
        if (request.Language is not ("fa" or "en") || request.BusinessContext is null || request.BusinessContext.Count > 32 ||
            request.BusinessContext.Any(f => f is null || !ValidIdentifier(f.Name) || string.IsNullOrWhiteSpace(f.Value) || f.Value.Length > 1000) ||
            request.BusinessContext.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != request.BusinessContext.Count)
            throw InvalidRequest();
        ValidateRetrieval(new(request.RequestId, request.CorrelationId, request.Query, request.AllowedSourceVersions,
            request.MaxResults, request.IncludeQuotePreview), evidence);
        using var content = Content(request);
        return (await SendAsync<KnowledgeRagResponse>("rag", HttpMethod.Post, "/api/v1/knowledge/rag/answer",
            request.RequestId, request.CorrelationId, content, [200], result => ValidateAnswer(request, evidence, result), ct)).Value;
    }

    private async Task<(T Value, int Status)> SendAsync<T>(string operation, HttpMethod method, string path,
        string requestId, string? correlationId, HttpContent? content, int[] accepted, Action<T> validate, CancellationToken ct)
    {
        runtime.EnsureEnabled();
        if (!AiKnowledgeOptions.IsValid(Options)) throw new InvalidOperationException("تنظیمات Knowledge معتبر نیست.");
        var started = Stopwatch.GetTimestamp();
        using var activity = AiServiceTelemetry.Start("knowledge_" + operation, requestId, correlationId);
        activity?.SetTag("http.request.method", method.Method);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Options.TimeoutSeconds));
        var errorCode = "none";
        var status = 0;
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(Options.BaseUrl), path)) { Content = content };
            request.Headers.Add("X-AI-Contract-Version", Options.ContractVersion);
            if (correlationId is not null) request.Headers.Add("X-Correlation-ID", correlationId);
            if (Options.UseServiceAuthentication)
            {
                var credential = await credentials.GetCredentialAsync(timeout.Token);
                request.Headers.Authorization = new AuthenticationHeaderValue(credential.Scheme, credential.Token);
            }
            // هر intent فقط یک HTTP write دارد؛ retry شبکه یا fallback ضمنی وجود ندارد.
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            status = (int)response.StatusCode;
            AiServiceTelemetry.SetHttpStatus(status);
            var bytes = await ReadBoundedAsync(response.Content, timeout.Token);
            if (!response.IsSuccessStatusCode) throw MapFailure(response, bytes);
            if (!accepted.Contains(status) || !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)) throw InvalidResponse();
            var value = JsonSerializer.Deserialize<T>(bytes, Json) ?? throw InvalidResponse();
            validate(value);
            return (value, status);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { errorCode = KnowledgeErrorCodes.Timeout; throw new AiKnowledgeException(errorCode, 504); }
        catch (OperationCanceledException) { errorCode = "CANCELLED"; throw; }
        catch (AiKnowledgeException error) { errorCode = error.Code; throw; }
        catch (JsonException) { errorCode = KnowledgeErrorCodes.ResponseInvalid; throw InvalidResponse(); }
        catch (Exception error) when (error is HttpRequestException or IOException)
        { errorCode = KnowledgeErrorCodes.Unavailable; throw new AiKnowledgeException(errorCode, 503); }
        catch (Exception)
        { errorCode = KnowledgeErrorCodes.Unavailable; throw new AiKnowledgeException(errorCode, 503); }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(started);
            AiServiceTelemetry.Record("knowledge_" + operation, errorCode == "none" ? "success" : "failure", errorCode, duration);
            if (errorCode == "none") logger.LogInformation(
                "Knowledge completed: {Operation} {StatusCode} {RequestId} {CorrelationId} {DurationMs}.",
                operation, status, requestId, correlationId, duration.TotalMilliseconds);
            else logger.LogError("Knowledge failed: {Operation} {StatusCode} {Code} {RequestId} {CorrelationId} {DurationMs}.",
                operation, status, errorCode, requestId, correlationId, duration.TotalMilliseconds);
        }
    }

    private async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > Options.MaximumResponseBytes) throw InvalidResponse();
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + count > Options.MaximumResponseBytes) throw InvalidResponse();
            memory.Write(buffer, 0, count);
        }
        return memory.ToArray();
    }

    private static AiKnowledgeException MapFailure(HttpResponseMessage response, byte[] bytes)
    {
        string? code = null, detail = null;
        try
        {
            using var json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String) code = value.GetString();
                if (error.TryGetProperty("processingFailureCode", out var failure) && failure.ValueKind == JsonValueKind.String)
                    detail = failure.GetString() == "AI_OCR_FAILED" ? "AI_OCR_FAILED" : null;
            }
        }
        catch (JsonException) { /* متن خام خطا قابل اعتماد یا قابل ثبت نیست. */ }
        var status = (int)response.StatusCode;
        if (status == 429) return new(KnowledgeErrorCodes.RateLimited, 429, retryAfter: response.Headers.RetryAfter?.Delta);
        var valid = status switch
        {
            409 => code is KnowledgeErrorCodes.NotIngested or KnowledgeErrorCodes.Processing or KnowledgeErrorCodes.ProcessingFailed or KnowledgeErrorCodes.VersionConflict,
            422 => code is KnowledgeErrorCodes.InvalidRequest or KnowledgeErrorCodes.ContextTooLarge,
            502 => code == KnowledgeErrorCodes.RagResponseInvalid,
            503 => code is KnowledgeErrorCodes.VectorUnavailable or KnowledgeErrorCodes.EmbeddingUnavailable or KnowledgeErrorCodes.GenerationUnavailable,
            _ => false
        };
        return valid ? new(code!, status, code == KnowledgeErrorCodes.ProcessingFailed ? detail : null, response.Headers.RetryAfter?.Delta)
            : new(status == 504 ? KnowledgeErrorCodes.Timeout : KnowledgeErrorCodes.Unavailable, status == 504 ? 504 : 503,
                retryAfter: response.Headers.RetryAfter?.Delta);
    }

    private static StringContent Content<T>(T value) => new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    private static string Path(KnowledgeSourceVersion source, string action) => $"/api/v1/knowledge/sources/{source.SourceId:D}/versions/{source.Version}/{action}";
    private static AiKnowledgeException InvalidRequest() => new(KnowledgeErrorCodes.InvalidRequest, 422);
    private static AiKnowledgeException InvalidResponse() => new(KnowledgeErrorCodes.ResponseInvalid, 502);
}
