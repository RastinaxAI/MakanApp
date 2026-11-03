using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Realtime;
using MakanApp.Application.Storage;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalClassV2TeacherPublishRequiresCurrentExplicitLocalDelegationAndSchoolRoleGrantsNothing()
    {
        var host = await ActorAsync(); var teacher = await ActorAsync(); var outsider = await ActorAsync();
        foreach (var actor in new[] { host, teacher, outsider }) await CompletePersonalMessagingProfile(actor);
        var org = await factory.CreateOrganizationAsync("مدرسهٔ نامرتبط");
        await factory.CreateMembershipAsync(outsider.UserId, org, MakanApp.Domain.Organization.OrganizationRole.Teacher);
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "تفویض محلی جاری");
        var invitation = await PostAsync<PersonalClassInvitationDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/invitations",
            new InvitePersonalClassStudentCommand(teacher.UserId, Guid.NewGuid(), PersonalClassLocalRoles.Teacher));
        await PostAsync<PersonalClassDto>(teacher.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        await ProvisionPersonalClass(c.Id);
        var pair = await GetAsync<OfficialSpacesPairResult>(teacher.Client, $"/api/v1/personal-classes/{c.Id}/spaces");
        var route = $"/api/v1/conversations/{pair.Channel.ConversationId}/messages";
        using var denied = await teacher.Client.PostAsJsonAsync(route, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "بدون تفویض" }, Json);
        Assert.False(denied.IsSuccessStatusCode);
        using var external = await outsider.Client.GetAsync($"/api/v1/personal-classes/{c.Id}/spaces"); Assert.Equal(HttpStatusCode.NotFound, external.StatusCode);
        var member = Assert.Single((await GetAsync<PersonalClassPage<PersonalClassMembershipDto>>(host.Client, $"/api/v1/personal-classes/{c.Id}/memberships")).Items, m => m.UserId == teacher.UserId);
        using var grant = await host.Client.PutAsJsonAsync($"/api/v1/personal-classes/{c.Id}/memberships/{member.Id}/roles",
            new ChangePersonalClassRolesCommand(member.Version, PersonalClassLocalRoles.Teacher, PersonalClassDelegatedCapabilities.PublishCommunication, "تفویض انتشار"), Json);
        grant.EnsureSuccessStatusCode(); member = (await grant.Content.ReadFromJsonAsync<PersonalClassMembershipDto>(Json))!;
        await PostAsync<MessageReceiptResult>(teacher.Client, route, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "انتشار مجاز" });
        using var revoke = await host.Client.PutAsJsonAsync($"/api/v1/personal-classes/{c.Id}/memberships/{member.Id}/roles",
            new ChangePersonalClassRolesCommand(member.Version, PersonalClassLocalRoles.Teacher, PersonalClassDelegatedCapabilities.None, "پایان تفویض"), Json);
        revoke.EnsureSuccessStatusCode();
        using var after = await teacher.Client.PostAsJsonAsync(route, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "تفویض پایان‌یافته" }, Json);
        Assert.False(after.IsSuccessStatusCode);
    }

    [Fact]
    public async Task PersonalClassV2SelectedManagerMustAcceptAndTeacherInvitationDoesNotUseLearnerSeat()
    {
        var requester = await ActorAsync(); var manager = await ActorAsync(); var teacher = await ActorAsync(); var admin = await AdminAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(requester.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true, learners: 0));
        var request = await SubmittedClassRequestAsync(requester, "مدیر منتخب و مدرس اختیاری", 1);
        var approved = await PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{request.Id}/review",
            new ReviewPersonalClassRequestCommand(request.Version, PersonalClassRequestStatus.Approved, "انتخاب مدیر با پذیرش", manager.UserId));
        var id = approved.CreatedClassId!.Value;
        var transfers = await GetAsync<PersonalClassPage<PersonalClassTransferDto>>(manager.Client, "/api/v1/personal-classes/manager-transfers");
        var transfer = Assert.Single(transfers.Items, t => t.PersonalClassId == id);
        await WithDb(async db => Assert.False(await db.PersonalClassParticipants.AnyAsync(m => m.PersonalClassId == id)));
        await PostAsync<PersonalClassTransferDto>(manager.Client, $"/api/v1/personal-classes/manager-transfers/{transfer.Id}/decision",
            new DecidePersonalClassTransferCommand(transfer.Version, PersonalClassManagerTransferStatus.Accepted));
        await ProvisionPersonalClass(id);
        var c = await GetAsync<PersonalClassDto>(manager.Client, $"/api/v1/personal-classes/{id}");
        await PostAsync<PersonalClassDto>(manager.Client, $"/api/v1/personal-classes/{id}/activate", new PersonalClassReasonCommand(c.Version, "آغاز توسط مدیر منتخب"));
        var invitation = await PostAsync<PersonalClassInvitationDto>(manager.Client, $"/api/v1/personal-classes/{id}/invitations",
            new InvitePersonalClassStudentCommand(teacher.UserId, Guid.NewGuid(), PersonalClassLocalRoles.Teacher, PersonalClassDelegatedCapabilities.None));
        Assert.Equal(PersonalClassLocalRoles.Teacher, invitation.LocalRoles);
        await PostAsync<PersonalClassDto>(teacher.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        var members = await GetAsync<PersonalClassPage<PersonalClassMembershipDto>>(manager.Client, $"/api/v1/personal-classes/{id}/memberships");
        var accepted = Assert.Single(members.Items, m => m.UserId == teacher.UserId);
        Assert.Equal(PersonalClassLocalRoles.Teacher, accepted.LocalRoles); Assert.Equal(PersonalClassDelegatedCapabilities.None, accepted.DelegatedCapabilities);
        await ProvisionPersonalClass(id);
        var pair = await GetAsync<OfficialSpacesPairResult>(teacher.Client, $"/api/v1/personal-classes/{id}/spaces");
        Assert.False(pair.Channel.Capabilities.CanSend); Assert.False(pair.Channel.Capabilities.CanManage);
        var managerMember = Assert.Single(members.Items, m => m.UserId == manager.UserId);
        using var combine = await manager.Client.PutAsJsonAsync($"/api/v1/personal-classes/{id}/memberships/{managerMember.Id}/roles",
            new ChangePersonalClassRolesCommand(managerMember.Version, PersonalClassLocalRoles.Manager | PersonalClassLocalRoles.Teacher, PersonalClassDelegatedCapabilities.None, "مسئولیت همزمان"), Json);
        combine.EnsureSuccessStatusCode();
        await WithDb(async db => Assert.Equal(0, await db.PersonalClassParticipants.CountAsync(m => m.PersonalClassId == id && (m.LocalRoles & PersonalClassLocalRoles.Learner) != 0)));
    }

    [Fact]
    public async Task PersonalClassV2RevocationImmediatelyFencesReadSendFileReplayAndLiveAudienceButKeepsPersonalDirect()
    {
        var host = await ActorAsync(); var learner = await ActorAsync();
        await CompletePersonalMessagingProfile(host); await CompletePersonalMessagingProfile(learner);
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "مجوز جاری فضای مستقل");
        var invitation = await InviteAsync(host, learner, c.Id);
        await PostAsync<PersonalClassDto>(learner.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        await ProvisionPersonalClass(c.Id);
        var pair = await GetAsync<OfficialSpacesPairResult>(learner.Client, $"/api/v1/personal-classes/{c.Id}/spaces");
        Assert.True(pair.Group.Capabilities.CanRead); Assert.True(pair.Group.Capabilities.CanSend); Assert.True(pair.Channel.Capabilities.CanRead); Assert.False(pair.Channel.Capabilities.CanSend);
        await PostAsync<UserContactResult>(learner.Client, "/api/v1/contacts", new SaveUserContactCommand(host.UserId, null));
        await PostAsync<UserContactResult>(host.Client, "/api/v1/contacts", new SaveUserContactCommand(learner.UserId, null));
        var direct = await PostAsync<DirectConversationResult>(host.Client, "/api/v1/conversations/direct", new StartDirectConversationCommand(learner.UserId, ConversationScope.Personal));
        var personal = await PostAsync<MessageReceiptResult>(host.Client, $"/api/v1/conversations/{direct.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "تاریخچه شخصی مستقل" });
        using var multipart = new MultipartFormDataContent(); using var content = new ByteArrayContent(StabilizationMediaFixtures.Bytes("image/png"));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); multipart.Add(content, "File", "class-fixture.png");
        using var upload = await host.Client.PostAsync("/api/v1/files", multipart); upload.EnsureSuccessStatusCode();
        var file = (await upload.Content.ReadFromJsonAsync<FileAssetResult>(Json))!;
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [file.Id] };
        var message = await PostAsync<MessageReceiptResult>(host.Client, $"/api/v1/conversations/{pair.Group.ConversationId}/messages", command);
        var retry = await PostAsync<MessageReceiptResult>(host.Client, $"/api/v1/conversations/{pair.Group.ConversationId}/messages", command); Assert.Equal(message.MessageId, retry.MessageId);
        using var download = await learner.Client.GetAsync($"/api/v1/files/{file.Id}/content"); download.EnsureSuccessStatusCode();
        var boundary = await GetAsync<AccountChangePageResult>(learner.Client, "/api/v1/realtime/changes");
        var members = await GetAsync<PersonalClassPage<PersonalClassMembershipDto>>(host.Client, $"/api/v1/personal-classes/{c.Id}/memberships");
        var member = Assert.Single(members.Items, m => m.UserId == learner.UserId);
        using var end = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personal-classes/{c.Id}/memberships/{member.Id}")
        { Content = JsonContent.Create(new PersonalClassReasonCommand(member.Version, "پایان دسترسی آزمایشی"), options: Json) };
        using var ended = await host.Client.SendAsync(end); ended.EnsureSuccessStatusCode();
        // پیش از اجرای Worker، projection قبلی هنوز هست؛ مجوز باید از Membership جاری رد شود.
        foreach (var id in new[] { pair.Group.ConversationId, pair.Channel.ConversationId })
        {
            using var read = await learner.Client.GetAsync($"/api/v1/conversations/{id}/messages"); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            using var send = await learner.Client.PostAsJsonAsync($"/api/v1/conversations/{id}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "دسترسی منقضی" }, Json);
            Assert.False(send.IsSuccessStatusCode);
        }
        using var deniedFile = await learner.Client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, deniedFile.StatusCode);
        await PostAsync<MessageReceiptResult>(host.Client, $"/api/v1/conversations/{pair.Group.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پس از پایان عضویت" });
        var changes = await GetAsync<AccountChangePageResult>(learner.Client, "/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(boundary.NextCursor));
        Assert.DoesNotContain(changes.Changes, e => e.Conversation?.ConversationId == pair.Group.ConversationId || e.Conversation?.ConversationId == pair.Channel.ConversationId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var change = await db.MessagingChangeEvents.Where(e => e.ConversationId == pair.Group.ConversationId).OrderByDescending(e => e.ChangeSequence).FirstAsync();
            var notification = new MessagingRealtimeNotification(change.ConversationId, change.ChangeType, change.ResourceId, change.ResourceVersion,
                MessagingChangeCursor.Encode(change.ConversationId, change.ChangeSequence), DateTime.SpecifyKind(change.OccurredAtUtc, DateTimeKind.Utc), 1);
            var recipients = new List<Guid>();
            await foreach (var session in scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>().GetActiveSessionIdsAsync(notification, null, factory.Clock.GetUtcNow().UtcDateTime, default)) recipients.Add(session);
            Assert.DoesNotContain(learner.SessionId, recipients);
        }
        var history = await GetAsync<ConversationMessagePageResult>(learner.Client, $"/api/v1/conversations/{direct.ConversationId}/messages");
        Assert.Contains(history.Messages, m => m.MessageId == personal.MessageId);
    }

    [Fact]
    public async Task PersonalClassV2ArchivePreservesHistoryButRejectsMessagesAndReactions()
    {
        var host = await ActorAsync(); await CompletePersonalMessagingProfile(host);
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "بایگانی بدون حذف تاریخچه");
        var pair = await GetAsync<OfficialSpacesPairResult>(host.Client, $"/api/v1/personal-classes/{c.Id}/spaces");
        var message = await PostAsync<MessageReceiptResult>(host.Client, $"/api/v1/conversations/{pair.Group.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "سابقه مجاز" });
        await PostAsync<PersonalClassDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/archive", new PersonalClassReasonCommand(c.Version, "پایان فعالیت"));
        Assert.Contains((await GetAsync<ConversationMessagePageResult>(host.Client, $"/api/v1/conversations/{pair.Group.ConversationId}/messages")).Messages, m => m.MessageId == message.MessageId);
        using var send = await host.Client.PostAsJsonAsync($"/api/v1/conversations/{pair.Group.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "تغییر پس از پایان" }, Json);
        Assert.False(send.IsSuccessStatusCode);
        using var react = await host.Client.PostAsJsonAsync($"/api/v1/conversations/{pair.Group.ConversationId}/messages/{message.MessageId}/reactions", new AddReactionCommand(MessageReactionType.Like), Json);
        Assert.False(react.IsSuccessStatusCode);
    }

    private async Task CompletePersonalMessagingProfile(TestActor actor)
    {
        using var response = await actor.Client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("کاربر", "کلاس", null, "pc." + Guid.NewGuid().ToString("N")[..12]), Json);
        response.EnsureSuccessStatusCode();
    }
    private async Task ProvisionPersonalClass(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePersonalClassPairAsync(id, default));
    }
}
