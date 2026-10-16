using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task P2ManagementLinkUsesHashAndExplicitAcceptanceWithoutDuplicateUse(string resource)
    {
        using var setup = await CreateP2ManagedSetupAsync(resource);
        var command = P2LinkCommand(); var created = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, command);
        Assert.NotNull(created.Token); Assert.Equal(43, created.Token.Length);
        var repeated = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, command);
        Assert.True(repeated.AlreadyExisted); Assert.Null(repeated.Token); Assert.Equal(created.Link.Id, repeated.Link.Id);
        var listing = await setup.OwnerClient.GetStringAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/invite-links");
        Assert.DoesNotContain(created.Token, listing); Assert.DoesNotContain("tokenHash", listing, StringComparison.OrdinalIgnoreCase);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var stored = await db.ConversationInviteLinks.AsNoTracking().SingleAsync(l => l.Id == created.Link.Id);
            Assert.Equal(64, stored.TokenHash.Length); Assert.DoesNotContain(created.Token, JsonSerializer.Serialize(stored));
            var audit = await db.AdminAuditLogs.AsNoTracking().Where(a => a.TargetId == created.Link.Id.ToString()).ToArrayAsync();
            Assert.NotEmpty(audit); Assert.DoesNotContain(created.Token, JsonSerializer.Serialize(audit));
        }
        Assert.DoesNotContain(await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId), p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active);
        (await RedeemP2Async(setup.TargetClient, created)).EnsureSuccessStatusCode();
        var retry = await RedeemP2Async(setup.TargetClient, created); retry.EnsureSuccessStatusCode();
        Assert.True((await retry.Content.ReadFromJsonAsync<InviteLinkRedemptionResult>(JsonOptions))!.AlreadyMember);
        Assert.Equal(1, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, created.Link.Id)).UsedCount);
        Assert.Single(await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId), p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active);
        if (resource == "channels") Assert.Equal(HttpStatusCode.Forbidden,
            (await SendMessageAsync(setup.TargetClient, setup.Conversation.ConversationId, Guid.NewGuid(), "subscriber publish denied")).StatusCode);
    }

    [Fact]
    public async Task P2ManagementExpiredLinkRejectsJoinUsingServerClock()
    {
        var start = DateTime.UtcNow; factory.Clock.SetUtcNow(start);
        try
        {
            using var setup = await CreateP2ManagedSetupAsync("groups");
            var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { ExpiresAtUtc = start.AddMinutes(1) });
            factory.Clock.SetUtcNow(start.AddMinutes(2));
            Assert.Equal(HttpStatusCode.NotFound, (await RedeemP2Async(setup.TargetClient, link)).StatusCode);
            Assert.Equal(0, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id)).UsedCount);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task P2ManagementRevocationBlocksFutureJoinButDoesNotRemoveExistingMember()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); using var other = factory.CreateClient(); var user = await CreateMessagingUserAsync(other);
        await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, user.UserId);
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand());
        (await RedeemP2Async(setup.TargetClient, link)).EnsureSuccessStatusCode();
        var current = await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id);
        (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/invite-links/{current.Id}/revoke",
            new RevokeInviteLinkCommand(current.Version), JsonOptions)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemP2Async(other, link)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/messages")).StatusCode);
    }

    [Fact]
    public async Task P2ManagementConcurrentLastLinkUseHasExactlyOneNewMember()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); using var other = factory.CreateClient(); var user = await CreateMessagingUserAsync(other);
        await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, user.UserId);
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { MaximumUses = 1 });
        var responses = await Task.WhenAll(RedeemP2Async(setup.TargetClient, link), RedeemP2Async(other, link));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NotFound); }
        finally { foreach (var response in responses) response.Dispose(); }
        var participants = await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId);
        Assert.Equal(2, participants.Count(p => p.Status == ConversationParticipantStatus.Active)); Assert.Equal(1, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id)).UsedCount);
    }

    [Fact]
    public async Task P2ManagementPendingRequestIsNotMembershipAndDuplicateApprovalHasOneEffect()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups");
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true });
        var requested = await ReadP2RedemptionAsync(setup.TargetClient, link); var duplicate = await ReadP2RedemptionAsync(setup.TargetClient, link);
        Assert.Equal(requested.JoinRequest!.Id, duplicate.JoinRequest!.Id); Assert.Equal(ConversationJoinRequestStatus.Pending, requested.JoinRequest.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/messages")).StatusCode);
        Assert.Equal(0, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id)).UsedCount);
        var own = (await setup.TargetClient.GetFromJsonAsync<ConversationJoinRequestPageResult>("/api/v1/messaging/join-requests", JsonOptions))!;
        Assert.Equal(requested.JoinRequest.Id, Assert.Single(own.Items).Id);
        var approved = await CompleteP2RequestAsync(setup.OwnerClient, requested.JoinRequest, "approve"); approved.EnsureSuccessStatusCode();
        (await CompleteP2RequestAsync(setup.OwnerClient, requested.JoinRequest, "approve")).EnsureSuccessStatusCode();
        Assert.Equal(1, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id)).UsedCount);
        Assert.Equal(HttpStatusCode.OK, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/messages")).StatusCode);
    }

    [Fact]
    public async Task P2ManagementApprovalRacingWithRevocationProducesConsistentMembershipAndUse()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups");
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true, MaximumUses = 1 });
        var request = (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!;
        var responses = await Task.WhenAll(CompleteP2RequestAsync(setup.OwnerClient, request, "approve"),
            setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/invite-links/{link.Link.Id}/revoke", new RevokeInviteLinkCommand(link.Link.Version), JsonOptions));
        try
        {
            var joined = responses[0].StatusCode == HttpStatusCode.OK;
            Assert.Equal(joined ? HttpStatusCode.PreconditionFailed : HttpStatusCode.OK, responses[1].StatusCode);
            if (!joined) Assert.Equal(HttpStatusCode.NotFound, responses[0].StatusCode);
            var current = await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id);
            Assert.Equal(joined ? 1 : 0, current.UsedCount);
            Assert.Equal(joined, (await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId)).Any(p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2ManagementProtectedJoinRequiresRelationAndRechecksItAtApproval(CommunicationAgeCategory age)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, age);
        await factory.RemoveApprovedContactAsync(setup.Target.UserId, setup.Owner.UserId);
        var privacy = (await setup.TargetClient.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
        (await setup.TargetClient.PutAsJsonAsync("/api/v1/messaging/privacy", new UpdateMessagingPrivacyCommand(privacy.DirectMessagesAudience,
            privacy.PhoneNumberVisibilityAudience, privacy.PhoneNumberDiscoveryAudience, PrivacyAudience.Everyone, privacy.Version), JsonOptions)).EnsureSuccessStatusCode();
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true });
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemP2Async(setup.TargetClient, link)).StatusCode);
        await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, setup.Target.UserId);
        var request = (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!;
        await factory.RemoveApprovedContactAsync(setup.Target.UserId, setup.Owner.UserId);
        Assert.Equal(HttpStatusCode.NotFound, (await CompleteP2RequestAsync(setup.OwnerClient, request, "approve")).StatusCode);
        Assert.DoesNotContain(await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId), p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active);
    }

    private CreateInviteLinkCommand P2LinkCommand() => new(Guid.NewGuid(), factory.Clock.GetUtcNow().UtcDateTime.AddHours(1), 10, false);
    private static async Task<CreatedInviteLinkResult> CreateP2LinkAsync(HttpClient client, Guid conversationId, CreateInviteLinkCommand command)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/invite-links", command, JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<CreatedInviteLinkResult>(JsonOptions))!;
    }
    private static Task<HttpResponseMessage> RedeemP2Async(HttpClient client, CreatedInviteLinkResult link) =>
        client.PostAsJsonAsync("/api/v1/messaging/invite-links/redeem", new RedeemInviteLinkCommand(link.Token!), JsonOptions);
    private static async Task<InviteLinkRedemptionResult> ReadP2RedemptionAsync(HttpClient client, CreatedInviteLinkResult link)
    {
        using var response = await RedeemP2Async(client, link); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InviteLinkRedemptionResult>(JsonOptions))!;
    }
    private static async Task<InviteLinkResult> ReadP2LinkAsync(HttpClient client, Guid conversationId, Guid linkId) =>
        Assert.Single((await client.GetFromJsonAsync<InviteLinkPageResult>($"/api/v1/conversations/{conversationId}/invite-links", JsonOptions))!.Items, l => l.Id == linkId);
    private static Task<HttpResponseMessage> CompleteP2RequestAsync(HttpClient client, ConversationJoinRequestResult request, string action) =>
        client.PostAsJsonAsync($"/api/v1/messaging/join-requests/{request.Id}/{action}", new CompleteJoinRequestCommand(request.Version), JsonOptions);
    private async Task<P2ManagedSetup> CreateP2ManagedSetupAsync(string resource, bool initiallyMember = false, bool isPublic = false)
    {
        var ownerClient = factory.CreateClient(); var targetClient = factory.CreateClient();
        try
        {
            var owner = await CreateMessagingUserAsync(ownerClient); var target = await CreateMessagingUserAsync(targetClient);
            await factory.AllowPersonalGroupInviteAsync(owner.UserId, target.UserId);
            var conversation = await CreateManagedAndReadAsync(ownerClient, resource, PersonalCommand("مدیریت پیشرفته", initiallyMember ? [target.UserId] : []) with {
                Visibility = isPublic ? ConversationVisibility.Public : ConversationVisibility.Private });
            return new(ownerClient, targetClient, owner, target, conversation);
        }
        catch { ownerClient.Dispose(); targetClient.Dispose(); throw; }
    }
    private sealed record P2ManagedSetup(HttpClient OwnerClient, HttpClient TargetClient, MessagingUser Owner, MessagingUser Target, ManagedConversationResult Conversation) : IDisposable
    {
        public void Dispose() { OwnerClient.Dispose(); TargetClient.Dispose(); }
    }
}
