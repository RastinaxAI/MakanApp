using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    private static Task<HttpResponseMessage> CreateManagedAsync(HttpClient client, string resource, CreateManagedConversationCommand command) =>
        client.PostAsJsonAsync($"/api/v1/conversations/{resource}", command, JsonOptions);

    private static async Task InviteAndAcceptPersonalAsync(HttpClient inviter, HttpClient target, Guid conversationId, Guid targetId)
    {
        using var invite = await inviter.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/invitations",
            new CreateConversationInvitationCommand(targetId, DateTime.UtcNow.AddDays(1)), JsonOptions);
        invite.EnsureSuccessStatusCode();
        var invitation = (await invite.Content.ReadFromJsonAsync<ConversationInvitationResult>(JsonOptions))!;
        using var accept = await target.PostAsync($"/api/v1/messaging/invitations/{invitation.Id}/accept", null);
        accept.EnsureSuccessStatusCode();
    }
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task ApprovedPolicyProtectedAccountCannotBeGloballyDiscoveredOrContactedByStranger(CommunicationAgeCategory age)
    {
        using var actorClient = factory.CreateClient(); using var targetClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient); var target = await CreateMessagingUserAsync(targetClient);
        await factory.SetCommunicationAgeCategoryAsync(target.UserId, age);
        using var profile = await actorClient.GetAsync($"/api/v1/people/{target.UserId}");
        var search = await actorClient.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={target.Username}", JsonOptions);
        var resolve = await actorClient.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(target.Username, null), JsonOptions);
        var result = await resolve.Content.ReadFromJsonAsync<PeopleResolveResult>(JsonOptions);
        using var direct = await StartDirectAsync(actorClient, target.UserId, ConversationScope.Personal);
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode); Assert.Empty(search!.Items);
        Assert.False(result!.IsDiscoverable); Assert.Null(result.Profile);
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
        Assert.Equal(MessagingErrorCodes.DirectRecipientNotAvailable, await ReadProblemCodeAsync(direct));
        Assert.DoesNotContain(age.ToString(), await direct.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task ApprovedContactAllowsScopedProfileAndDirectButRevocationStopsFutureSendNotHistory(CommunicationAgeCategory age)
    {
        using var actorClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient); var child = await CreateMessagingUserAsync(childClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, age);
        await factory.AllowPersonalGroupInviteAsync(actor.UserId, child.UserId);
        var profile = await actorClient.GetFromJsonAsync<MessagingPublicProfileResult>($"/api/v1/people/{child.UserId}", JsonOptions);
        Assert.Null(profile!.PhoneNumber); Assert.True(profile.CanStartDirect);
        var direct = await StartDirectAndReadAsync(actorClient, child.UserId, ConversationScope.Personal);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        await SendMessageAndReadAsync(actorClient, direct.ConversationId, Guid.NewGuid(), "پیام مجاز");
        await factory.RemoveApprovedContactAsync(child.UserId, actor.UserId);
        using var denied = await SendMessageAsync(actorClient, direct.ConversationId, Guid.NewGuid(), "شناسه قدیمی مجوز نیست");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var history = await actorClient.GetAsync($"/api/v1/conversations/{direct.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(direct.ConversationId));
    }

    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task ProtectedAccountCanCreatePrivateGroupButNotPublicGroupOrPersonalChannel(CommunicationAgeCategory age)
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await factory.SetCommunicationAgeCategoryAsync(user.UserId, age);
        using var privateGroup = await CreateManagedAsync(client, "groups", PersonalCommand("خصوصی مجاز"));
        using var publicGroup = await CreateManagedAsync(client, "groups", PersonalCommand("عمومی ممنوع") with { Visibility = ConversationVisibility.Public });
        using var channel = await CreateManagedAsync(client, "channels", PersonalCommand("کانال ممنوع"));
        Assert.Equal(HttpStatusCode.Created, privateGroup.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, publicGroup.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, channel.StatusCode);
    }

    [Fact]
    public async Task ProtectedGroupMembershipRequiresConsentAndCurrentRelationship()
    {
        using var ownerClient = factory.CreateClient(); using var childClient = factory.CreateClient(); using var hostileClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var child = await CreateMessagingUserAsync(childClient);
        _ = await CreateMessagingUserAsync(hostileClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Minor);
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", PersonalCommand("دعوت رضایتی"));
        using var strangerInvite = await ownerClient.PostAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/invitations",
            new CreateConversationInvitationCommand(child.UserId, DateTime.UtcNow.AddDays(1)), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, strangerInvite.StatusCode);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, child.UserId);
        using var instantAdd = await ownerClient.PostAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/members", new AddConversationMemberCommand(child.UserId), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, instantAdd.StatusCode);
        using var invited = await ownerClient.PostAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/invitations",
            new CreateConversationInvitationCommand(child.UserId, DateTime.UtcNow.AddDays(1)), JsonOptions);
        invited.EnsureSuccessStatusCode();
        var invitation = (await invited.Content.ReadFromJsonAsync<ConversationInvitationResult>(JsonOptions))!;
        using var beforeAccept = await childClient.GetAsync($"/api/v1/conversations/{group.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, beforeAccept.StatusCode);
        using var hostileAccept = await hostileClient.PostAsync($"/api/v1/messaging/invitations/{invitation.Id}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, hostileAccept.StatusCode);
        using var accepted = await childClient.PostAsync($"/api/v1/messaging/invitations/{invitation.Id}/accept", null);
        accepted.EnsureSuccessStatusCode();
        using var retried = await childClient.PostAsync($"/api/v1/messaging/invitations/{invitation.Id}/accept", null);
        retried.EnsureSuccessStatusCode();
        Assert.Single(await factory.GetConversationParticipantsAsync(group.ConversationId), p => p.UserId == child.UserId && p.Status == ConversationParticipantStatus.Active);
        await factory.RemoveApprovedContactAsync(child.UserId, owner.UserId);
        using var send = await SendMessageAsync(ownerClient, group.ConversationId, Guid.NewGuid(), "گروه راه دور زدن نیست");
        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
    }

    [Fact]
    public async Task AgeChangeReevaluatesOldDirectWithoutDeletingCommittedHistory()
    {
        using var firstClient = factory.CreateClient(); using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient); var second = await CreateMessagingUserAsync(secondClient);
        var direct = await StartDirectAndReadAsync(firstClient, second.UserId, ConversationScope.Personal);
        await SendMessageAndReadAsync(firstClient, direct.ConversationId, Guid.NewGuid(), "قبل از تغییر");
        await factory.SetCommunicationAgeCategoryAsync(second.UserId, CommunicationAgeCategory.Minor);
        using var send = await SendMessageAsync(firstClient, direct.ConversationId, Guid.NewGuid(), "بعد از تغییر");
        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
        using var read = await firstClient.GetAsync($"/api/v1/conversations/{direct.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(direct.ConversationId));
    }

    [Theory]
    [InlineData(ContentSafetyDecision.Allowed, HttpStatusCode.OK, 1)]
    [InlineData(ContentSafetyDecision.Blocked, HttpStatusCode.UnprocessableEntity, 0)]
    [InlineData(ContentSafetyDecision.ReviewRequired, HttpStatusCode.UnprocessableEntity, 0)]
    [InlineData(ContentSafetyDecision.Unavailable, HttpStatusCode.ServiceUnavailable, 0)]
    public async Task ProtectedSendUsesDurableSafetyOutcome(ContentSafetyDecision decision, HttpStatusCode status, int messageCount)
    {
        using var firstClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient); var child = await CreateMessagingUserAsync(childClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(first.UserId, child.UserId);
        var direct = await StartDirectAndReadAsync(firstClient, child.UserId, ConversationScope.Personal);
        factory.ContentSafetyProvider.Configure(decision);
        using var send = await SendMessageAsync(firstClient, direct.ConversationId, Guid.NewGuid(), "محتوای خصوصی تست");
        Assert.Equal(status, send.StatusCode); Assert.Equal(messageCount, (await factory.GetMessagesAsync(direct.ConversationId)).Length);
        if (decision != ContentSafetyDecision.Allowed) Assert.DoesNotContain("محتوای خصوصی تست", await send.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BlockCommittedDuringSafetyEvaluationStopsFinalSendAndOldIdDoesNotBypass()
    {
        using var firstClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient); var child = await CreateMessagingUserAsync(childClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(first.UserId, child.UserId);
        var direct = await StartDirectAndReadAsync(firstClient, child.UserId, ConversationScope.Personal);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () =>
        {
            using var blocked = await childClient.PostAsync($"/api/v1/messaging/blocks/{first.UserId}", null);
            blocked.EnsureSuccessStatusCode();
        });
        using var send = await SendMessageAsync(firstClient, direct.ConversationId, Guid.NewGuid(), "نباید commit شود");
        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode); Assert.Empty(await factory.GetMessagesAsync(direct.ConversationId));
    }

    [Theory]
    [InlineData("groups", ConversationVisibility.Public)]
    [InlineData("channels", ConversationVisibility.Private)]
    public async Task AgeChangeReevaluatesExistingPublicGroupAndChannelPublicationWithoutDeletingHistory(string resource, ConversationVisibility visibility)
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        var conversation = await CreateManagedAndReadAsync(client, resource, PersonalCommand("تغییر سن مالک") with { Visibility = visibility });
        await SendMessageAndReadAsync(client, conversation.ConversationId, Guid.NewGuid(), "تاریخچه معتبر قبلی");
        await factory.SetCommunicationAgeCategoryAsync(owner.UserId, CommunicationAgeCategory.Unknown);
        using var denied = await SendMessageAsync(client, conversation.ConversationId, Guid.NewGuid(), "اختیار قبلی دائمی نیست");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var history = await client.GetAsync($"/api/v1/conversations/{conversation.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode); Assert.Single(await factory.GetMessagesAsync(conversation.ConversationId));
    }
}
