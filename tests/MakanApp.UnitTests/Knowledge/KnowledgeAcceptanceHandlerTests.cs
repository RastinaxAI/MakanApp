using System.Net;
using MakanApp.KnowledgeIntegrationProbe;
using Xunit;

namespace MakanApp.UnitTests.Knowledge;

public sealed class KnowledgeAcceptanceHandlerTests
{
    [Fact]
    public async Task CurrentNoAuthAcceptanceRejectsAuthorizationBeforeAnyUpstreamRequest()
    {
        var inner = new CountingHandler();
        using var client = new HttpClient(new KnowledgeAcceptanceHandler(inner)) { BaseAddress = new("https://knowledge.example.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/knowledge/retrieve");
        request.Headers.Authorization = new("Bearer", "non-secret-test-placeholder");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        Assert.Equal("KNOWLEDGE_ACCEPTANCE_AUTH_HEADER_FORBIDDEN", error.Message);
        Assert.Equal(0, inner.Calls);
    }

    private const string Source = "/api/v1/knowledge/sources/309f21bd-f81c-4d4c-b9bf-53c802824a93/versions/1/";

    [Theory]
    [InlineData("GET", Source + "status")]
    [InlineData("POST", Source + "ingest")]
    [InlineData("POST", "/api/v1/knowledge/retrieve")]
    [InlineData("POST", "/api/v1/knowledge/rag/answer")]
    public async Task AcceptanceAllowsOnlyRequiredTransportOperations(string method, string path)
    {
        var inner = new CountingHandler();
        using var client = new HttpClient(new KnowledgeAcceptanceHandler(inner)) { BaseAddress = new("https://knowledge.example.test") };
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Theory]
    [InlineData("POST", Source + "delete")]
    [InlineData("POST", Source + "supersede")]
    [InlineData("POST", Source + "reindex")]
    [InlineData("DELETE", Source + "status")]
    [InlineData("PUT", Source + "ingest")]
    [InlineData("GET", "/api/v1/knowledge/retrieve")]
    [InlineData("POST", "/api/v1/knowledge/sources/00000000-0000-0000-0000-000000000000/versions/1/ingest")]
    [InlineData("POST", "/api/v1/knowledge/sources/309f21bd-f81c-4d4c-b9bf-53c802824a93/versions/0/ingest")]
    [InlineData("POST", "/api/v1/knowledge/unknown")]
    public async Task FrozenLifecycleNeverReachesUpstream(string method, string path)
    {
        var inner = new CountingHandler();
        using var client = new HttpClient(new KnowledgeAcceptanceHandler(inner)) { BaseAddress = new("https://knowledge.example.test") };
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        Assert.Equal("KNOWLEDGE_ACCEPTANCE_OPERATION_FORBIDDEN", error.Message);
        Assert.Equal(0, inner.Calls);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }
    }
}
