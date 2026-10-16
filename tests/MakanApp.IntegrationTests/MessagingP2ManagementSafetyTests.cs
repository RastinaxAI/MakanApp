using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2ManagementAdultCannotUseLinkToBypassExistingProtectedGroupAudience(CommunicationAgeCategory age)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); using var strangerClient = factory.CreateClient(); var stranger = await CreateMessagingUserAsync(strangerClient);
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, age);
        await InviteAndAcceptPersonalAsync(setup.OwnerClient, setup.TargetClient, setup.Conversation.ConversationId, setup.Target.UserId);
        await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, stranger.UserId);
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand());
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemP2Async(strangerClient, link)).StatusCode);
        Assert.DoesNotContain(await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId), p => p.UserId == stranger.UserId && p.Status == ConversationParticipantStatus.Active);
        Assert.Equal(0, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id)).UsedCount);
    }

    [Fact]
    public async Task P2ManagementLinkJoinHonorsFromJoinAcrossHistorySearchAndDelta()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups");
        var settings = new UpdateConversationSettingsCommand(ConversationVisibility.Private, GroupHistoryPolicy.FromJoin, GroupMemberCapability.LegacyMemberDefault, setup.Conversation.Version);
        (await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/settings", settings, JsonOptions)).EnsureSuccessStatusCode();
        var old = await SendMessageAndReadAsync(setup.OwnerClient, setup.Conversation.ConversationId, Guid.NewGuid(), "p2-before-link-join");
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand());
        (await RedeemP2Async(setup.TargetClient, link)).EnsureSuccessStatusCode();
        var current = await SendMessageAndReadAsync(setup.OwnerClient, setup.Conversation.ConversationId, Guid.NewGuid(), "p2-after-link-join");
        Assert.Equal(current.MessageId, Assert.Single((await setup.TargetClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.Conversation.ConversationId}/messages", JsonOptions))!.Messages).MessageId);
        Assert.Empty((await SearchAsync(setup.TargetClient, "p2-before-link-join")).Items);
        Assert.DoesNotContain((await GetChangesAsync(setup.TargetClient, setup.Conversation.ConversationId)).Changes, c => c.ResourceId == old.MessageId);
    }

    [Fact]
    public async Task P2ManagementBanPreventsAcceptanceOfPreviouslyPendingTargetedInvitation()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
        var invited = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/invitations",
            new CreateConversationInvitationCommand(setup.Target.UserId, DateTime.UtcNow.AddHours(1)), JsonOptions);
        invited.EnsureSuccessStatusCode(); var invitation = (await invited.Content.ReadFromJsonAsync<ConversationInvitationResult>(JsonOptions))!;
        await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new SetMemberRestrictionCommand(true, GroupMemberCapability.None, null, "ممنوعیت پس از دعوت", null));
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PostAsync($"/api/v1/messaging/invitations/{invitation.Id}/accept", null)).StatusCode);
        Assert.DoesNotContain(await factory.GetConversationParticipantsAsync(id), p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active);
    }

    [Fact]
    public async Task P2ManagementRestrictionReplacementUsesRowVersionAcrossDevices()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        var initial = await RestrictP2MemberAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId,
            new SetMemberRestrictionCommand(false, GroupMemberCapability.SendMedia, null, "محدودیت رسانه", null));
        var cleared = await RestrictP2MemberAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId,
            new SetMemberRestrictionCommand(false, GroupMemberCapability.None, null, "رفع محدودیت", initial.Version));
        Assert.NotEqual(initial.Version, cleared.Version);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await setup.OwnerClient.PutAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{setup.Target.UserId}/restriction",
            new SetMemberRestrictionCommand(true, GroupMemberCapability.None, null, "نسخه قدیمی", initial.Version), JsonOptions)).StatusCode);
        var list = (await setup.OwnerClient.GetFromJsonAsync<MemberRestrictionPageResult>($"/api/v1/conversations/{setup.Conversation.ConversationId}/member-restrictions", JsonOptions))!;
        Assert.False(Assert.Single(list.Items).IsEffective);
    }

    [Fact]
    public async Task P2ManagementExpiredRequestStaysTerminalAndCannotBeApprovedOrRewritten()
    {
        var start = DateTime.UtcNow; factory.Clock.SetUtcNow(start);
        try
        {
            using var setup = await CreateP2ManagedSetupAsync("groups");
            var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true, ExpiresAtUtc = start.AddMinutes(1) });
            var request = (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!;
            factory.Clock.SetUtcNow(start.AddMinutes(2));
            var own = (await setup.TargetClient.GetFromJsonAsync<ConversationJoinRequestPageResult>("/api/v1/messaging/join-requests", JsonOptions))!;
            Assert.Equal(ConversationJoinRequestStatus.Expired, Assert.Single(own.Items).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await CompleteP2RequestAsync(setup.OwnerClient, request, "approve")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await CompleteP2RequestAsync(setup.TargetClient, request, "cancel")).StatusCode);
            Assert.Equal(0, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, link.Link.Id)).UsedCount);
        }
        finally { factory.Clock.UseSystemTime(); }
    }
}
