using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task OfficialHistoryCannotBeReadByForeignSchoolAccountOrMembershipWithoutSourceGrant()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var id = pair.Group.ConversationId!.Value;
        await SendMessageAndReadAsync(scenario.Teacher, id, Guid.NewGuid(), "تاریخچه رسمی محدود به منبع");
        using var outsider = factory.CreateClient(); var account = await CreateMessagingUserAsync(outsider);
        var other = await factory.CreateOrganizationAsync(NewOrganizationName());
        var foreign = await factory.CreateMembershipAsync(account.UserId, other, OrganizationRole.Manager);
        var sameSchool = await factory.CreateMembershipAsync(account.UserId, scenario.OrganizationId, OrganizationRole.Teacher);
        foreach (var selection in new[] { (foreign.MembershipId, OrganizationRole.Manager), (sameSchool.MembershipId, OrganizationRole.Teacher) })
        {
            await SelectOrganizationAsync(outsider, selection.Item1, selection.Item2);
            var inbox = (await outsider.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
            Assert.DoesNotContain(inbox.Items, c => c.ConversationId == id);
            foreach (var path in new[] { "messages", "summary", "changes" })
            {
                using var denied = await outsider.GetAsync($"/api/v1/conversations/{id}/{path}");
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(denied));
            }
        }
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task AccountHistoryUnifiesTwoOrganizationsAndPersonalChatsWithoutChangingLegacyScope()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var actor = scenario.Teacher; var userId = scenario.TeacherId;
        var pairA = (await actor.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var officialA = pairA.Group.ConversationId!.Value;
        var messageA = await SendMessageAndReadAsync(actor, officialA, Guid.NewGuid(), "تاریخچه سازمان الف");
        var organizationB = await factory.CreateOrganizationAsync(NewOrganizationName());
        var memberB = await factory.CreateMembershipAsync(userId, organizationB, OrganizationRole.Manager);
        Guid memberA; Guid officialB;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            memberA = await db.Memberships.Where(m => m.UserId == userId && m.OrganizationId == scenario.OrganizationId).Select(m => m.Id).SingleAsync();
            db.RoleAssignments.Add(RoleAssignment.CreateActive(memberA, OrganizationRole.Parent, DateTime.UtcNow));
            await db.SaveChangesAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(organizationB, null, default));
            officialB = await db.Conversations.Where(c => c.OrganizationId == organizationB && c.IsOfficialOrganizationSpace &&
                c.Type == ConversationType.Group).Select(c => c.Id).SingleAsync();
        }
        await SelectOrganizationAsync(actor, memberB.MembershipId, OrganizationRole.Manager);
        var messageB = await SendMessageAndReadAsync(actor, officialB, Guid.NewGuid(), "تاریخچه سازمان ب");
        await AssertHistoryPersistenceAsync(officialA, userId, scenario.StudentId, messageA);
        await AssertHistoryPersistenceAsync(officialB, userId, userId, messageB);
        using var peerClient = factory.CreateClient(); var peer = await CreateMessagingUserAsync(peerClient);
        var direct = await StartDirectAndReadAsync(actor, peer.UserId, ConversationScope.Personal);
        var group = await CreateManagedAndReadAsync(actor, "groups", PersonalCommand("گروه شخصی ثابت"));
        var channel = await CreateManagedAndReadAsync(actor, "channels", PersonalCommand("کانال شخصی ثابت"));
        var saved = (await actor.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var personalIds = new[] { direct.ConversationId, group.ConversationId, channel.ConversationId, saved.ConversationId };
        foreach (var id in personalIds)
            await SendMessageAndReadAsync(actor, id, Guid.NewGuid(), "تاریخچه شخصی مستقل");
        foreach (var selection in new[] { (memberA, OrganizationRole.Teacher), (memberA, OrganizationRole.Parent),
            (memberB.MembershipId, OrganizationRole.Manager) })
        {
            await SelectOrganizationAsync(actor, selection.Item1, selection.Item2);
            var inbox = (await actor.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
            Assert.All(personalIds.Concat(new[] { officialA, officialB }), id => Assert.Contains(inbox.Items, c => c.ConversationId == id));
            Assert.Contains((await HistoryAsync(actor, officialA)).Messages, m => m.MessageId == messageA.MessageId);
            Assert.Contains((await HistoryAsync(actor, officialB)).Messages, m => m.MessageId == messageB.MessageId);
            Assert.All(await actor.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions) ?? [],
                c => Assert.Equal(selection.Item1 == memberA ? scenario.OrganizationId : organizationB, c.OrganizationId));
            foreach (var id in personalIds) Assert.Single((await HistoryAsync(actor, id)).Messages);
        }
        await SelectPersonalAsync(actor);
        foreach (var id in personalIds.Concat(new[] { officialA, officialB })) Assert.NotEmpty((await HistoryAsync(actor, id)).Messages);
        var legacyPersonal = (await actor.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!;
        Assert.All(legacyPersonal, c => Assert.Null(c.OrganizationId));
        await factory.EndMembershipAsync(memberA);
        foreach (var id in personalIds) Assert.Single((await HistoryAsync(actor, id)).Messages);
        Assert.Contains((await HistoryAsync(actor, officialB)).Messages, m => m.MessageId == messageB.MessageId);
        using var denied = await actor.GetAsync($"/api/v1/conversations/{officialA}/messages");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task HistoryBoundariesPersonalHidingAndDeletedTombstonesStayConsistentWithPreviewAndUnread()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups");
        var id = setup.Conversation.ConversationId;
        using var settings = await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/settings",
            new UpdateConversationSettingsCommand(ConversationVisibility.Private, GroupHistoryPolicy.FromJoin,
                GroupMemberCapability.LegacyMemberDefault, setup.Conversation.Version), JsonOptions);
        settings.EnsureSuccessStatusCode();
        var old = await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "پیش از عضویت");
        await InviteAndAcceptPersonalAsync(setup.OwnerClient, setup.TargetClient, id, setup.Target.UserId);
        Assert.Empty((await HistoryAsync(setup.TargetClient, id)).Messages);
        var initial = (await setup.TargetClient.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{id}/summary", JsonOptions))!;
        Assert.Null(initial.LastMessagePreview); Assert.Equal(0, initial.UnreadCount);
        var visible = await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "پس از عضویت");
        var hidden = await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "پنهان‌شده برای یک عضو");
        using var hide = await setup.TargetClient.DeleteAsync($"/api/v1/conversations/{id}/messages/{hidden.MessageId}/for-me");
        hide.EnsureSuccessStatusCode();
        var history = await HistoryAsync(setup.TargetClient, id, limit: 1);
        Assert.Equal(visible.MessageId, Assert.Single(history.Messages).MessageId);
        Assert.Null(history.NextBeforeSequence);
        var summary = (await setup.TargetClient.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{id}/summary", JsonOptions))!;
        Assert.Equal(visible.Sequence, summary.LastMessageSequence); Assert.Equal(1, summary.UnreadCount);
        using var deleted = await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/messages/{visible.MessageId}/for-everyone?expectedVersion=" + Uri.EscapeDataString(visible.Version));
        deleted.EnsureSuccessStatusCode();
        var tombstone = Assert.Single((await HistoryAsync(setup.TargetClient, id)).Messages);
        Assert.True(tombstone.IsDeleted); Assert.Null(tombstone.Text); Assert.Empty(tombstone.Attachments);
        using var clear = await setup.TargetClient.PostAsync($"/api/v1/conversations/{id}/clear-history", null);
        clear.EnsureSuccessStatusCode();
        Assert.Empty((await HistoryAsync(setup.TargetClient, id)).Messages);
        var cleared = (await setup.TargetClient.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{id}/summary", JsonOptions))!;
        Assert.Null(cleared.LastMessagePreview); Assert.Equal(0, cleared.UnreadCount);
        var newer = await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "پس از پاک‌کردن تاریخچه شخصی");
        Assert.Equal(newer.MessageId, Assert.Single((await HistoryAsync(setup.TargetClient, id)).Messages).MessageId);
        var after = (await setup.TargetClient.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{id}/summary", JsonOptions))!;
        Assert.Equal(newer.Sequence, after.LastMessageSequence); Assert.Equal(1, after.UnreadCount);
        Assert.Contains(await factory.GetMessagesAsync(id), m => m.Id == old.MessageId);
    }
}
