using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Realtime;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task PersonalAccountExplicitOrganizationListsDoNotSilentlyReturnPersonalData()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        foreach (var route in new[] { "/api/v1/conversations?scope=Organization", "/api/v1/conversations/discover?scope=Organization",
            "/api/v1/messages/search?q=scope&scope=Organization", "/api/v1/realtime/changes?scope=Organization" })
        {
            using var response = await client.GetAsync(route); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task PersonalAccountAdvancedFeaturesKeepSelectedTeacherAndExistingSafetyPolicies()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var org = await factory.CreateOrganizationAsync(NewOrganizationName());
        var firstRole = await factory.CreateMembershipAsync(setup.First.UserId, org, OrganizationRole.Teacher);
        var secondRole = await factory.CreateMembershipAsync(setup.Second.UserId, org, OrganizationRole.Teacher);
        await SelectOrganizationAsync(setup.FirstClient, firstRole.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(setup.SecondClient, secondRole.MembershipId, OrganizationRole.Teacher);
        var selected = await setup.FirstClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions);
        var poll = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        Assert.Equal(1, (await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, poll.MessageId, [1])).TotalVoters);
        var checklist = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        Assert.NotEmpty((await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, checklist.MessageId)).Items);
        var saved = (await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var own = await SendMessageAndReadAsync(setup.FirstClient, saved.ConversationId, Guid.NewGuid(), "account-scope-label");
        await WriteP2LabelsAsync(setup.FirstClient, own.MessageId, ["personal"], null);
        var matches = (await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>("/api/v1/messages/search?savedLabel=personal&scope=Personal", JsonOptions))!;
        Assert.Equal(own.MessageId, Assert.Single(matches.Items).MessageId);
        var folder = await WriteP2FolderAsync(setup.FirstClient, Guid.NewGuid(), P2Folder("شخصی", [new(setup.ConversationId, true)]));
        var inbox = (await setup.FirstClient.GetFromJsonAsync<ConversationSummaryResult[]>($"/api/v1/conversations?scope=Personal&folderId={folder.Id}", JsonOptions))!;
        Assert.Contains(inbox, c => c.ConversationId == setup.ConversationId);
        var scheduled = await ScheduleP2Async(setup.FirstClient, setup.ConversationId,
            new ScheduleMessageCommand(DateTime.UtcNow.AddDays(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "personal scheduled" }));
        Assert.Equal(setup.ConversationId, scheduled.ConversationId);
        await CleanupP2SchedulesAsync(setup.First.UserId);
        Assert.Equal(selected, await setup.FirstClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions));
        await factory.EndRoleAssignmentAsync(firstRole.RoleAssignmentId);
        using var blocked = await setup.FirstClient.PostAsync($"/api/v1/messaging/blocks/{setup.Second.UserId}", null);
        blocked.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PersonalAccountInvitationLinkAcceptanceAndManagedGroupRemainIndependentOfTeacherWorkspace()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups");
        var org = await factory.CreateOrganizationAsync(NewOrganizationName());
        foreach (var (client, id) in new[] { (setup.OwnerClient, setup.Owner.UserId), (setup.TargetClient, setup.Target.UserId) })
        {
            var role = await factory.CreateMembershipAsync(id, org, OrganizationRole.Teacher);
            await SelectOrganizationAsync(client, role.MembershipId, OrganizationRole.Teacher);
        }
        var created = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand());
        using var accepted = await RedeemP2Async(setup.TargetClient, created); accepted.EnsureSuccessStatusCode();
        using var repeat = await RedeemP2Async(setup.TargetClient, created); repeat.EnsureSuccessStatusCode();
        Assert.Equal(1, (await ReadP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, created.Link.Id)).UsedCount);
        using var details = await setup.OwnerClient.GetAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}"); details.EnsureSuccessStatusCode();
        var personal = await CreateManagedAndReadAsync(setup.OwnerClient, "groups", PersonalCommand("گروه شخصی مستقل"));
        Assert.Equal(ConversationScope.Personal, personal.Scope);
        Assert.Null(personal.OrganizationId);
        Assert.Equal(WorkspaceType.Organization, (await setup.OwnerClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!.WorkspaceType);
    }

    [Fact]
    public async Task PersonalAccountExplicitReplayAndLiveWorkWithoutChangingSelectedTeacherOrganization()
    {
        using var senderClient = factory.CreateClient(); using var receiverClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient); var receiver = await CreateMessagingUserAsync(receiverClient);
        var senderOrg = await factory.CreateOrganizationAsync(NewOrganizationName()); var receiverOrg = await factory.CreateOrganizationAsync(NewOrganizationName());
        var senderRole = await factory.CreateMembershipAsync(sender.UserId, senderOrg, OrganizationRole.Teacher);
        var receiverRole = await factory.CreateMembershipAsync(receiver.UserId, receiverOrg, OrganizationRole.Teacher);
        await SelectOrganizationAsync(senderClient, senderRole.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(receiverClient, receiverRole.MembershipId, OrganizationRole.Teacher);
        var selected = (await receiverClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!;
        var personal = (await receiverClient.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/changes?scope=Personal", JsonOptions))!;
        var organization = await AccountChangesAsync(receiverClient);
        var events = Channel.CreateUnbounded<MessagingRealtimeNotification>();
        await using var live = CreateHubConnection(receiver.AccessToken);
        live.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, e => events.Writer.TryWrite(e)); await live.StartAsync();
        var direct = await StartDirectAndReadAsync(senderClient, receiver.UserId, ConversationScope.Personal);
        var sent = await SendMessageAndReadAsync(senderClient, direct.ConversationId, Guid.NewGuid(), "شخصی مستقل از تب نقش");
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            MessagingRealtimeNotification item;
            do { item = await events.Reader.ReadAsync(timeout.Token); } while (item.ResourceId != sent.MessageId);
            Assert.Equal(personal.ContextRevision, item.ContextRevision);
        }
        var page = (await receiverClient.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/changes?scope=Personal&cursor=" + Uri.EscapeDataString(personal.NextCursor), JsonOptions))!;
        Assert.Contains(page.Changes, e => e.Conversation?.ResourceId == sent.MessageId);
        Assert.DoesNotContain((await DrainAccountAsync(receiverClient, organization.NextCursor)).Values, e => e.Conversation?.ConversationId == direct.ConversationId);
        var personalInbox = (await receiverClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations?scope=Personal", JsonOptions))!;
        Assert.Contains(personalInbox, c => c.ConversationId == direct.ConversationId && c.OrganizationId == null);
        Assert.DoesNotContain((await receiverClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!, c => c.ConversationId == direct.ConversationId);
        using var wrongScope = await receiverClient.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(personal.NextCursor));
        Assert.Equal(HttpStatusCode.Conflict, wrongScope.StatusCode);
        Assert.Equal(selected, await receiverClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions));
        await factory.RevokeSessionByTokenAsync(receiver.AccessToken);
        using var revoked = await receiverClient.GetAsync("/api/v1/realtime/changes?scope=Personal"); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task PersonalAccountFileAndAvatarRemainPersonalWhileTeacherCardIsHiddenAndAfterRoleEnds()
    {
        using var teacherClient = factory.CreateClient(); using var peerClient = factory.CreateClient();
        var teacher = await CreateMessagingUserAsync(teacherClient); _ = await CreateMessagingUserAsync(peerClient);
        var org = await factory.CreateOrganizationAsync(NewOrganizationName()); var member = await factory.CreateMembershipAsync(teacher.UserId, org, OrganizationRole.Teacher);
        await SelectOrganizationAsync(teacherClient, member.MembershipId, OrganizationRole.Teacher);
        var selected = (await teacherClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!;
        using var multipart = new MultipartFormDataContent(); using var bytes = new ByteArrayContent(StabilizationMediaFixtures.Bytes("image/png"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png"); multipart.Add(bytes, "File", "account-avatar.png");
        using var upload = await teacherClient.PostAsync("/api/v1/files?workspaceScope=Personal", multipart); upload.EnsureSuccessStatusCode();
        var file = (await upload.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
        Assert.Null(file.OrganizationId); Assert.Equal(teacher.UserId, file.UploadedByUserId);
        using (var peer = await peerClient.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.NotFound, peer.StatusCode);
        using (var own = await teacherClient.GetAsync($"/api/v1/files/{file.Id}/content"))
        { own.EnsureSuccessStatusCode(); Assert.Equal(StabilizationMediaFixtures.Bytes("image/png"), await own.Content.ReadAsByteArrayAsync()); }
        using var photo = await teacherClient.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id), JsonOptions); photo.EnsureSuccessStatusCode();
        Assert.Equal(selected, await teacherClient.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions));
        await factory.EndRoleAssignmentAsync(member.RoleAssignmentId);
        using var after = await teacherClient.GetAsync($"/api/v1/files/{file.Id}/content"); after.EnsureSuccessStatusCode();
        Assert.Equal(StabilizationMediaFixtures.Bytes("image/png"), await after.Content.ReadAsByteArrayAsync());
    }
}
