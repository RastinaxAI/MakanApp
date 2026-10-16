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
    [Fact]
    public async Task P2ScheduledBlockAtDispatchDoesNotUseCreationTimePermission()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پس از بلاک ممنوع" }));
            (await setup.SecondClient.PostAsync($"/api/v1/messaging/blocks/{setup.First.UserId}", null)).EnsureSuccessStatusCode();
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Failed, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledRemovedGroupMemberCannotDispatchOldIntent()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.TargetClient, setup.Conversation.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "عضو سابق" }));
            (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.Conversation.ConversationId));
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(ScheduledMessageStatus.Failed, (await db.ScheduledMessages.SingleAsync(s => s.Id == task.Id)).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/scheduled-messages")).StatusCode);
        }
        finally { await CleanupP2SchedulesAsync(setup.Target.UserId); factory.Clock.UseSystemTime(); }
    }
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2ScheduledSafetyUnavailableIsRetryableButNeverSent(CommunicationAgeCategory age)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, age);
            await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "بررسی در لحظه ارسال" }));
            factory.ContentSafetyProvider.ConfigureUnavailable(); factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); var waiting = await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id);
            Assert.Equal(ScheduledMessageStatus.Scheduled, waiting.Status); Assert.Equal(MessagingErrorCodes.MessageSafetyUnavailable, waiting.LastFailureCode); Assert.Null(waiting.DispatchedMessageId);
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); factory.Clock.SetUtcNow(now.AddMinutes(3)); await DispatchP2ScheduledAsync();
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Dispatched, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledBlockWhileSafetyIsRunningWinsBeforeCommit()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor);
            await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "رقابت بلاک" }));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () =>
                (await setup.SecondClient.PostAsync($"/api/v1/messaging/blocks/{setup.First.UserId}", null)).EnsureSuccessStatusCode());
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Failed, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledCancellationDuringSafetyPreventsCommitAndCannotBecomeFailed()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor);
            await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "لغو حین بررسی" }));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () =>
            {
                var current = await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id);
                (await CancelP2ScheduleAsync(setup.FirstClient, current)).EnsureSuccessStatusCode();
            });
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Cancelled, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledOrganizationMembershipRemovedDuringSafetyCannotDispatch()
    {
        using var managerClient = factory.CreateClient(); using var ownerClient = factory.CreateClient(); using var studentClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var owner = await CreateMessagingUserAsync(ownerClient); var student = await CreateMessagingUserAsync(studentClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, student); await factory.SetCommunicationAgeCategoryAsync(student.UserId, CommunicationAgeCategory.Minor);
        var teacherMembership = await factory.CreateMembershipAsync(owner.UserId, school.OrganizationId, OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(school.OrganizationId, school.ClassId, teacherMembership.MembershipId);
        await SelectOrganizationAsync(ownerClient, teacherMembership.MembershipId, OrganizationRole.Teacher);
        var resolved = await managerClient.PostAsync($"/api/v1/organization/classes/{school.ClassId}/conversation", null); resolved.EnsureSuccessStatusCode();
        var group = (await resolved.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!;
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(ownerClient, group.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "عضویت باید جاری بماند" }));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: () => factory.EndMembershipAsync(teacherMembership.MembershipId));
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync(); Assert.Empty(await factory.GetMessagesAsync(group.ConversationId));
            await using var verification = factory.Services.CreateAsyncScope(); Assert.Equal(ScheduledMessageStatus.Failed, (await verification.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.SingleAsync(s => s.Id == task.Id)).Status);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); await CleanupP2SchedulesAsync(owner.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledClosedTopicRejectsDispatchWithoutDeletingExistingHistory()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var topic = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, new(Guid.NewGuid(), "موضوع زمان‌بندی"));
            (await SendP2TopicAsync(setup.OwnerClient, topic, Guid.NewGuid(), "تاریخچهٔ محفوظ")).EnsureSuccessStatusCode();
            var task = await ScheduleP2Async(setup.OwnerClient, topic.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "بسته شده", TopicId = topic.TopicId }));
            await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed); factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Equal("تاریخچهٔ محفوظ", Assert.Single(await factory.GetMessagesAsync(topic.ConversationId)).Text); Assert.Equal(ScheduledMessageStatus.Failed, (await ReadP2ScheduleAsync(setup.OwnerClient, topic.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.Owner.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledAttachmentRevokedAfterQueueCannotBePublished()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var file = await UploadFileAsync(setup.FirstClient, "image/png", "p2-scheduled.png");
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [file.Id] }));
            await factory.SetFileAssetStatusAsync(file.Id, MakanApp.Domain.Storage.FileAssetStatus.Deleted);
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Failed, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledSilentIntentPreservesFlagOnActualMessage()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "زمان‌بندی بی‌صدا", IsSilent = true }));
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            var message = Assert.Single((await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages);
            Assert.True(message.IsSilent); Assert.Equal(ScheduledMessageStatus.Dispatched, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledChangedChannelPublishPermissionIsRechecked()
    {
        using var setup = await CreateP2ManagedSetupAsync("channels", initiallyMember: true); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId, ConversationAdminPermission.PublishMessages);
            var task = await ScheduleP2Async(setup.TargetClient, setup.Conversation.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "اختیار قبلی" }));
            await PromoteP2AdminAsync(setup.OwnerClient, setup.Conversation.ConversationId, setup.Target.UserId, ConversationAdminPermission.ManageInvites);
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.Conversation.ConversationId)); Assert.Equal(ScheduledMessageStatus.Failed, (await ReadP2ScheduleAsync(setup.TargetClient, setup.Conversation.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.Target.UserId); factory.Clock.UseSystemTime(); }
    }
}
