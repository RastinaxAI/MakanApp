using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2PollRevokedProtectedRelationshipStopsNewVoteWithoutErasingHistory(CommunicationAgeCategory age)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, age);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await factory.RemoveApprovedContactAsync(setup.Second.UserId, setup.First.UserId);
        var vote = await setup.SecondClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, receipt.MessageId) + "/vote", new VoteConversationPollCommand([1]), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, vote.StatusCode); Assert.Equal(0, (await ReadP2PollAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId)).TotalVoters);
    }
    [Fact]
    public async Task P2PollChannelSubscriberMayVoteButCannotPublishOrCloseOthers()
    {
        using var setup = await CreateP2ManagedSetupAsync("channels", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var message = await CreateP2PollAsync(setup.OwnerClient, id);
        await VoteP2PollAsync(setup.TargetClient, id, message.MessageId, [1]);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PostAsJsonAsync($"/api/v1/conversations/{id}/polls", P2Poll(), JsonOptions)).StatusCode);
        var current = await ReadP2PollAsync(setup.TargetClient, id, message.MessageId);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PostAsJsonAsync(P2PollRoute(id, message.MessageId) + "/close", new CloseConversationPollCommand(current.Version), JsonOptions)).StatusCode);
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.DeleteMessages);
        Assert.Equal(ConversationPollStatus.Closed, (await CloseP2PollAsync(setup.TargetClient, current)).Status);
    }
    [Fact]
    public async Task P2PollRemovedMemberCannotReadVoteOrListVotersAndRevokedSessionIsUnauthorized()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var message = await CreateP2PollAsync(setup.OwnerClient, id, P2Poll(anonymous: false));
        (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
        var route = P2PollRoute(id, message.MessageId);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(route + "/voters")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.PostAsJsonAsync(route + "/vote", new VoteConversationPollCommand([1]), JsonOptions)).StatusCode);
        await factory.RevokeSessionByTokenAsync(setup.Owner.AccessToken); Assert.Equal(HttpStatusCode.Unauthorized, (await setup.OwnerClient.GetAsync(route)).StatusCode);
    }
    [Fact]
    public async Task P2PollBlockStopsNewVoteButDoesNotDeleteAuthorizedHistoricalRead()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        (await setup.FirstClient.PostAsync($"/api/v1/messaging/blocks/{setup.Second.UserId}", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.SecondClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/vote", new VoteConversationPollCommand([1]), JsonOptions)).StatusCode);
        Assert.Equal(0, (await ReadP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId)).TotalVoters);
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
    }
    [Fact]
    public async Task P2PollIndividualRestrictionStopsVotingWithoutRemovingHistory()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var message = await CreateP2PollAsync(setup.OwnerClient, id);
        await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new(false, GroupMemberCapability.SendMessages, null, "توقف تعامل", null));
        var response = await setup.TargetClient.PostAsJsonAsync(P2PollRoute(id, message.MessageId) + "/vote", new VoteConversationPollCommand([1]), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(MessagingErrorCodes.MemberRestricted, await ReadProblemCodeAsync(response));
        Assert.Equal(0, (await ReadP2PollAsync(setup.TargetClient, id, message.MessageId)).TotalVoters);
    }
    [Fact]
    public async Task P2PollFromJoinAndPersonalClearCannotLeakOldPollOrItsDelta()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: false); var id = setup.Conversation.ConversationId;
        (await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/settings", new UpdateConversationSettingsCommand(ConversationVisibility.Private, GroupHistoryPolicy.FromJoin, GroupMemberCapability.LegacyMemberDefault, setup.Conversation.Version), JsonOptions)).EnsureSuccessStatusCode();
        var message = await CreateP2PollAsync(setup.OwnerClient, id); await VoteP2PollAsync(setup.OwnerClient, id, message.MessageId, [1]);
        (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/members", new AddConversationMemberCommand(setup.Target.UserId), JsonOptions)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(P2PollRoute(id, message.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.TargetClient, id)).Changes, c => c.ResourceId == message.MessageId);
        (await setup.OwnerClient.PostAsync($"/api/v1/conversations/{id}/clear-history", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OwnerClient.GetAsync(P2PollRoute(id, message.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.OwnerClient, id)).Changes, c => c.Type == MessagingChangeType.PollChanged);
    }
    [Fact]
    public async Task P2PollDeleteForMeConcealsDetailsVotesAndRealtimeDeltaOnlyForThatUser()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/for-me")).EnsureSuccessStatusCode();
        await VoteP2PollAsync(setup.FirstClient, setup.ConversationId, message.MessageId, [1]);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.GetAsync(P2PollRoute(setup.ConversationId, message.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.SecondClient, setup.ConversationId)).Changes, c => c.ResourceId == message.MessageId && c.Type != MessagingChangeType.MessageHiddenForUser);
        Assert.Equal(1, (await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, message.MessageId)).TotalVoters);
    }
    [Fact]
    public async Task P2PollTopicMustBelongToSameConversationAndBeOpenForCreation()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var topic = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "رأی‌گیری موضوعی"));
        var command = P2Poll() with { TopicId = topic.TopicId }; var message = await CreateP2PollAsync(setup.OwnerClient, id, command);
        await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed);
        Assert.Equal(message.MessageId, (await CreateP2PollAsync(setup.OwnerClient, id, command)).MessageId);
        Assert.Equal(HttpStatusCode.Conflict, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/polls", command with { ClientMessageId = Guid.NewGuid() }, JsonOptions)).StatusCode);
        using var other = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        Assert.Equal(HttpStatusCode.NotFound, (await other.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{other.Conversation.ConversationId}/polls", P2Poll() with { TopicId = topic.TopicId }, JsonOptions)).StatusCode);
    }
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2PollProtectedAudienceSafetyReviewsAllOptionsAndFailsClosed(CommunicationAgeCategory age)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, age); await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        factory.ContentSafetyProvider.ConfigureUnavailable();
        try
        {
            var command = P2Poll(quiz: true); var response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/polls", command, JsonOptions);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked);
            response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/polls", command, JsonOptions); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var reviewed = Assert.Single(factory.ContentSafetyProvider.Requests); Assert.All(command.Options, o => Assert.Contains(o, reviewed.Text)); Assert.Contains(command.Explanation!, reviewed.Text);
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.False(await db.ConversationPolls.AnyAsync(p => p.ConversationId == setup.ConversationId)); Assert.True(await db.SafetyIncidents.AnyAsync(i => i.ConversationId == setup.ConversationId));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P2PollCurrentPermissionOrSessionRevokedDuringSafetyCannotCommit(bool revokeSession)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, CommunicationAgeCategory.Minor);
        var invoked = false; factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () =>
        { invoked = true; if (revokeSession) await factory.RevokeSessionByTokenAsync(setup.Target.AccessToken); else await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new(false, GroupMemberCapability.SendMessages, null, "لغو حین بررسی", null)); });
        try
        {
            var response = await setup.TargetClient.PostAsJsonAsync($"/api/v1/conversations/{id}/polls", P2Poll(), JsonOptions);
            Assert.True(invoked); Assert.Equal(revokeSession ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode); Assert.Empty(await factory.GetMessagesAsync(id));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }
}
