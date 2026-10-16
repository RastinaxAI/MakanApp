using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task MessengerOpenApiEnumsMatchActualMvcAndSignalRSerialization()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var mvc = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var hub = factory.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions;
        var enumTypes = typeof(Conversation).Assembly.GetTypes().Concat(typeof(MessagingService).Assembly.GetTypes())
            .Where(type => type.IsEnum && (type.Namespace == "MakanApp.Domain.Messaging" ||
                type.Namespace == "MakanApp.Application.Messaging" || type.Name.StartsWith("CommunicationAge", StringComparison.Ordinal)))
            .Where(type => schemas.TryGetProperty(type.Name, out _)).ToArray();
        foreach (var required in new[] { "PrivacyAudience", "ConversationType", "ConversationScope", "ConversationVisibility",
            "GroupHistoryPolicy", "ConversationParticipantRole", "ConversationAdminPermission", "GroupMemberCapability",
            "MessageKind", "CommunicationAgeCategory", "CommunicationAgeVerificationStatus", "ConversationInvitationStatus",
            "ModerationCaseStatus", "ModerationActionType", "PersonalMessagingAction" })
            Assert.Contains(enumTypes, type => type.Name == required);
        foreach (var type in enumTypes)
        {
            var schema = schemas.GetProperty(type.Name);
            Assert.True(schema.TryGetProperty("type", out var schemaType), type.Name);
            Assert.Equal("string", schemaType.GetString());
            foreach (var value in Enum.GetValues(type))
            {
                var actual = JsonSerializer.SerializeToElement(value, type, mvc);
                Assert.Equal(JsonValueKind.String, actual.ValueKind);
                Assert.Equal(actual.GetRawText(), JsonSerializer.SerializeToElement(value, type, hub).GetRawText());
                if (!type.IsDefined(typeof(FlagsAttribute), false))
                    Assert.Contains(schema.GetProperty("enum").EnumerateArray(), item => item.GetString() == actual.GetString());
            }
        }
    }

    [Fact]
    public async Task NullableMessageKindAndActualPrivacyResponseUseStringContract()
    {
        using var client = factory.CreateClient();
        _ = await CreateMessagingUserAsync(client);
        using var actual = JsonDocument.Parse(await client.GetStringAsync("/api/v1/messaging/privacy"));
        Assert.Equal("Everyone", actual.RootElement.GetProperty("directMessagesAudience").GetString());
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var property = schemas.GetProperty(nameof(MessageReplySummaryResult)).GetProperty("properties").GetProperty("kind");
        var schema = property.TryGetProperty("$ref", out var reference) ? schemas.GetProperty(reference.GetString()!.Split('/')[^1]) : property;
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.True(schema.GetProperty("nullable").GetBoolean());
        var mvc = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var nullableReply = JsonSerializer.SerializeToElement(new MessageReplySummaryResult(Guid.NewGuid(), null, null, null, false, true), mvc);
        Assert.Equal(JsonValueKind.Null, nullableReply.GetProperty("kind").ValueKind);
    }

    [Fact]
    public async Task FlagsContractAllowsRealCommaSeparatedCombinationsWithoutSingleValueEnumRestriction()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var mvc = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        foreach (var value in new object[] { ConversationAdminPermission.ManageMembers | ConversationAdminPermission.PublishMessages,
            GroupMemberCapability.SendMedia | GroupMemberCapability.InviteMembers })
        {
            var type = value.GetType();
            var literal = JsonSerializer.SerializeToElement(value, type, mvc);
            Assert.Contains(", ", literal.GetString());
            var schema = schemas.GetProperty(type.Name);
            Assert.Equal("string", schema.GetProperty("type").GetString());
            if (schema.TryGetProperty("enum", out var singleValues))
                Assert.Contains(singleValues.EnumerateArray(), item => item.GetString() == literal.GetString());
            Assert.Equal(value, JsonSerializer.Deserialize(literal.GetRawText(), type, mvc));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownPermissionNameOrBitsCannotGrantChannelPublishing(bool unknownName)
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var channel = await CreateManagedAndReadAsync(ownerClient, "channels", PersonalCommand("مقدار ناشناخته مجوز نیست", member.UserId));
        using var promote = await ownerClient.PatchAsJsonAsync($"/api/v1/conversations/{channel.ConversationId}/members/{member.UserId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin, ConversationAdminPermission.None), JsonOptions);
        promote.EnsureSuccessStatusCode();
        var current = (await promote.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        var version = current.Participants.Single(p => p.User.UserId == member.UserId).Version;
        using var invalid = await ownerClient.PatchAsJsonAsync($"/api/v1/conversations/{channel.ConversationId}/members/{member.UserId}/permissions",
            new { permissions = unknownName ? (object)"UnknownPrivilege" : 1 << 20, expectedVersion = version });
        Assert.Equal(unknownName ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, invalid.StatusCode);
        var after = (await ownerClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{channel.ConversationId}", JsonOptions))!;
        var participant = after.Participants.Single(p => p.User.UserId == member.UserId);
        Assert.Equal(ConversationAdminPermission.None, participant.AdminPermissions);
        Assert.Equal(version, participant.Version);
        using var denied = await SendMessageAsync(memberClient, channel.ConversationId, Guid.NewGuid(), "بدون مجوز انتشار");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
