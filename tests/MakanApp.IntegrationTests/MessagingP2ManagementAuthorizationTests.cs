using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2ManagementInviteCapabilityDoesNotGrantPromotionInfoOrChannelPublication()
    {
        using var setup = await CreateP2ManagedSetupAsync("channels", initiallyMember: true);
        using var otherClient = factory.CreateClient(); var other = await CreateMessagingUserAsync(otherClient);
        await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, other.UserId);
        (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members", new AddConversationMemberCommand(other.UserId), JsonOptions)).EnsureSuccessStatusCode();
        var promoted = await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId, ConversationAdminPermission.ManageInvites);
        var link = await CreateP2LinkAsync(setup.TargetClient, setup.Conversation.ConversationId, P2LinkCommand()); Assert.NotNull(link.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/info",
            new UpdateConversationInfoCommand("دسترسی اضافه نداریم", null, promoted.Version), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{other.UserId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendMessageAsync(setup.TargetClient, setup.Conversation.ConversationId, Guid.NewGuid(), "cannot publish")).StatusCode);
    }

    [Fact]
    public async Task P2ManagementPermissionRevokeRacingLinkCreationCannotLeaveUsableUnauthorizedLink()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); using var guestClient = factory.CreateClient(); var guest = await CreateMessagingUserAsync(guestClient);
        await factory.AllowPersonalGroupInviteAsync(setup.Target.UserId, guest.UserId);
        var promoted = await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId, ConversationAdminPermission.ManageInvites);
        var version = promoted.Participants.Single(p => p.User.UserId == setup.Target.UserId).Version;
        var responses = await Task.WhenAll(setup.TargetClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/invite-links", P2LinkCommand(), JsonOptions),
            setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{setup.Target.UserId}/permissions",
                new UpdateConversationPermissionsCommand(ConversationAdminPermission.None, version), JsonOptions));
        try
        {
            Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden });
            if (responses[0].StatusCode == HttpStatusCode.OK)
            {
                var link = (await responses[0].Content.ReadFromJsonAsync<CreatedInviteLinkResult>(JsonOptions))!;
                Assert.Equal(HttpStatusCode.NotFound, (await RedeemP2Async(guestClient, link)).StatusCode);
            }
            Assert.DoesNotContain(await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId), p => p.UserId == guest.UserId && p.Status == ConversationParticipantStatus.Active);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task P2ManagementInfoRequiresDedicatedPermissionAndRejectsStaleVersion()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        var current = await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId, ConversationAdminPermission.ChangeInfo);
        var response = await setup.TargetClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/info",
            new UpdateConversationInfoCommand("عنوان جدید", "توضیح جدید", current.Version), JsonOptions);
        response.EnsureSuccessStatusCode(); var changed = (await response.Content.ReadFromJsonAsync<ConversationInfoResult>(JsonOptions))!;
        Assert.Equal("عنوان جدید", changed.Title); Assert.NotEqual(current.Version, changed.Version);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/info",
            new UpdateConversationInfoCommand("نسخه قدیمی", null, current.Version), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/invite-links", P2LinkCommand(), JsonOptions)).StatusCode);
    }

    [Fact]
    public async Task P2ManagementRestrictionStopsCurrentSendAndTypingButExpiresWithoutDeletingHistory()
    {
        var start = DateTime.UtcNow; factory.Clock.SetUtcNow(start);
        try
        {
            using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
            await SendMessageAndReadAsync(setup.OwnerClient, setup.Conversation.ConversationId, Guid.NewGuid(), "تاریخچه باقی می‌ماند");
            var restricted = await RestrictP2MemberAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId,
                new SetMemberRestrictionCommand(false, GroupMemberCapability.SendMessages, start.AddSeconds(20), "محدودیت کوتاه آزمایشی", null));
            Assert.True(restricted.IsEffective);
            var denied = await SendMessageAsync(setup.TargetClient, setup.Conversation.ConversationId, Guid.NewGuid(), "restricted");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(MessagingErrorCodes.MemberRestricted, await ReadProblemCodeAsync(denied));
            await using var hub = CreateHubConnection(setup.Target.AccessToken); await StartP2HubAsync(hub); await hub.InvokeAsync("SubscribeConversation", setup.Conversation.ConversationId);
            Assert.Contains(MessagingErrorCodes.MemberRestricted, (await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("SetTyping", setup.Conversation.ConversationId, true))).Message);
            Assert.Equal(HttpStatusCode.OK, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/messages")).StatusCode);
            factory.Clock.SetUtcNow(start.AddSeconds(21));
            await SendMessageAndReadAsync(setup.TargetClient, setup.Conversation.ConversationId, Guid.NewGuid(), "پس از انقضا");
            Assert.Equal(2, (await factory.GetMessagesAsync(setup.Conversation.ConversationId)).Count());
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task P2ManagementBanBlocksAllJoinPathsAndUnbanNeverAutomaticallyReactivatesMembership()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true, isPublic: true);
        var id = setup.Conversation.ConversationId; var link = await CreateP2LinkAsync(setup.OwnerClient, id, P2LinkCommand());
        var banned = await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new SetMemberRestrictionCommand(true, GroupMemberCapability.None, null, "ممنوعیت آزمایشی", null));
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PostAsync($"/api/v1/conversations/{id}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RedeemP2Async(setup.TargetClient, link)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/members", new AddConversationMemberCommand(setup.Target.UserId), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/invitations",
            new CreateConversationInvitationCommand(setup.Target.UserId, DateTime.UtcNow.AddHours(1)), JsonOptions)).StatusCode);
        var unbanned = await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new SetMemberRestrictionCommand(false, GroupMemberCapability.None, null, "رفع محدودیت", banned.Version));
        Assert.False(unbanned.IsEffective); Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{id}/messages")).StatusCode);
        (await RedeemP2Async(setup.TargetClient, link)).EnsureSuccessStatusCode();
        Assert.Single(await factory.GetConversationParticipantsAsync(id), p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active);
    }

    [Fact]
    public async Task P2ManagementAdminCannotRestrictOwnerOrOtherAdminWithoutManageAdmins()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); using var otherClient = factory.CreateClient(); var other = await CreateMessagingUserAsync(otherClient);
        await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, other.UserId);
        (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members", new AddConversationMemberCommand(other.UserId), JsonOptions)).EnsureSuccessStatusCode();
        await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId, ConversationAdminPermission.BanMembers);
        await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, other.UserId, ConversationAdminPermission.None);
        foreach (var targetId in new[] { setup.Owner.UserId, other.UserId, setup.Target.UserId })
            Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PutAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{targetId}/restriction",
                new SetMemberRestrictionCommand(true, GroupMemberCapability.None, null, "تلاش غیرمجاز", null), JsonOptions)).StatusCode);
    }

    [Fact]
    public async Task P2ManagementPrivateRequestsCannotBeBrowsedOrCancelledByAnotherAccount()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); using var outsider = factory.CreateClient(); await CreateMessagingUserAsync(outsider);
        var id = setup.Conversation.ConversationId;
        var link = await CreateP2LinkAsync(setup.OwnerClient, id, P2LinkCommand() with { RequiresApproval = true });
        var request = (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!;
        foreach (var suffix in new[] { "invite-links", "join-requests", "member-restrictions" })
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/conversations/{id}/{suffix}")).StatusCode);
        Assert.Empty((await outsider.GetFromJsonAsync<ConversationJoinRequestPageResult>("/api/v1/messaging/join-requests", JsonOptions))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await CompleteP2RequestAsync(outsider, request, "cancel")).StatusCode);
        (await CompleteP2RequestAsync(setup.TargetClient, request, "cancel")).EnsureSuccessStatusCode();
        (await CompleteP2RequestAsync(setup.TargetClient, request, "cancel")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await CompleteP2RequestAsync(setup.OwnerClient, request, "approve")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P2ManagementOrganizationApprovalUsesCreatorsAndRequestersOwnCurrentRoles(bool revokeCreatorRole)
    {
        using var ownerClient = factory.CreateClient(); using var adminClient = factory.CreateClient(); using var studentClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var admin = await CreateMessagingUserAsync(adminClient); var student = await CreateMessagingUserAsync(studentClient);
        var school = await CreatePolicySchoolAsync(ownerClient, owner, student);
        var adminMembership = await factory.CreateMembershipAsync(admin.UserId, school.OrganizationId, OrganizationRole.Manager);
        await SelectOrganizationAsync(adminClient, adminMembership.MembershipId, OrganizationRole.Manager);
        await SelectOrganizationAsync(studentClient, school.StudentMembershipId, OrganizationRole.Student);
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", new CreateManagedConversationCommand(Guid.NewGuid(), ConversationScope.Organization, "پذیرش سازمانی", null, [admin.UserId]));
        await PromoteP2AdminAsync(ownerClient, group.ConversationId, admin.UserId, ConversationAdminPermission.ManageInvites);
        var link = await CreateP2LinkAsync(adminClient, group.ConversationId, P2LinkCommand() with { RequiresApproval = true });
        var request = (await ReadP2RedemptionAsync(studentClient, link)).JoinRequest!;
        if (revokeCreatorRole) await factory.EndRoleAssignmentAsync(adminMembership.RoleAssignmentId);
        var approved = await CompleteP2RequestAsync(ownerClient, request, "approve");
        Assert.Equal(revokeCreatorRole ? HttpStatusCode.NotFound : HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(!revokeCreatorRole, (await factory.GetConversationParticipantsAsync(group.ConversationId)).Any(p => p.UserId == student.UserId && p.Status == ConversationParticipantStatus.Active));
        Assert.Equal(1, await factory.CountMembershipsAsync(student.UserId, school.OrganizationId));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Enrollments.CountAsync(e => e.OrganizationId == school.OrganizationId));
    }

    [Fact]
    public async Task P2ManagementRevokedSessionCannotUseLinksOrPendingRequestActions()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); using var survivor = factory.CreateClient();
        await AuthenticateP1SessionAsync(survivor, setup.Target.PhoneNumber, factory.Services);
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true });
        var request = (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!;
        var sessions = (await setup.TargetClient.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions", JsonOptions))!;
        (await survivor.DeleteAsync($"/api/v1/auth/sessions/{sessions.Single(s => s.IsCurrent).Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await RedeemP2Async(setup.TargetClient, link)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteP2RequestAsync(setup.TargetClient, request, "cancel")).StatusCode);
        (await CompleteP2RequestAsync(survivor, request, "cancel")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task P2ManagementInvalidInputsFailAndSqlFkRejectsCrossConversationRequest()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        foreach (var invalid in new[] { P2LinkCommand() with { MaximumUses = 0 }, P2LinkCommand() with { ExpiresAtUtc = DateTime.UtcNow.AddDays(31) },
            P2LinkCommand() with { ClientLinkId = Guid.Empty } })
            Assert.Equal(HttpStatusCode.BadRequest, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/invite-links", invalid, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.PostAsJsonAsync("/api/v1/messaging/invite-links/redeem", new RedeemInviteLinkCommand(Guid.NewGuid().ToString()), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{setup.Target.UserId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin, (ConversationAdminPermission)(1 << 30)), JsonOptions)).StatusCode);
        var unchanged = (await setup.OwnerClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{setup.Conversation.ConversationId}", JsonOptions))!;
        Assert.Equal(ConversationParticipantRole.Member, unchanged.Participants.Single(p => p.User.UserId == setup.Target.UserId).Role);
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true });
        var other = await CreateManagedAndReadAsync(setup.OwnerClient, "groups", PersonalCommand("منبع متفاوت"));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        db.ConversationJoinRequests.Add(ConversationJoinRequest.Create(other.ConversationId, link.Link.Id, setup.Target.UserId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<Microsoft.Data.SqlClient.SqlException>(failure.InnerException);
        Assert.Equal(547, sql.Number); Assert.Contains("FK_ConversationJoinRequests_ConversationInviteLinks", sql.Message);
    }

    private static async Task<ManagedConversationResult> PromoteP2AdminAsync(HttpClient owner, Guid conversationId, Guid targetId, ConversationAdminPermission permissions)
    {
        using var response = await owner.PatchAsJsonAsync($"/api/v1/conversations/{conversationId}/members/{targetId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin, permissions), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
    }
    private static async Task<MemberRestrictionResult> RestrictP2MemberAsync(HttpClient owner, Guid conversationId, Guid userId, SetMemberRestrictionCommand command)
    {
        using var response = await owner.PutAsJsonAsync($"/api/v1/conversations/{conversationId}/members/{userId}/restriction", command, JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<MemberRestrictionResult>(JsonOptions))!;
    }
}
