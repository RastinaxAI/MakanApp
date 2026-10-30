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
    [Fact]
    public async Task GuardianCommunicationIsScopedAndRevocationDoesNotExposePrivateChildHistory()
    {
        using var managerClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        using var parentClient = factory.CreateClient(); using var friendClient = factory.CreateClient(); using var siblingClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var child = await CreateMessagingUserAsync(childClient);
        var parent = await CreateMessagingUserAsync(parentClient); var friend = await CreateMessagingUserAsync(friendClient);
        var sibling = await CreateMessagingUserAsync(siblingClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Minor);
        await factory.SetCommunicationAgeCategoryAsync(sibling.UserId, CommunicationAgeCategory.Unknown);
        var school = await CreatePolicySchoolAsync(managerClient, manager, child);
        await factory.CreateMembershipAsync(parent.UserId, school.OrganizationId, OrganizationRole.Parent);
        var relation = await factory.CreateGuardianRelationAsync(parent.UserId, school.OrganizationId, school.PersonId);
        var parentDirect = await StartDirectAndReadAsync(parentClient, child.UserId, ConversationScope.Personal);
        await factory.AllowPersonalGroupInviteAsync(friend.UserId, child.UserId);
        var privateDirect = await StartDirectAndReadAsync(friendClient, child.UserId, ConversationScope.Personal);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        await SendMessageAndReadAsync(friendClient, privateDirect.ConversationId, Guid.NewGuid(), "تاریخچه خصوصی کودک");
        using var parentBrowse = await parentClient.GetAsync($"/api/v1/conversations/{privateDirect.ConversationId}/messages");
        using var managerBrowse = await managerClient.GetAsync($"/api/v1/conversations/{privateDirect.ConversationId}/messages");
        using var siblingDirect = await StartDirectAsync(parentClient, sibling.UserId, ConversationScope.Personal);
        Assert.Equal(HttpStatusCode.NotFound, parentBrowse.StatusCode); Assert.Equal(HttpStatusCode.NotFound, managerBrowse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, siblingDirect.StatusCode);
        var siblingSchool = await CreatePolicySchoolAsync(managerClient, manager, sibling);
        await factory.CreateMembershipAsync(parent.UserId, siblingSchool.OrganizationId, OrganizationRole.Parent);
        await factory.CreateGuardianRelationAsync(parent.UserId, siblingSchool.OrganizationId, siblingSchool.PersonId);
        var siblingConversation = await StartDirectAndReadAsync(parentClient, sibling.UserId, ConversationScope.Personal);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: () => factory.RevokeGuardianRelationAsync(relation));
        using var race = await SendMessageAsync(parentClient, parentDirect.ConversationId, Guid.NewGuid(), "لغو هم‌زمان رابطه");
        Assert.Equal(HttpStatusCode.Forbidden, race.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(privateDirect.ConversationId));
        Assert.Empty(await factory.GetMessagesAsync(parentDirect.ConversationId));
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        await SendMessageAndReadAsync(parentClient, siblingConversation.ConversationId, Guid.NewGuid(), "رابطه مستقل فرزند دوم برقرار است");
        Assert.Single(await factory.GetMessagesAsync(siblingConversation.ConversationId));
    }

    [Fact]
    public async Task OfficialGroupUsesEnrollmentAndAssignmentNotArbitraryMessengerRows()
    {
        using var managerClient = factory.CreateClient(); using var childClient = factory.CreateClient(); using var teacherClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var child = await CreateMessagingUserAsync(childClient);
        var teacher = await CreateMessagingUserAsync(teacherClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, child);
        var teacherMembership = await factory.CreateMembershipAsync(teacher.UserId, school.OrganizationId, OrganizationRole.Teacher);
        var assignment = await factory.CreateTeacherAssignmentAsync(school.OrganizationId, school.ClassId, teacherMembership.MembershipId);
        using var resolve = await managerClient.PostAsync($"/api/v1/organization/classes/{school.ClassId}/conversation", null);
        resolve.EnsureSuccessStatusCode(); var group = (await resolve.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!;
        Assert.Equal(3, group.ActiveMemberCount);
        using var managerRead = await managerClient.GetAsync($"/api/v1/conversations/{group.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, managerRead.StatusCode);
        await SelectOrganizationAsync(teacherClient, teacherMembership.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(childClient, school.StudentMembershipId, OrganizationRole.Student);
        await SendMessageAndReadAsync(teacherClient, group.ConversationId, Guid.NewGuid(), "پیام رسمی");
        await factory.EndEnrollmentAsync(school.EnrollmentId);
        using var oldChildRead = await childClient.GetAsync($"/api/v1/conversations/{group.ConversationId}/messages");
        using var oldChildSearch = await childClient.GetAsync($"/api/v1/messages/search?q=پیام&conversationId={group.ConversationId}");
        Assert.Equal(HttpStatusCode.NotFound, oldChildRead.StatusCode);
        Assert.DoesNotContain("پیام رسمی", await oldChildSearch.Content.ReadAsStringAsync());
        await factory.EndTeacherAssignmentAsync(assignment);
        using var oldTeacherSend = await SendMessageAsync(teacherClient, group.ConversationId, Guid.NewGuid(), "شناسه قدیمی");
        Assert.Equal(HttpStatusCode.NotFound, oldTeacherSend.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(group.ConversationId));
    }

    [Fact]
    public async Task EndingOneSchoolAssignmentDoesNotRevokeOtherSchoolOrPersonalConversation()
    {
        using var managerAClient = factory.CreateClient(); using var managerBClient = factory.CreateClient();
        using var studentAClient = factory.CreateClient(); using var studentBClient = factory.CreateClient(); using var teacherClient = factory.CreateClient();
        var managerA = await CreateMessagingUserAsync(managerAClient); var managerB = await CreateMessagingUserAsync(managerBClient);
        var studentA = await CreateMessagingUserAsync(studentAClient); var studentB = await CreateMessagingUserAsync(studentBClient);
        var teacher = await CreateMessagingUserAsync(teacherClient);
        var a = await CreatePolicySchoolAsync(managerAClient, managerA, studentA); var b = await CreatePolicySchoolAsync(managerBClient, managerB, studentB);
        await factory.SetCommunicationAgeCategoryAsync(studentA.UserId, CommunicationAgeCategory.Minor);
        await factory.SetCommunicationAgeCategoryAsync(studentB.UserId, CommunicationAgeCategory.Minor);
        var ma = await factory.CreateMembershipAsync(teacher.UserId, a.OrganizationId, OrganizationRole.Teacher);
        var mb = await factory.CreateMembershipAsync(teacher.UserId, b.OrganizationId, OrganizationRole.Teacher);
        var ta = await factory.CreateTeacherAssignmentAsync(a.OrganizationId, a.ClassId, ma.MembershipId);
        await factory.CreateTeacherAssignmentAsync(b.OrganizationId, b.ClassId, mb.MembershipId);
        var personal = await CreateManagedAndReadAsync(teacherClient, "groups", PersonalCommand("گروه شخصی مستقل"));
        await SelectOrganizationAsync(teacherClient, ma.MembershipId, OrganizationRole.Teacher);
        var directA = await StartDirectAndReadAsync(teacherClient, studentA.UserId, ConversationScope.Organization);
        using var crossOrg = await StartDirectAsync(teacherClient, studentB.UserId, ConversationScope.Organization);
        Assert.Equal(HttpStatusCode.NotFound, crossOrg.StatusCode);
        await factory.EndTeacherAssignmentAsync(ta);
        using var deniedA = await SendMessageAsync(teacherClient, directA.ConversationId, Guid.NewGuid(), "پایان مدرسه الف");
        Assert.Equal(HttpStatusCode.Forbidden, deniedA.StatusCode);
        await SelectOrganizationAsync(teacherClient, mb.MembershipId, OrganizationRole.Teacher);
        var directB = await StartDirectAndReadAsync(teacherClient, studentB.UserId, ConversationScope.Organization);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        await SendMessageAndReadAsync(teacherClient, directB.ConversationId, Guid.NewGuid(), "مدرسه ب برقرار است");
        await factory.EndMembershipAsync(ma.MembershipId);
        await SelectPersonalAsync(teacherClient);
        await SendMessageAndReadAsync(teacherClient, personal.ConversationId, Guid.NewGuid(), "فضای شخصی برقرار است");
        Assert.Single(await factory.GetMessagesAsync(directB.ConversationId)); Assert.Single(await factory.GetMessagesAsync(personal.ConversationId));
    }

    [Fact]
    public async Task ConcurrentInvitationAcceptProducesOneActiveMembershipAndOneAcceptedEffect()
    {
        using var ownerClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var child = await CreateMessagingUserAsync(childClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, child.UserId);
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", PersonalCommand("پذیرش هم‌زمان"));
        using var invited = await ownerClient.PostAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/invitations",
            new CreateConversationInvitationCommand(child.UserId, DateTime.UtcNow.AddDays(1)), JsonOptions);
        invited.EnsureSuccessStatusCode(); var invitation = (await invited.Content.ReadFromJsonAsync<ConversationInvitationResult>(JsonOptions))!;
        var accepts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => childClient.PostAsync($"/api/v1/messaging/invitations/{invitation.Id}/accept", null)));
        try { Assert.All(accepts, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode)); }
        finally { foreach (var response in accepts) response.Dispose(); }
        Assert.Single(await factory.GetConversationParticipantsAsync(group.ConversationId), p => p.UserId == child.UserId && p.Status == ConversationParticipantStatus.Active);
    }
}
