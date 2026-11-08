using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Infrastructure.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.UnitTests.Knowledge;

public sealed class AiKnowledgeClientTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly KnowledgeSourceVersion Source = new(Guid.Parse("309f21bd-f81c-4d4c-b9bf-53c802824a93"), 1);
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("Synthetic QA: energy is conserved.");

    [Fact]
    public async Task IngestUsesExactMultipartBytesHashAndIdentityAndPreservesReplayStatus()
    {
        var metadata = Metadata();
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal($"/api/v1/knowledge/sources/{Source.SourceId:D}/versions/1/ingest", request.RequestUri!.AbsolutePath);
            Assert.Equal("knowledge-v1", request.Headers.GetValues("X-AI-Contract-Version").Single());
            Assert.Equal(metadata.CorrelationId, request.Headers.GetValues("X-Correlation-ID").Single());
            Assert.Null(request.Headers.Authorization);
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            var parts = multipart.ToArray();
            Assert.Equal(2, parts.Length);
            Assert.Equal("metadata", parts[0].Headers.ContentDisposition!.Name!.Trim('"'));
            Assert.Equal("application/json", parts[0].Headers.ContentType!.MediaType);
            Assert.Equal(metadata, JsonSerializer.Deserialize<KnowledgeIngestMetadata>(await parts[0].ReadAsStringAsync(ct), Json));
            Assert.Equal("content", parts[1].Headers.ContentDisposition!.Name!.Trim('"'));
            Assert.Equal(Bytes, await parts[1].ReadAsByteArrayAsync(ct));
            return Response(HttpStatusCode.Accepted, Status("Queued"));
        });
        var client = Client(handler);
        Assert.Equal(202, (await client.IngestAsync(metadata, Bytes, default)).StatusCode);
        handler.Respond = (_, _) => Task.FromResult(Response(HttpStatusCode.OK, Status("Ready")));
        var replay = await client.IngestAsync(metadata, Bytes, default);
        Assert.Equal(200, replay.StatusCode); Assert.Equal("Ready", replay.Status.State); Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("length")]
    [InlineData("mime")]
    [InlineData("library")]
    [InlineData("empty-id")]
    [InlineData("version")]
    [InlineData("correlation")]
    public async Task InvalidIngestNeverSendsHttp(string invalid)
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException());
        var metadata = invalid switch
        {
            "hash" => Metadata() with { ContentHash = new string('0', 64) },
            "length" => Metadata() with { ContentLengthBytes = Bytes.Length + 1 },
            "mime" => Metadata() with { ContentType = "application/octet-stream" },
            "library" => Metadata() with { Scope = new("Library") },
            "empty-id" => Metadata() with { SourceId = Guid.Empty },
            "version" => Metadata() with { Version = 0 },
            _ => Metadata() with { CorrelationId = " corr " }
        };
        var error = await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).IngestAsync(metadata, Bytes, default));
        Assert.Equal(KnowledgeErrorCodes.InvalidRequest, error.Code); Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("delete", "Deleted", 200)]
    [InlineData("supersede", "Superseded", 200)]
    [InlineData("reindex", "Queued", 202)]
    public async Task LifecycleCommandsUseFrozenRoutesAndSameIdentity(string action, string state, int status)
    {
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/versions/1/" + action, request.RequestUri!.AbsolutePath);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("request-1", document.RootElement.GetProperty("requestId").GetString());
            Assert.Equal("corr-1", document.RootElement.GetProperty("correlationId").GetString());
            return Response((HttpStatusCode)status, Status(state));
        });
        var client = Client(handler); var command = new KnowledgeCommandRequest("request-1", "corr-1");
        var result = action switch
        {
            "delete" => await client.DeleteAsync(Source, command, default),
            "supersede" => await client.SupersedeAsync(Source, command, default),
            _ => await client.ReindexAsync(Source, command, default)
        };
        Assert.Equal(state, result.State); Assert.Equal(Source.SourceId, result.SourceId); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task StatusPollingStopsAtReadyAndIsBoundedWhenProcessingNeverFinishes()
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, Status("Queued"))));
        var client = Client(handler, new() { Enabled = true, BaseUrl = "https://knowledge.example.test", StatusPollAttempts = 2, StatusPollIntervalMilliseconds = 1 });
        var error = await Assert.ThrowsAsync<AiKnowledgeException>(() => client.WaitForCompletionAsync(Source, default));
        Assert.Equal(KnowledgeErrorCodes.PollingTimeout, error.Code); Assert.Equal(2, handler.Calls);
        handler.Respond = (_, _) => Task.FromResult(Response(HttpStatusCode.OK, Status("Ready")));
        Assert.Equal("Ready", (await client.WaitForCompletionAsync(Source, default)).State); Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task StatusAndBoundedPollingPreserveCorrelationWithoutBodyOrRetryingWrites()
    {
        var handler = new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("corr-status", request.Headers.GetValues("X-Correlation-ID").Single());
            Assert.Null(request.Content);
            return Task.FromResult(Response(HttpStatusCode.OK, Status("Ready")));
        });
        var client = Client(handler);
        Assert.Equal("Ready", (await client.StatusAsync(Source, default, "corr-status")).State);
        Assert.Equal("Ready", (await client.WaitForCompletionAsync(Source, default, "corr-status")).State);
        await Assert.ThrowsAsync<AiKnowledgeException>(() => client.StatusAsync(Source, default, " corr-status "));
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("ExtractFailed")]
    [InlineData("OcrFailed")]
    [InlineData("ChunkFailed")]
    [InlineData("EmbeddingFailed")]
    [InlineData("IndexFailed")]
    [InlineData("Deleted")]
    [InlineData("Superseded")]
    [InlineData("ReindexRequired")]
    public async Task PollingReturnsTerminalStateWithoutTreatingItAsReady(string state)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, Status(state))));
        Assert.Equal(state, (await Client(handler).WaitForCompletionAsync(Source, default)).State); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(409, KnowledgeErrorCodes.VersionConflict)]
    [InlineData(409, KnowledgeErrorCodes.Processing)]
    [InlineData(409, KnowledgeErrorCodes.ProcessingFailed)]
    [InlineData(422, KnowledgeErrorCodes.InvalidRequest)]
    [InlineData(422, KnowledgeErrorCodes.ContextTooLarge)]
    [InlineData(503, KnowledgeErrorCodes.VectorUnavailable)]
    [InlineData(503, KnowledgeErrorCodes.EmbeddingUnavailable)]
    [InlineData(503, KnowledgeErrorCodes.GenerationUnavailable)]
    [InlineData(502, KnowledgeErrorCodes.RagResponseInvalid)]
    public async Task KnownErrorsPreserveCodeAndStatusWithoutRetryOrPrivateBodyLogging(int status, string code)
    {
        const string secret = "private-provider-payload-do-not-log";
        var log = new Logger();
        var handler = new Handler((_, _) => Task.FromResult(Response((HttpStatusCode)status,
            new { error = new { code, message = secret, processingFailureCode = "AI_OCR_FAILED" } })));
        var error = await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler, logger: log).RetrieveAsync(Query(), default));
        Assert.Equal(code, error.Code); Assert.Equal(status, error.StatusCode); Assert.Equal(1, handler.Calls);
        Assert.Equal(code == KnowledgeErrorCodes.ProcessingFailed ? "AI_OCR_FAILED" : null, error.ProcessingFailureCode);
        Assert.DoesNotContain(secret, string.Join("\n", log.Messages)); Assert.Contains(log.Levels, x => x == LogLevel.Error);
    }

    [Theory]
    [InlineData(504, KnowledgeErrorCodes.Timeout)]
    [InlineData(503, KnowledgeErrorCodes.Unavailable)]
    [InlineData(429, KnowledgeErrorCodes.RateLimited)]
    public async Task UnknownTransportErrorsAreSanitizedAndNeverRetried(int status, string expected)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("raw SQL or provider exception") }));
        var error = await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).RetrieveAsync(Query(), default));
        Assert.Equal(expected, error.Code); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CallerCancellationAndTimeoutRemainDistinct()
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); throw new InvalidOperationException(); });
        var client = Client(handler, new() { Enabled = true, BaseUrl = "https://knowledge.example.test", TimeoutSeconds = 1 });
        var error = await Assert.ThrowsAsync<AiKnowledgeException>(() => client.RetrieveAsync(Query(), default));
        Assert.Equal(KnowledgeErrorCodes.Timeout, error.Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RetrieveAsync(Query(), cancelled.Token));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("citation")]
    [InlineData("rank")]
    [InlineData("preview")]
    [InlineData("correlation")]
    public async Task RetrievalRejectsForgedSourceVersionCitationAndEnvelope(string invalid)
    {
        var result = Evidence(); var item = result.Results[0];
        result = invalid switch
        {
            "source" => result with { Results = [item with { SourceId = Guid.NewGuid() }] },
            "version" => result with { Results = [item with { Version = 2 }] },
            "citation" => result with { Results = [item with { Citation = item.Citation with { ChunkId = "forged" } }] },
            "rank" => result with { Results = [item with { Rank = 2 }] },
            "preview" => result with { Results = [item with { Citation = item.Citation with { QuotePreview = "not in chunk" } }] },
            _ => result with { CorrelationId = "wrong" }
        };
        var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, result)));
        Assert.Equal(KnowledgeErrorCodes.ResponseInvalid, (await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).RetrieveAsync(Query(), default))).Code);
    }

    [Fact]
    public async Task RagAcceptsOnlyClaimsCitedFromActualRetrievalAndDoesNotReturnSilentFallback()
    {
        var evidence = Evidence(); var citation = evidence.Results[0].Citation;
        var valid = new KnowledgeRagResponse("request-1", "corr-1", "Ok", "Grounded claim.", [new("Grounded claim.", [citation])], [citation]);
        var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, valid)));
        var request = new KnowledgeRagRequest("request-1", "corr-1", "What is energy?", "en", [Source], 8, true, []);
        Assert.Equal(valid.Answer, (await Client(handler).AnswerAsync(request, evidence, default)).Answer);
        handler.Respond = (_, _) => Task.FromResult(Response(HttpStatusCode.OK, valid with { Answer = "Uncited fallback." }));
        await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).AnswerAsync(request, evidence, default));
        var forged = citation with { Title = "Invented citation" };
        handler.Respond = (_, _) => Task.FromResult(Response(HttpStatusCode.OK, valid with { Claims = [new("Grounded claim.", [forged])], Citations = [forged] }));
        await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).AnswerAsync(request, evidence, default));
        handler.Respond = (_, _) => Task.FromResult(Response(HttpStatusCode.OK,
            new KnowledgeRagResponse("request-1", "corr-1", "InsufficientEvidence", "Not enough evidence.", [], [])));
        Assert.Equal("InsufficientEvidence", (await Client(handler).AnswerAsync(request, evidence, default)).Status);
    }

    [Fact]
    public async Task EmptyOrWildcardAllowlistNeverMakesNetworkCall()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException());
        await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).RetrieveAsync(Query() with { AllowedSourceVersions = [] }, default));
        await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler).RetrieveAsync(Query() with { AllowedSourceVersions = [new(Guid.Empty, 0)] }, default));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"error\":[]}")]
    public async Task NonObjectErrorEnvelopeIsUnavailableAndNeverRetriedOrLoggedRaw(string body)
    {
        var logger = new Logger();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        var error = await Assert.ThrowsAsync<AiKnowledgeException>(() => Client(handler, logger: logger).RetrieveAsync(Query(), default));
        Assert.Equal(KnowledgeErrorCodes.Unavailable, error.Code);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(1, handler.Calls);
        Assert.Contains(LogLevel.Error, logger.Levels);
        Assert.All(logger.Messages, message => Assert.DoesNotContain(body, message));
    }

    private static KnowledgeIngestMetadata Metadata() => new("request-1", "corr-1", Source.SourceId, 1,
        Convert.ToHexStringLower(SHA256.HashData(Bytes)), "text/plain", Bytes.Length, "Synthetic QA", new("Personal", Source.SourceId), new());
    private static KnowledgeProcessingStatus Status(string state) => new(Source.SourceId, 1, state, Now, state == "Ready" ? Now : null);
    private static KnowledgeRetrieveRequest Query() => new("request-1", "corr-1", "What is energy?", [Source], 8, true);
    private static KnowledgeRetrieveResponse Evidence() => new("request-1", "corr-1", "Ok",
        [new(Source.SourceId, 1, 1, "chunk-1", "Energy is conserved.", new(Source.SourceId, 1, "chunk-1", "Synthetic QA", null, null, null, "Energy"))]);
    private static HttpResponseMessage Response<T>(HttpStatusCode status, T value) => new(status)
    { Content = new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json") };
    private static AiKnowledgeClient Client(Handler handler, AiKnowledgeOptions? options = null, Logger? logger = null) =>
        new(new HttpClient(handler), Options.Create(options ?? new() { Enabled = true, BaseUrl = "https://knowledge.example.test" }),
            new Runtime(), new Credentials(), TimeProvider.System, logger ?? new());
    private sealed class Runtime : IKnowledgeRuntimeAvailability { public void EnsureEnabled() { } }
    private sealed class Credentials : IAiServiceCredentialProvider
    { public ValueTask<AiServiceCredential> GetCredentialAsync(CancellationToken ct) => throw new InvalidOperationException("No authentication expected."); }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return Respond(request, ct); }
    }
    private sealed class Logger : ILogger<AiKnowledgeClient>
    {
        public List<string> Messages { get; } = [];
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Levels.Add(level); Messages.Add(formatter(state, error)); Assert.Null(error); }
    }
}
