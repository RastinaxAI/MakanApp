using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task PeerReceiptRequiresPeerReadNotAcceptedDeliveredOrOwnReadAndSurvivesReconnect()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1 receipt");
        Assert.Equal(MessageReceiptStatus.Sent, sent.Status);
        Assert.Equal(0, await ReadP1PeerSequenceAsync(setup.FirstClient, setup.ConversationId));
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "delivered", sent.Sequence);
        await AdvanceCursorAsync(setup.FirstClient, setup.ConversationId, "read", sent.Sequence);
        Assert.Equal(0, await ReadP1PeerSequenceAsync(setup.FirstClient, setup.ConversationId));
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        var received = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var connection = CreateHubConnection(setup.First.AccessToken))
        {
            connection.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, notification =>
            {
                if (notification.ConversationId == setup.ConversationId && notification.Type.ToString() == "PeerReadStateChanged")
                    received.TrySetResult(notification);
            });
            await connection.StartAsync();
            await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", sent.Sequence);
            var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var json = JsonSerializer.SerializeToElement(notification, JsonOptions);
            Assert.Equal(sent.Sequence, json.GetProperty("peerReadThroughSequence").GetInt64());
            await connection.StopAsync();
        }
        using var reconnect = factory.CreateClient();
        UseBearerToken(reconnect, setup.First.AccessToken);
        Assert.Equal(sent.Sequence, await ReadP1PeerSequenceAsync(reconnect, setup.ConversationId));
        var delta = await GetChangesAsync(reconnect, setup.ConversationId, baseline.NextCursor);
        Assert.Single(delta.Changes, change => change.Type.ToString() == "PeerReadStateChanged");
        Assert.DoesNotContain(delta.Changes, change => change.Type == MessagingChangeType.ReadCursorAdvanced);
    }

    [Fact]
    public async Task PeerReceiptIsMonotonicAndRepeatedReadCreatesNoDuplicatePeerEvent()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var first = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "first receipt");
        var second = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "second receipt");
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", second.Sequence);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", first.Sequence);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", second.Sequence);
        Assert.Equal(second.Sequence, await ReadP1PeerSequenceAsync(setup.FirstClient, setup.ConversationId));
        var delta = await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor);
        Assert.Single(delta.Changes, change => change.Type.ToString() == "PeerReadStateChanged");
        Assert.Equal(0, await ReadP1PeerSequenceAsync(setup.SecondClient, setup.ConversationId));
    }

    [Fact]
    public async Task BlockHidesPeerStateAndPendingReplayWithoutDeletingHistory()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "private receipt");
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", sent.Sequence);
        Assert.Equal(sent.Sequence, await ReadP1PeerSequenceAsync(setup.FirstClient, setup.ConversationId));
        var peerEvent = Assert.Single((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes,
            change => change.Type.ToString() == "PeerReadStateChanged");
        using var blocked = await setup.SecondClient.PostAsync($"/api/v1/messaging/blocks/{setup.First.UserId}", null);
        blocked.EnsureSuccessStatusCode();
        using var response = await setup.FirstClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/read-states");
        response.EnsureSuccessStatusCode();
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hidden", state.GetProperty("visibility").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("peerReadThroughSequence").ValueKind);
        Assert.DoesNotContain((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes,
            change => change.Type.ToString() == "PeerReadStateChanged");
        Assert.Empty(await ReplayP0NotificationAsync(setup.ConversationId, peerEvent));
        Assert.Single((await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages);
    }

    [Fact]
    public async Task HiddenHistoryAndUnrelatedAccountsCannotObtainPeerReceipt()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var outsiderClient = factory.CreateClient();
        await CreateMessagingUserAsync(outsiderClient);
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "receipt hidden history");
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", sent.Sequence);
        using var outsider = await outsiderClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/read-states");
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        using var hidden = await setup.FirstClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/for-me");
        hidden.EnsureSuccessStatusCode();
        Assert.Equal(0, await ReadP1PeerSequenceAsync(setup.FirstClient, setup.ConversationId));
        Assert.DoesNotContain((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes,
            change => change.Type.ToString() == "PeerReadStateChanged");
    }

    private static async Task<long> ReadP1PeerSequenceAsync(HttpClient client, Guid conversationId)
    {
        using var response = await client.GetAsync($"/api/v1/conversations/{conversationId}/read-states");
        response.EnsureSuccessStatusCode();
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Visible", state.GetProperty("visibility").GetString());
        Assert.Equal(conversationId, state.GetProperty("conversationId").GetGuid());
        return state.GetProperty("peerReadThroughSequence").GetInt64();
    }

    [Theory]
    [InlineData("minor")]
    [InlineData("unknown")]
    [InlineData("removed")]
    [InlineData("privacy")]
    public async Task CurrentPeerEligibilityHidesOldReceiptWithoutLeakingPrivateReason(string mutation)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "receipt current policy");
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", sent.Sequence);
        var peerEvent = Assert.Single((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes,
            c => c.Type == MessagingChangeType.PeerReadStateChanged);
        if (mutation is "minor" or "unknown")
            await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId,
                mutation == "minor" ? CommunicationAgeCategory.Minor : CommunicationAgeCategory.Unknown);
        else if (mutation == "privacy")
        {
            using var changed = await setup.SecondClient.PutAsJsonAsync("/api/v1/messaging/privacy",
                new { directMessagesAudience = "Nobody", phoneNumberVisibilityAudience = "Nobody",
                    phoneNumberDiscoveryAudience = "Nobody", groupInvitesAudience = "Contacts", expectedVersion = (string?)null });
            changed.EnsureSuccessStatusCode();
        }
        else
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.ConversationParticipants.SingleAsync(p => p.ConversationId == setup.ConversationId && p.UserId == setup.Second.UserId))
                .Leave(factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
        var state = (await setup.FirstClient.GetFromJsonAsync<ConversationPeerReadStateResult>(
            $"/api/v1/conversations/{setup.ConversationId}/read-states", JsonOptions))!;
        Assert.Equal(PeerReadVisibility.Hidden, state.Visibility);
        Assert.Null(state.PeerReadThroughSequence);
        Assert.Null(state.Version);
        Assert.DoesNotContain((await GetChangesAsync(setup.FirstClient, setup.ConversationId, baseline.NextCursor)).Changes,
            c => c.Type == MessagingChangeType.PeerReadStateChanged);
        Assert.Empty(await ReplayP0NotificationAsync(setup.ConversationId, peerEvent));
    }

    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task GroupAndChannelReadStateDoesNotEnumerateMemberReceipts(string kind)
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var conversation = await CreateManagedAndReadAsync(ownerClient, kind, PersonalCommand("p1 no member receipts", member.UserId));
        var sent = await SendMessageAndReadAsync(ownerClient, conversation.ConversationId, Guid.NewGuid(), "not a member receipt list");
        await AdvanceCursorAsync(memberClient, conversation.ConversationId, "read", sent.Sequence);
        var state = (await ownerClient.GetFromJsonAsync<ConversationPeerReadStateResult>(
            $"/api/v1/conversations/{conversation.ConversationId}/read-states", JsonOptions))!;
        Assert.Equal(PeerReadVisibility.NotSupported, state.Visibility);
        Assert.Null(state.PeerReadThroughSequence);
        Assert.DoesNotContain((await GetChangesAsync(ownerClient, conversation.ConversationId)).Changes,
            c => c.Type == MessagingChangeType.PeerReadStateChanged);
        using var removed = await ownerClient.DeleteAsync($"/api/v1/conversations/{conversation.ConversationId}/members/{member.UserId}");
        removed.EnsureSuccessStatusCode();
        using var denied = await memberClient.GetAsync($"/api/v1/conversations/{conversation.ConversationId}/read-states");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task PeerReadOpenApiMatchesActualNullableSequenceAndVisibility()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/conversations/{conversationId}/read-states", out _));
        Assert.Equal("string", schemas.GetProperty(nameof(PeerReadVisibility)).GetProperty("type").GetString());
        var property = schemas.GetProperty(nameof(ConversationPeerReadStateResult)).GetProperty("properties").GetProperty("peerReadThroughSequence");
        Assert.Equal("integer", property.GetProperty("type").GetString());
        Assert.True(property.GetProperty("nullable").GetBoolean());
    }
}
