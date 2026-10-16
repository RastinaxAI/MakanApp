using System.Net;
using System.Reflection;
using System.Text.Json;
using MakanApp.Api.Controllers;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2OpenApiContainsActualPersistentAndPresenceContractsWithoutWorkerSecrets()
    {
        using var client = factory.CreateClient(); using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = doc.RootElement.GetProperty("paths"); var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var (route, method) in new[]
        {
            ("/api/v1/people/{userId}/presence", "get"), ("/api/v1/messaging/folders", "get"),
            ("/api/v1/messaging/folders/{folderId}", "put"), ("/api/v1/messaging/folders/{folderId}", "delete"),
            ("/api/v1/conversations/saved/messages/{messageId}/labels", "put"),
            ("/api/v1/conversations/{conversationId}/invite-links", "post"), ("/api/v1/messaging/invite-links/redeem", "post"),
            ("/api/v1/messaging/join-requests/{requestId}/approve", "post"),
            ("/api/v1/conversations/{conversationId}/topics", "post"), ("/api/v1/conversations/{conversationId}/topics/{topicId}", "put"),
            ("/api/v1/conversations/{conversationId}/scheduled-messages", "post"), ("/api/v1/conversations/{conversationId}/scheduled-messages/{id}/cancel", "post"),
            ("/api/v1/conversations/{conversationId}/me/restriction", "get")
        }) Assert.True(paths.GetProperty(route).TryGetProperty(method, out _), method + " " + route);
        var parameters = paths.GetProperty("/api/v1/messages/search").GetProperty("get").GetProperty("parameters");
        foreach (var name in new[] { "hasAttachment", "hasLinks", "topicId", "savedLabel", "senderUserId", "conversationId" })
            Assert.Contains(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() == name);
        foreach (var hidden in new[] { "authority", "claimToken", "claimUntilUtc", "payloadHash", "sourceSessionId" })
            Assert.False(schemas.GetProperty(nameof(ScheduledMessageResult)).GetProperty("properties").TryGetProperty(hidden, out _));
        foreach (var hidden in new[] { "token", "tokenHash" }) Assert.False(schemas.GetProperty(nameof(InviteLinkResult)).GetProperty("properties").TryGetProperty(hidden, out _));
        Assert.True(schemas.GetProperty(nameof(SendMessageCommand)).GetProperty("properties").TryGetProperty("isSilent", out _));
        Assert.True(schemas.GetProperty(nameof(ConversationMessageResult)).GetProperty("properties").TryGetProperty("topicId", out _));
    }

    [Fact]
    public async Task P2OpenApiNewEnumsAndActualResponseUseStringNames()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json")); var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        var options = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        foreach (var value in new object[] { MessagingPresenceState.Hidden, ConversationTopicStatus.Closed, ConversationJoinRequestStatus.Pending, ScheduledMessageStatus.Scheduled, FolderConversationTypes.All })
        {
            var type = value.GetType(); var actual = JsonSerializer.SerializeToElement(value, type, options);
            Assert.Equal(JsonValueKind.String, actual.ValueKind); Assert.Equal("string", schemas.GetProperty(type.Name).GetProperty("type").GetString());
            if (!type.IsDefined(typeof(FlagsAttribute), false)) Assert.Contains(schemas.GetProperty(type.Name).GetProperty("enum").EnumerateArray(), i => i.GetString() == actual.GetString());
        }
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        using var empty = JsonDocument.Parse(await setup.TargetClient.GetStringAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/me/restriction"));
        Assert.Equal("None", empty.RootElement.GetProperty("deniedCapabilities").GetString()); Assert.Equal(JsonValueKind.Null, empty.RootElement.GetProperty("version").ValueKind);
    }

    [Fact]
    public async Task P2MetadataEndpointsHaveBoundedBodyPolicyAndRequireAuthentication()
    {
        foreach (var type in new[] { typeof(MessagingFoldersController), typeof(SavedMessageLabelsController), typeof(ConversationTopicsController),
            typeof(ConversationInviteLinksController), typeof(ScheduledMessagesController), typeof(ConversationAdvancedManagementController) })
            Assert.Equal(64 * 1024, ((IRequestSizeLimitMetadata)type.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
        using var anonymous = factory.CreateClient();
        foreach (var route in new[] { "/api/v1/messaging/folders", $"/api/v1/people/{Guid.NewGuid()}/presence", "/api/v1/messaging/join-requests",
            $"/api/v1/conversations/{Guid.NewGuid()}/topics", $"/api/v1/conversations/{Guid.NewGuid()}/scheduled-messages", $"/api/v1/conversations/{Guid.NewGuid()}/me/restriction" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
    }
}
