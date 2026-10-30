using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task TeacherWithHiddenPersonalCardCanUseAccountCapabilitiesWithoutSelectingPersonal()
    {
        using var teacherClient = factory.CreateClient(); using var peerClient = factory.CreateClient();
        var teacher = await CreateMessagingUserAsync(teacherClient); var peer = await CreateMessagingUserAsync(peerClient);
        var org = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(teacher.UserId, org, OrganizationRole.Teacher);
        await SelectOrganizationAsync(teacherClient, membership.MembershipId, OrganizationRole.Teacher);
        var snapshot = await teacherClient.GetFromJsonAsync<WorkspaceSnapshotResult>("/api/v1/workspaces/snapshot", JsonOptions);
        Assert.False(snapshot!.PersonalWorkspaceVisible); Assert.True(snapshot.HasEffectiveEducationalRole);
        var before = await teacherClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions);
        using (var self = await teacherClient.GetAsync("/api/v1/me")) self.EnsureSuccessStatusCode();
        using (var contact = await teacherClient.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(peer.UserId, "مخاطب آزمایشی"), JsonOptions)) contact.EnsureSuccessStatusCode();
        using (var privacy = await teacherClient.GetAsync("/api/v1/messaging/privacy")) privacy.EnsureSuccessStatusCode();
        var personal = await StartDirectAndReadAsync(teacherClient, peer.UserId, ConversationScope.Personal);
        var sent = await SendMessageAndReadAsync(teacherClient, personal.ConversationId, Guid.NewGuid(), "گفتگوی شخصی مستقل از نقش آموزشی");
        var messages = await teacherClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{personal.ConversationId}/messages", JsonOptions);
        Assert.Contains(messages!.Messages, m => m.MessageId == sent.MessageId);
        Assert.Equal(ConversationScope.Personal, personal.Scope); Assert.Null(personal.OrganizationId);
        var summary = await teacherClient.GetFromJsonAsync<CommercialSummaryDto>("/api/v1/commerce/personal/subscription", JsonOptions);
        Assert.Equal(teacher.UserId, summary!.OwnerId);
        Assert.Equal(before, await teacherClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions));
        using (var block = await teacherClient.PostAsync($"/api/v1/messaging/blocks/{peer.UserId}", null)) block.EnsureSuccessStatusCode();
        using var blocked = await SendMessageAsync(teacherClient, personal.ConversationId, Guid.NewGuid(), "ارسال پس از مسدودی نباید مجاز شود");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task PendingIsNotEffectiveAndEndingLastRoleRestoresCardWithoutDeletingPersonalChat()
    {
        using var userClient = factory.CreateClient(); using var peerClient = factory.CreateClient();
        var user = await CreateMessagingUserAsync(userClient); var peer = await CreateMessagingUserAsync(peerClient);
        var organization = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateInvitationAsync(user.UserId, organization, OrganizationRole.Teacher);
        var pending = await userClient.GetFromJsonAsync<WorkspaceSnapshotResult>("/api/v1/workspaces/snapshot", JsonOptions);
        Assert.True(pending!.PersonalWorkspaceVisible);
        var teacher = await factory.CreateMembershipAsync(user.UserId, organization, OrganizationRole.Teacher);
        var other = await factory.CreateOrganizationAsync(NewOrganizationName());
        var parent = await factory.CreateMembershipAsync(user.UserId, other, OrganizationRole.Parent);
        await SelectOrganizationAsync(userClient, teacher.MembershipId, OrganizationRole.Teacher);
        var chat = await StartDirectAndReadAsync(userClient, peer.UserId, ConversationScope.Personal);
        var first = await SendMessageAndReadAsync(userClient, chat.ConversationId, Guid.NewGuid(), "تاریخچهٔ شخصی حفظ می‌شود");
        await factory.EndRoleAssignmentAsync(teacher.RoleAssignmentId);
        var multi = await userClient.GetFromJsonAsync<WorkspaceSnapshotResult>("/api/v1/workspaces/snapshot", JsonOptions);
        Assert.False(multi!.PersonalWorkspaceVisible);
        Assert.Contains(multi.Workspaces, w => w.OrganizationId == other && w.Role == OrganizationRole.Parent && w.CanEnter);
        await factory.EndRoleAssignmentAsync(parent.RoleAssignmentId);
        var ended = await userClient.GetFromJsonAsync<WorkspaceSnapshotResult>("/api/v1/workspaces/snapshot", JsonOptions);
        Assert.True(ended!.PersonalWorkspaceVisible); Assert.NotEqual(multi.Version, ended.Version);
        await SendMessageAndReadAsync(userClient, chat.ConversationId, Guid.NewGuid(), "ورود به Personal نباید به نقش منقضی وابسته باشد");
        var history = await userClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{chat.ConversationId}/messages", JsonOptions);
        Assert.Contains(history!.Messages, m => m.MessageId == first.MessageId);
    }

    [Fact]
    public async Task PersonalProjectionCannotAuthorizeAnOrganizationConversationFromAnotherContext()
    {
        using var actorClient = factory.CreateClient(); using var peerClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient); var peer = await CreateMessagingUserAsync(peerClient);
        var orgA = await factory.CreateOrganizationAsync(NewOrganizationName()); var orgB = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerA = await factory.CreateMembershipAsync(actor.UserId, orgA, OrganizationRole.Manager);
        var managerB = await factory.CreateMembershipAsync(actor.UserId, orgB, OrganizationRole.Manager);
        await factory.CreateMembershipAsync(peer.UserId, orgB, OrganizationRole.Teacher);
        await SelectOrganizationAsync(actorClient, managerB.MembershipId, OrganizationRole.Manager);
        var chatB = await StartDirectAndReadAsync(actorClient, peer.UserId, ConversationScope.Organization);
        await SelectOrganizationAsync(actorClient, managerA.MembershipId, OrganizationRole.Manager);
        using var denied = await actorClient.GetAsync($"/api/v1/conversations/{chatB.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var deniedSend = await SendMessageAsync(actorClient, chatB.ConversationId, Guid.NewGuid(), "دسترسی بین‌سازمانی ممنوع");
        Assert.False(deniedSend.IsSuccessStatusCode);
        Assert.Equal(orgA, (await actorClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!.OrganizationId);
    }
}
