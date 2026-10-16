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
    public async Task P2ContactCardCannotDiscloseProtectedProfileToUnrelatedViewer(CommunicationAgeCategory age)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var subjectClient = factory.CreateClient();
        var subject = await CreateMessagingUserAsync(subjectClient); await factory.SetCommunicationAgeCategoryAsync(subject.UserId, age);
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.ContactCard, ContactUserId = subject.UserId };
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", command, JsonOptions)).StatusCode);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, subject.UserId);
        // رضایت نسبت به فرستنده، رضایت نسبت به همهٔ دریافت‌کنندگان کارت نیست.
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", command, JsonOptions)).StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await factory.AllowPersonalGroupInviteAsync(setup.Second.UserId, subject.UserId);
        var sent = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, command);
        var wire = await setup.SecondClient.GetStringAsync($"/api/v1/conversations/{setup.ConversationId}/messages");
        Assert.DoesNotContain(subject.PhoneNumber, wire, StringComparison.Ordinal);
        var card = Assert.Single((await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages);
        Assert.Equal(sent.MessageId, card.MessageId); Assert.Equal(subject.Username, card.ContactCard!.Username);
    }

    [Fact]
    public async Task P2ContactCardForwardRechecksSubjectVisibilityWithoutRewritingHistory()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var subjectClient = factory.CreateClient();
        var subject = await CreateMessagingUserAsync(subjectClient); await factory.SetCommunicationAgeCategoryAsync(subject.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, subject.UserId); await factory.AllowPersonalGroupInviteAsync(setup.Second.UserId, subject.UserId);
        var card = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.ContactCard, ContactUserId = subject.UserId });
        await factory.RemoveApprovedContactAsync(subject.UserId, setup.Second.UserId);
        var forward = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{card.MessageId}/forward", new ForwardMessageCommand(setup.ConversationId, Guid.NewGuid()), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, forward.StatusCode); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        Assert.Equal(card.MessageId, Assert.Single((await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages).MessageId);
    }

    [Fact]
    public async Task P2ScheduledContactCardRechecksSubjectConsentAtDispatch()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var subjectClient = factory.CreateClient();
        var subject = await CreateMessagingUserAsync(subjectClient); await factory.SetCommunicationAgeCategoryAsync(subject.UserId, CommunicationAgeCategory.Unknown);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, subject.UserId); await factory.AllowPersonalGroupInviteAsync(setup.Second.UserId, subject.UserId);
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.ContactCard, ContactUserId = subject.UserId }));
            await factory.RemoveApprovedContactAsync(subject.UserId, setup.Second.UserId); factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Failed, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }

    [Theory]
    [InlineData("send")]
    [InlineData("edit")]
    [InlineData("forward")]
    [InlineData("blocked")]
    public async Task P2SessionRevokedDuringSafetyCannotCommitMessageRevisionOrIncident(string operation)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var source = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "نسخهٔ اولیه");
        await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor); await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        var evaluated = false;
        factory.ContentSafetyProvider.Configure(operation == "blocked" ? ContentSafetyDecision.Blocked : ContentSafetyDecision.Allowed, beforeReturn: async () =>
        { evaluated = true; await factory.RevokeSessionByTokenAsync(setup.First.AccessToken); });
        try
        {
            using var response = operation switch
            {
                "edit" => await setup.FirstClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}", new EditMessageCommand("نباید ویرایش شود", source.Version), JsonOptions),
                "forward" => await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}/forward", new ForwardMessageCommand(setup.ConversationId, Guid.NewGuid()), JsonOptions),
                _ => await SendMessageAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "نباید ثبت شود")
            };
            Assert.True(evaluated); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("نسخهٔ اولیه", Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)).Text);
            Assert.Single(await factory.GetMessageRevisionTextsAsync(source.MessageId));
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().SafetyIncidents.AnyAsync(i => i.ConversationId == setup.ConversationId));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P2OfficialContactCardIncludesNewClassMemberBeforeParticipantSynchronization(bool enrollDuringSafety)
    {
        using var managerClient = factory.CreateClient(); using var teacherClient = factory.CreateClient();
        using var firstStudentClient = factory.CreateClient(); using var newStudentClient = factory.CreateClient(); using var subjectClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var teacher = await CreateMessagingUserAsync(teacherClient);
        var firstStudent = await CreateMessagingUserAsync(firstStudentClient); var newStudent = await CreateMessagingUserAsync(newStudentClient); var subject = await CreateMessagingUserAsync(subjectClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, firstStudent);
        var membership = await factory.CreateMembershipAsync(teacher.UserId, school.OrganizationId, OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(school.OrganizationId, school.ClassId, membership.MembershipId);
        await SelectOrganizationAsync(teacherClient, membership.MembershipId, OrganizationRole.Teacher);
        var resolved = await managerClient.PostAsync($"/api/v1/organization/classes/{school.ClassId}/conversation", null); resolved.EnsureSuccessStatusCode();
        var conversation = (await resolved.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!;
        await factory.SetCommunicationAgeCategoryAsync(firstStudent.UserId, CommunicationAgeCategory.Minor); await factory.SetCommunicationAgeCategoryAsync(newStudent.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(subject.UserId, firstStudent.UserId);
        async Task EnrollNewStudent()
        {
            await factory.CreateMembershipAsync(newStudent.UserId, school.OrganizationId, OrganizationRole.Student);
            var person = await factory.CreateOrganizationPersonForUserAsync(school.OrganizationId, newStudent.UserId);
            await factory.CreateEnrollmentAsync(school.OrganizationId, school.ClassId, person);
        }
        var evaluated = false;
        if (enrollDuringSafety) factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () => { evaluated = true; await EnrollNewStudent(); });
        else await EnrollNewStudent();
        Assert.DoesNotContain(await factory.GetConversationParticipantsAsync(conversation.ConversationId), p => p.UserId == newStudent.UserId);
        try
        {
            var response = await teacherClient.PostAsJsonAsync($"/api/v1/conversations/{conversation.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.ContactCard, ContactUserId = subject.UserId }, JsonOptions);
            if (enrollDuringSafety) Assert.True(evaluated);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Empty(await factory.GetMessagesAsync(conversation.ConversationId));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task P2DurableScheduledIntentOutlivesOriginSessionWithoutCreatingSession()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "قصد پایدار، نه جعل نشست" }));
            await factory.RevokeSessionByTokenAsync(setup.First.AccessToken);
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var before = await db.UserSessions.CountAsync(s => s.UserId == setup.First.UserId);
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
            Assert.Equal(ScheduledMessageStatus.Dispatched, (await db.ScheduledMessages.AsNoTracking().SingleAsync(s => s.Id == task.Id)).Status);
            Assert.Equal(before, await db.UserSessions.CountAsync(s => s.UserId == setup.First.UserId));
            Assert.Equal(HttpStatusCode.Unauthorized, (await CancelP2ScheduleAsync(setup.FirstClient, task)).StatusCode);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
}
