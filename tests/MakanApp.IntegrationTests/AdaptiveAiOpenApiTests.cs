using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AdaptiveOpenApiPublishesStreamingProfileBoundsAndOwnerHistoryWithoutInvocationHeaders()
    {
        using var baseHost = AdaptiveHost();
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" })));
        using var client = host.CreateClient(); using var response = await client.GetAsync("/openapi/v1.json"); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement; var paths = root.GetProperty("paths");
        var stream = paths.GetProperty("/api/v1/ai/chat/stream").GetProperty("post");
        Assert.True(stream.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("text/event-stream", out _));
        Assert.Contains("AI_SOURCE_STREAMING_NOT_SUPPORTED", stream.GetProperty("description").GetString());
        var history = paths.GetProperty("/api/v1/ai/conversations/{conversationId}/history").GetProperty("get");
        if (history.TryGetProperty("parameters", out var parameters))
            Assert.DoesNotContain(parameters.EnumerateArray(), x => x.GetProperty("name").GetString() is "X-AI-Operation-Id" or "X-AI-Payer");
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var profile = schemas.GetProperty("AiAssistantProfile").GetProperty("properties");
        Assert.Equal(1, profile.GetProperty("gradeLevel").GetProperty("minimum").GetInt32());
        Assert.Equal(12, profile.GetProperty("gradeLevel").GetProperty("maximum").GetInt32());
        Assert.Equal(8, profile.GetProperty("goals").GetProperty("maxItems").GetInt32());
        Assert.Equal(80, profile.GetProperty("goals").GetProperty("items").GetProperty("maxLength").GetInt32());
        var input = schemas.GetProperty("SendAiChatCommand").GetProperty("properties");
        Assert.True(input.TryGetProperty("sourceIntent", out _)); Assert.True(input.TryGetProperty("selectedSources", out _));
        Assert.False(input.TryGetProperty("history", out _));
    }
}
