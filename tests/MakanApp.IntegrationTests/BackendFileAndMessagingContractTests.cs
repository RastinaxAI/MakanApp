using System.Text.Json;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task StabilizationFileOpenApiDescribesBinaryAndRangeWithoutJsonSuccess()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/v1/files/{fileId}/content").GetProperty("get");
        foreach (var status in new[] { "200", "206" })
        {
            var response = operation.GetProperty("responses").GetProperty(status);
            var content = response.GetProperty("content");
            Assert.False(content.TryGetProperty("application/json", out _));
            var binary = content.GetProperty("application/octet-stream").GetProperty("schema");
            Assert.Equal("string", binary.GetProperty("type").GetString());
            Assert.Equal("binary", binary.GetProperty("format").GetString());
            Assert.True(response.GetProperty("headers").TryGetProperty("Content-Disposition", out _));
        }
        Assert.True(operation.GetProperty("responses").TryGetProperty("416", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("409", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("503", out _));
        Assert.False(operation.GetProperty("responses").TryGetProperty("410", out _));
        Assert.True(operation.GetProperty("responses").GetProperty("404").GetProperty("content").TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public async Task StabilizationManagedConversationOpenApiDoesNotPromiseGeneralDirectDetails()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/v1/conversations/{conversationId}").GetProperty("get");
        Assert.Contains("مدیریتی", operation.GetProperty("summary").GetString());
        Assert.Contains("Direct", operation.GetProperty("description").GetString());
        Assert.Contains(operation.GetProperty("tags").EnumerateArray(), item => item.GetString() == "Conversation Management");
    }
}
