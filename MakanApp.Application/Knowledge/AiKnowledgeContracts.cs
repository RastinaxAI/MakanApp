using System.Text.Json.Serialization;
using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Knowledge;

public enum KnowledgeProcessingState
{
    NotIngested, Queued, Extracting, OCR, Normalizing, Chunking, Embedding, Indexing, Ready,
    ExtractFailed, OcrFailed, ChunkFailed, EmbeddingFailed, IndexFailed, Superseded, Deleted, ReindexRequired
}

public sealed record KnowledgeSourceVersion([property: JsonRequired] Guid SourceId, [property: JsonRequired] int Version);
public sealed record KnowledgeScope(string Type, Guid? OwnerUserId = null, Guid? OrganizationId = null, Guid? ClassId = null);
public sealed record KnowledgeContentAccess(string Mode = "InlineMultipart");
public sealed record KnowledgeIngestMetadata(string RequestId, string CorrelationId, Guid SourceId, int Version,
    string ContentHash, string ContentType, long ContentLengthBytes, string Title, KnowledgeScope Scope,
    KnowledgeContentAccess ContentAccess);
public sealed record KnowledgeCommandRequest(string RequestId, string CorrelationId);
public sealed record KnowledgeProcessingStatus([property: JsonRequired] Guid SourceId, [property: JsonRequired] int Version,
    [property: JsonRequired] string State, [property: JsonRequired] DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ReadyAtUtc = null, string? FailureCode = null, bool Retryable = false);
public sealed record KnowledgeIngestResult(int StatusCode, KnowledgeProcessingStatus Status);
public sealed record KnowledgeRetrieveRequest(string RequestId, string CorrelationId, string Query,
    IReadOnlyList<KnowledgeSourceVersion> AllowedSourceVersions, int MaxResults = 8, bool IncludeQuotePreview = false);
public sealed record KnowledgeCitation([property: JsonRequired] Guid SourceId, [property: JsonRequired] int Version,
    [property: JsonRequired] string ChunkId, [property: JsonRequired] string Title,
    [property: JsonRequired] int? PageStart, [property: JsonRequired] int? PageEnd,
    [property: JsonRequired] string? SectionTitle, [property: JsonRequired] string? QuotePreview);
public sealed record KnowledgeRetrieveResult([property: JsonRequired] Guid SourceId, [property: JsonRequired] int Version,
    [property: JsonRequired] int Rank, [property: JsonRequired] string ChunkId, [property: JsonRequired] string Text,
    [property: JsonRequired] KnowledgeCitation Citation);
public sealed record KnowledgeRetrieveResponse([property: JsonRequired] string RequestId, [property: JsonRequired] string CorrelationId,
    [property: JsonRequired] string Status, [property: JsonRequired] IReadOnlyList<KnowledgeRetrieveResult> Results);
public sealed record KnowledgeContextFact(string Name, string Value);
public sealed record KnowledgeRagRequest(string RequestId, string CorrelationId, string Query, string Language,
    IReadOnlyList<KnowledgeSourceVersion> AllowedSourceVersions, int MaxResults, bool IncludeQuotePreview,
    IReadOnlyList<KnowledgeContextFact> BusinessContext);
public sealed record KnowledgeRagClaim([property: JsonRequired] string Text, [property: JsonRequired] IReadOnlyList<KnowledgeCitation> Citations);
public sealed record KnowledgeRagResponse([property: JsonRequired] string RequestId, [property: JsonRequired] string CorrelationId,
    [property: JsonRequired] string Status, [property: JsonRequired] string Answer,
    [property: JsonRequired] IReadOnlyList<KnowledgeRagClaim> Claims,
    [property: JsonRequired] IReadOnlyList<KnowledgeCitation> Citations);

public interface IAiKnowledgeClient
{
    Task<KnowledgeIngestResult> IngestAsync(KnowledgeIngestMetadata metadata, ReadOnlyMemory<byte> content, CancellationToken ct);
    Task<KnowledgeProcessingStatus> StatusAsync(KnowledgeSourceVersion source, CancellationToken ct, string? correlationId = null);
    Task<KnowledgeProcessingStatus> DeleteAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct);
    Task<KnowledgeProcessingStatus> SupersedeAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct);
    Task<KnowledgeProcessingStatus> ReindexAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct);
    Task<KnowledgeProcessingStatus> WaitForCompletionAsync(KnowledgeSourceVersion source, CancellationToken ct, string? correlationId = null);
    Task<KnowledgeRetrieveResponse> RetrieveAsync(KnowledgeRetrieveRequest request, CancellationToken ct);
    Task<KnowledgeRagResponse> AnswerAsync(KnowledgeRagRequest request, KnowledgeRetrieveResponse evidence, CancellationToken ct);
}

public interface IKnowledgeRuntimeAvailability { void EnsureEnabled(); }
public sealed record KnowledgeOperationAudit(SourceActor Actor, Guid OperationId, KnowledgeSourceVersion Source,
    string Operation, string Reason, string RequestId, string CorrelationId, string Outcome);
public interface IKnowledgeOperationAudit
{
    Task WriteAsync(KnowledgeOperationAudit entry, CancellationToken ct);
}

// ورودی عمومی فقط selector است؛ scope، actor، context و allowlist نهایی را سرور می‌سازد.
public sealed record KnowledgeQueryCommand(Guid ClientOperationId, string Query, IReadOnlyList<KnowledgeSourceVersion> Sources,
    int MaxResults = 8, bool IncludeQuotePreview = false, string Language = "fa", AiPayerSelection? Payer = null);
public sealed record KnowledgeMutationCommand(Guid ClientOperationId, string ExpectedVersion, string Reason,
    AiPayerSelection? Payer = null);

public sealed class AiKnowledgeException(string code, int statusCode, string? processingFailureCode = null,
    TimeSpan? retryAfter = null) : Exception("سرویس دانش درخواست را با قرارداد معتبر تکمیل نکرد.")
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public string? ProcessingFailureCode { get; } = processingFailureCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public static class KnowledgeErrorCodes
{
    public const string InvalidRequest = "AI_KNOWLEDGE_INVALID_REQUEST";
    public const string NotIngested = "AI_SOURCE_NOT_INGESTED";
    public const string Processing = "AI_SOURCE_PROCESSING";
    public const string ProcessingFailed = "AI_SOURCE_PROCESSING_FAILED";
    public const string VersionConflict = "AI_SOURCE_VERSION_CONFLICT";
    public const string VectorUnavailable = "AI_VECTOR_UNAVAILABLE";
    public const string EmbeddingUnavailable = "AI_EMBEDDING_UNAVAILABLE";
    public const string ContextTooLarge = "AI_RAG_CONTEXT_TOO_LARGE";
    public const string GenerationUnavailable = "AI_RAG_GENERATION_UNAVAILABLE";
    public const string RagResponseInvalid = "AI_RAG_RESPONSE_INVALID";
    public const string ResponseInvalid = "AI_KNOWLEDGE_RESPONSE_INVALID";
    public const string Unavailable = "AI_KNOWLEDGE_UNAVAILABLE";
    public const string Timeout = "AI_KNOWLEDGE_TIMEOUT";
    public const string PollingTimeout = "AI_KNOWLEDGE_POLLING_TIMEOUT";
    public const string RateLimited = "AI_KNOWLEDGE_RATE_LIMITED";
}
