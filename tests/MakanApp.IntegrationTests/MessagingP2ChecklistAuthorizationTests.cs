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
    [Theory]
    [InlineData("groups")] [InlineData("channels")]
    public async Task P2ChecklistDefinitionRequiresCurrentSendAndContentManagementPermission(string kind)
    {
        using var setup = await CreateP2ManagedSetupAsync(kind, initiallyMember: true); var id = setup.Conversation.ConversationId;
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id); var checklist = await ReadP2ChecklistAsync(setup.TargetClient, id, receipt.MessageId);
        var update = P2ChecklistUpdate(checklist) with { Title = "تعریف تازه" };
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateP2ChecklistResponseAsync(setup.TargetClient, checklist, update)).StatusCode);
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.PublishMessages);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateP2ChecklistResponseAsync(setup.TargetClient, checklist, update)).StatusCode);
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.DeleteMessages | ConversationAdminPermission.PublishMessages);
        var edited = await UpdateP2ChecklistAsync(setup.TargetClient, checklist, update); Assert.Equal(update.Title, edited.Title);
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.PublishMessages);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateP2ChecklistResponseAsync(setup.TargetClient, edited, P2ChecklistUpdate(edited))).StatusCode);
    }

    [Theory]
    [InlineData("groups")] [InlineData("channels")]
    public async Task P2ChecklistCurrentMemberCanInteractButChannelSubscriberCannotPublish(string kind)
    {
        using var setup = await CreateP2ManagedSetupAsync(kind, initiallyMember: true); var id = setup.Conversation.ConversationId;
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id, P2Checklist(ConversationChecklistMode.Collaborative));
        var checklist = await ReadP2ChecklistAsync(setup.TargetClient, id, receipt.MessageId);
        var changed = await SetP2ChecklistAsync(setup.TargetClient, checklist, checklist.Items.First().ItemId, true); Assert.True(changed.Items.First().IsChecked);
        var creation = await setup.TargetClient.PostAsJsonAsync($"/api/v1/conversations/{id}/checklists", P2Checklist(), JsonOptions);
        Assert.Equal(kind == "channels" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, creation.StatusCode);
        await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new(false, GroupMemberCapability.SendMessages, null, "توقف تعامل", null));
        var denied = await SetP2ChecklistResponseAsync(setup.TargetClient, changed, changed.Items.First().ItemId, false, changed.Items.First().StateVersion);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(MessagingErrorCodes.MemberRestricted, await ReadProblemCodeAsync(denied));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task P2ChecklistRemovedAndBannedMembersCannotMutateOldIdsAndRevokedSessionIsUnauthorized(bool ban)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id); var checklist = await ReadP2ChecklistAsync(setup.TargetClient, id, receipt.MessageId);
        if (ban) await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new(true, GroupMemberCapability.None, null, "مسدودسازی عضو", null));
        else (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(P2ChecklistRoute(id, receipt.MessageId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetP2ChecklistResponseAsync(setup.TargetClient, checklist, checklist.Items.First().ItemId, true)).StatusCode);
        await factory.RevokeSessionByTokenAsync(setup.Owner.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SetP2ChecklistResponseAsync(setup.OwnerClient, checklist, checklist.Items.First().ItemId, true)).StatusCode);
    }

    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)] [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2ChecklistProtectedRelationshipAndBlockAreReevaluatedWithoutErasingHistory(CommunicationAgeCategory age)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, age);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId); var checklist = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId);
        await factory.RemoveApprovedContactAsync(setup.Second.UserId, setup.First.UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetP2ChecklistResponseAsync(setup.SecondClient, checklist, checklist.Items.First().ItemId, true)).StatusCode);
        Assert.All((await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId)).Items, i => Assert.False(i.IsChecked));
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        (await setup.FirstClient.PostAsync($"/api/v1/messaging/blocks/{setup.Second.UserId}", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await SetP2ChecklistResponseAsync(setup.SecondClient, checklist, checklist.Items.First().ItemId, true)).StatusCode);
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
    }

    [Fact]
    public async Task P2ChecklistOfficialClassUsesCurrentEnrollmentNotGuardianOrManagerPrivateAccess()
    {
        using var managerClient = factory.CreateClient(); using var studentClient = factory.CreateClient(); using var teacherClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var student = await CreateMessagingUserAsync(studentClient); var teacher = await CreateMessagingUserAsync(teacherClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, student);
        var membership = await factory.CreateMembershipAsync(teacher.UserId, school.OrganizationId, OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(school.OrganizationId, school.ClassId, membership.MembershipId);
        using var resolved = await managerClient.PostAsync($"/api/v1/organization/classes/{school.ClassId}/conversation", null); resolved.EnsureSuccessStatusCode();
        var id = (await resolved.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!.ConversationId;
        await SelectOrganizationAsync(teacherClient, membership.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(studentClient, school.StudentMembershipId, OrganizationRole.Student);
        var receipt = await CreateP2ChecklistAsync(teacherClient, id); var checklist = await ReadP2ChecklistAsync(studentClient, id, receipt.MessageId);
        await SetP2ChecklistAsync(studentClient, checklist, checklist.Items.First().ItemId, true);
        Assert.Equal(HttpStatusCode.NotFound, (await managerClient.GetAsync(P2ChecklistRoute(id, receipt.MessageId))).StatusCode);
        await factory.EndEnrollmentAsync(school.EnrollmentId);
        Assert.Equal(HttpStatusCode.NotFound, (await SetP2ChecklistResponseAsync(studentClient, checklist, checklist.Items.Last().ItemId, true)).StatusCode);
    }

    [Fact]
    public async Task P2ChecklistCreatorCannotUseDefinitionEndpointToBypassOrdinaryEditWindow()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId); var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
            factory.Clock.SetUtcNow(now.AddHours(1).AddSeconds(1));
            using var response = await UpdateP2ChecklistResponseAsync(setup.FirstClient, checklist, P2ChecklistUpdate(checklist) with { Title = "دیرهنگام" });
            Assert.Equal(MessagingErrorCodes.MessageEditWindowExpired, await ReadProblemCodeAsync(response));
            Assert.Equal(checklist.Version, (await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId)).Version);
        }
        finally { factory.Clock.UseSystemTime(); }
    }
}
