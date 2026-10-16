using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
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
    public async Task P2TopicsProtectedTitleSafetyFailsClosedWithoutFakeMessage(CommunicationAgeCategory age)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, age);
        factory.ContentSafetyProvider.ConfigureUnavailable();
        var unavailable = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/topics", new CreateConversationTopicCommand(Guid.NewGuid(), "عنوان"), JsonOptions);
        Assert.Equal(MessagingErrorCodes.MessageSafetyUnavailable, await ReadProblemCodeAsync(unavailable)); Assert.False(unavailable.IsSuccessStatusCode);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked, policyCodes: ["topic-policy"]);
        var blocked = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/topics", new CreateConversationTopicCommand(Guid.NewGuid(), "عنوان مسدود"), JsonOptions);
        Assert.Equal(MessagingErrorCodes.MessageSafetyBlocked, await ReadProblemCodeAsync(blocked));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.ConversationTopics.AnyAsync(t => t.ConversationId == id)); Assert.False(await db.Messages.AnyAsync(m => m.ConversationId == id));
        var incident = await db.SafetyIncidents.SingleAsync(s => s.ConversationId == id); Assert.Equal("عنوان مسدود", incident.ContentSnapshot);
        Assert.True(await db.ModerationCases.AnyAsync(c => c.SafetyIncidentId == incident.Id));
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
    }
    [Fact]
    public async Task P2TopicsRelationshipRevokedDuringSafetyCannotPublishTitle()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, CommunicationAgeCategory.Minor);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: () => factory.RemoveApprovedContactAsync(setup.Target.UserId, setup.Owner.UserId));
        var denied = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/topics", new CreateConversationTopicCommand(Guid.NewGuid(), "نباید ذخیره شود"), JsonOptions);
        Assert.False(denied.IsSuccessStatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.ConversationTopics.AnyAsync(t => t.ConversationId == id)); Assert.False(await db.Messages.AnyAsync(m => m.ConversationId == id));
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
    }
    [Fact]
    public async Task P2TopicsOfficialClassRequiresCurrentTeacherNotManagerOrStudent()
    {
        using var managerClient = factory.CreateClient(); using var studentClient = factory.CreateClient(); using var teacherClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var student = await CreateMessagingUserAsync(studentClient); var teacher = await CreateMessagingUserAsync(teacherClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, student);
        var membership = await factory.CreateMembershipAsync(teacher.UserId, school.OrganizationId, OrganizationRole.Teacher);
        var assignment = await factory.CreateTeacherAssignmentAsync(school.OrganizationId, school.ClassId, membership.MembershipId);
        var resolved = await managerClient.PostAsync($"/api/v1/organization/classes/{school.ClassId}/conversation", null); resolved.EnsureSuccessStatusCode();
        var id = (await resolved.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!.ConversationId;
        await SelectOrganizationAsync(teacherClient, membership.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(studentClient, school.StudentMembershipId, OrganizationRole.Student);
        var topic = await CreateP2TopicAsync(teacherClient, id, new(Guid.NewGuid(), "درس"));
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.PostAsJsonAsync($"/api/v1/conversations/{id}/topics", new CreateConversationTopicCommand(Guid.NewGuid(), "دانش‌آموز مدیر نیست"), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await managerClient.GetAsync($"/api/v1/conversations/{id}/topics")).StatusCode);
        Assert.All(await factory.GetConversationParticipantsAsync(id), p => Assert.Equal(ConversationParticipantRole.Member, p.Role));
        await factory.EndTeacherAssignmentAsync(assignment);
        Assert.Equal(HttpStatusCode.NotFound, (await teacherClient.PutAsJsonAsync($"/api/v1/conversations/{id}/topics/{topic.TopicId}", new UpdateConversationTopicCommand(topic.Title, ConversationTopicStatus.Closed, topic.Version), JsonOptions)).StatusCode);
    }
    [Fact]
    public async Task P2TopicsFromJoinHidesOldTopicMetadataAndDeltaUntilThereIsVisibleContent()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); var id = setup.Conversation.ConversationId;
        (await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/settings",
            new UpdateConversationSettingsCommand(ConversationVisibility.Private, GroupHistoryPolicy.FromJoin, GroupMemberCapability.LegacyMemberDefault, setup.Conversation.Version), JsonOptions)).EnsureSuccessStatusCode();
        var topic = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "عنوان پیش از عضویت"));
        (await SendP2TopicAsync(setup.OwnerClient, topic, Guid.NewGuid(), "پیش از عضویت")).EnsureSuccessStatusCode();
        var link = await CreateP2LinkAsync(setup.OwnerClient, id, P2LinkCommand());
        (await RedeemP2Async(setup.TargetClient, link)).EnsureSuccessStatusCode();
        Assert.Empty((await setup.TargetClient.GetFromJsonAsync<ConversationTopicPageResult>($"/api/v1/conversations/{id}/topics", JsonOptions))!.Items);
        Assert.DoesNotContain((await GetChangesAsync(setup.TargetClient, id)).Changes, c => c.ResourceId == topic.TopicId);
        (await SendP2TopicAsync(setup.OwnerClient, topic, Guid.NewGuid(), "پس از عضویت")).EnsureSuccessStatusCode();
        Assert.Single((await setup.TargetClient.GetFromJsonAsync<ConversationTopicPageResult>($"/api/v1/conversations/{id}/topics", JsonOptions))!.Items);
        Assert.Equal("پس از عضویت", Assert.Single((await setup.TargetClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{id}/messages?topicId={topic.TopicId}", JsonOptions))!.Messages).Text);
    }
    [Fact]
    public async Task P2TopicsForwardMustRespectDestinationTopicAndRetryPayload()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var source = await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "منبع");
        var topic = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "مقصد")); var clientId = Guid.NewGuid();
        var command = new ForwardMessageCommand(id, clientId, topic.TopicId);
        var forward = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/messages/{source.MessageId}/forward", command, JsonOptions); forward.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/messages/{source.MessageId}/forward", command with { DestinationTopicId = null }, JsonOptions)).StatusCode);
        await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed);
        Assert.Equal(HttpStatusCode.Conflict, (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/messages/{source.MessageId}/forward", command with { ClientMessageId = Guid.NewGuid() }, JsonOptions)).StatusCode);
    }
}
