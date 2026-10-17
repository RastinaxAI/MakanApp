using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using MakanApp.Api.Controllers;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2ChecklistOpenApiAndRuntimeAgreeOnModeMessageKindRoutesAndPrivateDto()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        using var doc = JsonDocument.Parse(await setup.FirstClient.GetStringAsync("/openapi/v1.json"));
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas"); var paths = doc.RootElement.GetProperty("paths");
        foreach (var (route, method) in new[]
        {
            ("/api/v1/conversations/{conversationId}/checklists", "post"),
            ("/api/v1/conversations/{conversationId}/messages/{messageId}/checklist", "get"),
            ("/api/v1/conversations/{conversationId}/messages/{messageId}/checklist", "put"),
            ("/api/v1/conversations/{conversationId}/messages/{messageId}/checklist/items/{itemId}", "put")
        }) Assert.True(paths.GetProperty(route).TryGetProperty(method, out _));
        foreach (var (name, value) in new[] { (nameof(ConversationChecklistMode), "Personal"), (nameof(MessageKind), "Checklist"), (nameof(MessagingChangeType), "ChecklistChanged") })
        {
            Assert.Equal("string", schemas.GetProperty(name).GetProperty("type").GetString());
            Assert.Contains(schemas.GetProperty(name).GetProperty("enum").EnumerateArray(), e => e.GetString() == value);
        }
        using var wire = JsonDocument.Parse(await setup.FirstClient.GetStringAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId)));
        Assert.Equal("Personal", wire.RootElement.GetProperty("mode").GetString()); Assert.Equal(JsonValueKind.False, wire.RootElement.GetProperty("isCompleted").ValueKind);
        foreach (var field in new[] { "creationHash", "creatorUserId", "otherUsers", "totalChecked", "checkedByUserId", "checkedAtUtc", "status" })
            Assert.False(schemas.GetProperty(nameof(ConversationChecklistResult)).GetProperty("properties").TryGetProperty(field, out _));
        Assert.False(schemas.GetProperty(nameof(SetChecklistItemCommand)).GetProperty("properties").TryGetProperty("userId", out _));
        Assert.Equal(64 * 1024, ((IRequestSizeLimitMetadata)typeof(ConversationChecklistsController).GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId))).StatusCode);
    }

    [Fact]
    public async Task P2ChecklistInvalidModeDefinitionAndVersionsFailSafelyAndGenericSendCannotMakeOrphan()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Checklist(); var route = $"/api/v1/conversations/{setup.ConversationId}/checklists";
        foreach (var invalid in new[] { command with { Mode = null }, command with { Mode = (ConversationChecklistMode)999 }, command with { Items = [] },
            command with { Items = [new(Guid.Empty, "کار")] }, command with { Items = [command.Items.First(), command.Items.First()] }, command with { Title = " " } })
            Assert.Equal(HttpStatusCode.BadRequest, (await setup.FirstClient.PostAsJsonAsync(route, invalid, JsonOptions)).StatusCode);
        using var unknown = await setup.FirstClient.PostAsJsonAsync(route, new { command.ClientMessageId, command.Title, Mode = "NotAMode", command.Items }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId); var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        foreach (var invalid in new[] { new SetChecklistItemCommand(null, checklist.Version), new SetChecklistItemCommand(true, "not-base64"), new SetChecklistItemCommand(true, checklist.Version, "AA==") })
            Assert.Equal(HttpStatusCode.BadRequest, (await setup.FirstClient.PutAsJsonAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId) + $"/items/{checklist.Items.First().ItemId}", invalid, JsonOptions)).StatusCode);
        using var orphan = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Checklist, Text = "بدون تعریف" }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, orphan.StatusCode); Assert.Equal(MessagingErrorCodes.MessageKindInvalid, await ReadProblemCodeAsync(orphan));
        using var scheduled = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/scheduled-messages",
            new ScheduleMessageCommand(DateTime.UtcNow.AddHours(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Checklist, Text = "تعریف گم‌شده" }), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, scheduled.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
    }

    [Fact]
    public async Task P2ChecklistTopicCreationAndDefinitionRespectCurrentClosedTopicWithoutBlockingExistingCheckState()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var topic = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "موضوع فهرست")); var command = P2Checklist() with { TopicId = topic.TopicId, IsSilent = true };
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id, command); var checklist = await ReadP2ChecklistAsync(setup.TargetClient, id, receipt.MessageId);
        await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed);
        Assert.Equal(receipt.MessageId, (await CreateP2ChecklistAsync(setup.OwnerClient, id, command)).MessageId);
        Assert.Equal(HttpStatusCode.Conflict, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/checklists", command with { ClientMessageId = Guid.NewGuid() }, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateP2ChecklistResponseAsync(setup.OwnerClient, checklist, P2ChecklistUpdate(checklist))).StatusCode);
        Assert.True((await SetP2ChecklistAsync(setup.TargetClient, checklist, checklist.Items.First().ItemId, true)).Items.First().IsChecked);
    }
}
