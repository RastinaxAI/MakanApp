using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Realtime;
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
    [Trait("Category", "MessengerHistory")]
    public async Task ExistingConversationAppearsInAccountInboxAndHistory()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var message = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "پیام پایدار پذیرش");
        var inbox = (await setup.SecondClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        var summary = Assert.Single(inbox.Items, c => c.ConversationId == setup.ConversationId);
        Assert.Equal(message.Sequence, summary.LastMessageSequence);
        Assert.Equal(1, summary.UnreadCount);
        Assert.Equal(message.MessageId, Assert.Single((await HistoryAsync(setup.SecondClient, setup.ConversationId)).Messages).MessageId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await db.Conversations.AsNoTracking().AnyAsync(c => c.Id == setup.ConversationId));
        Assert.Equal(2, await db.ConversationParticipants.CountAsync(p => p.ConversationId == setup.ConversationId &&
            p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null));
        var persisted = await db.Messages.AsNoTracking().SingleAsync(m => m.Id == message.MessageId);
        Assert.Equal(message.Sequence, persisted.Sequence);
        Assert.Equal(setup.First.UserId, persisted.SenderUserId);
        Assert.Equal(message.SentAtUtc, persisted.SentAtUtc);
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public Task ConfirmedMessageSurvivesFreshLoginSession() => AssertHistoryAcrossSessionAsync(endOldSession: true);

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public Task SecondDeviceReceivesSameAuthorizedHistory() => AssertHistoryAcrossSessionAsync(endOldSession: false);

    private async Task AssertHistoryAcrossSessionAsync(bool endOldSession)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var message = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "تاریخچه مستقل از نشست");
        // دو گفتگوی شخصی برای تولید Cursor واقعی صفحهٔ اول، بدون انتقال Cache به نشست تازه.
        _ = await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions);
        var firstPage = (await setup.FirstClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account?limit=1", JsonOptions))!;
        Assert.NotNull(firstPage.NextCursor);
        if (endOldSession)
        {
            using var logout = await setup.FirstClient.PostAsync("/api/v1/auth/logout", null);
            logout.EnsureSuccessStatusCode();
        }
        using var device = factory.CreateClient();
        await LoginHistoryAccountAsync(device, setup.First.PhoneNumber);
        var inbox = (await device.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.Contains(inbox.Items, c => c.ConversationId == setup.ConversationId);
        Assert.Equal(message.MessageId, Assert.Single((await HistoryAsync(device, setup.ConversationId)).Messages).MessageId);
        using var cursor = await device.GetAsync("/api/v1/conversations/account?limit=1&cursor=" + Uri.EscapeDataString(firstPage.NextCursor));
        Assert.Equal(HttpStatusCode.BadRequest, cursor.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessagePageInvalid, await ReadProblemCodeAsync(cursor));
        if (endOldSession)
        {
            using var revoked = await setup.FirstClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/messages");
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        }
        else
            Assert.Equal(message.MessageId, Assert.Single((await HistoryAsync(setup.FirstClient, setup.ConversationId)).Messages).MessageId);
    }

    private async Task LoginHistoryAccountAsync(HttpClient client, string phone)
    {
        using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone), JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var verifiedResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, factory.GetLatestOtpCode(phone)), JsonOptions);
        verifiedResponse.EnsureSuccessStatusCode();
        var verified = (await verifiedResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        UseBearerToken(client, verified.AccessToken);
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task HistoryPaginationHasNoMissingOrDuplicateMessages()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = new List<MessageReceiptResult>();
        for (var index = 0; index < 65; index++)
            sent.Add(await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "پیام صفحه‌بندی " + index));
        sent.AddRange(await Task.WhenAll(Enumerable.Range(65, 10).Select(index =>
            SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "پیام همزمان " + index))));
        var seen = new HashSet<Guid>(); var sequences = new List<long>(); long? before = null; var pages = 0;
        do
        {
            var page = await HistoryAsync(setup.SecondClient, setup.ConversationId, before, 20);
            var rows = page.Messages.ToArray();
            Assert.Equal(rows.Select(m => m.Sequence).OrderBy(s => s), rows.Select(m => m.Sequence));
            Assert.All(rows, m => { Assert.True(seen.Add(m.MessageId)); Assert.True(before is null || m.Sequence < before); });
            sequences.AddRange(rows.Select(m => m.Sequence));
            before = page.NextBeforeSequence; pages++;
            Assert.True(pages <= 4);
        } while (before.HasValue);
        Assert.Equal(4, pages); Assert.Equal(75, seen.Count);
        Assert.True(seen.SetEquals(sent.Select(m => m.MessageId)));
        Assert.Equal(Enumerable.Range(1, 75).Select(i => (long)i), sequences.OrderBy(i => i));
        var persisted = await factory.GetMessagesAsync(setup.ConversationId);
        Assert.Equal(75, persisted.Length);
        Assert.Equal(sequences.OrderBy(i => i), persisted.Select(m => m.Sequence));
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task ReconnectReconcilesDurableHistoryWithoutDuplicate()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var first = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "پیش از قطع اتصال");
        var boundary = (await setup.SecondClient.GetFromJsonAsync<AccountChangePageResult>("/api/v1/realtime/account-inbox/changes", JsonOptions))!;
        await using var hub = CreateHubConnection(setup.Second.AccessToken);
        await hub.StartAsync(); await hub.StopAsync();
        var operation = Guid.NewGuid();
        var second = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, operation, "هنگام قطع اتصال");
        var repeated = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, operation, "هنگام قطع اتصال");
        Assert.Equal(second.MessageId, repeated.MessageId);
        var third = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "پیام بعدی آفلاین");
        await hub.StartAsync();
        var changes = await DrainPolicyAccountChangesAsync(setup.SecondClient, boundary.NextCursor);
        var replayed = await DrainPolicyAccountChangesAsync(setup.SecondClient, boundary.NextCursor);
        Assert.True(changes.Keys.ToHashSet().SetEquals(replayed.Keys));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var expected = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().MessagingChangeEvents
                .Where(e => e.ChangeType == MessagingChangeType.MessageCreated &&
                    (e.ResourceId == second.MessageId || e.ResourceId == third.MessageId)).Select(e => e.Id).ToArrayAsync();
            Assert.Equal(2, expected.Length);
            Assert.All(expected, eventId => Assert.Contains(eventId, changes.Keys));
        }
        Assert.All(changes.Values, c => { Assert.Null(c.Conversation); Assert.Equal("messaging.account-inbox", c.Invalidation!.Key); });
        var history = await HistoryAsync(setup.SecondClient, setup.ConversationId);
        var delta = (await setup.SecondClient.GetFromJsonAsync<ConversationChangePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/changes", JsonOptions))!;
        var created = delta.Changes.Where(c => c.Type == MessagingChangeType.MessageCreated).Select(c => c.ResourceId).ToArray();
        var merged = history.Messages.Select(m => m.MessageId).Concat(created).ToHashSet();
        Assert.True(merged.SetEquals(new[] { first.MessageId, second.MessageId, third.MessageId }));
        Assert.Equal(3, history.Messages.Count);
        Assert.Equal(3, (await factory.GetMessagesAsync(setup.ConversationId)).Length);
        Assert.Equal(3, delta.Changes.Count(c => c.Type == MessagingChangeType.MessageCreated));
    }

    [Theory]
    [InlineData("Direct")]
    [InlineData("Group")]
    [InlineData("Channel")]
    [InlineData("OfficialGroup")]
    [InlineData("OfficialChannel")]
    [Trait("Category", "MessengerHistory")]
    public async Task DirectGroupChannelAndOfficialHistoryRemainReadable(string kind)
    {
        if (kind.StartsWith("Official", StringComparison.Ordinal))
        {
            using var scenario = await CreateOfficialScenarioAsync();
            var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
                $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
            var id = (kind == "OfficialGroup" ? pair.Group : pair.Channel).ConversationId!.Value;
            var sent = await SendMessageAndReadAsync(scenario.Teacher, id, Guid.NewGuid(), "پیام رسمی پایدار");
            await SelectPersonalAsync(scenario.Student);
            Assert.Contains((await HistoryAsync(scenario.Student, id)).Messages, m => m.MessageId == sent.MessageId);
            await AssertHistoryPersistenceAsync(id, scenario.TeacherId, scenario.StudentId, sent);
            return;
        }
        using var setup = await CreateAdvancedConversationSetupAsync();
        var conversationId = setup.ConversationId;
        if (kind != "Direct")
        {
            await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
            conversationId = (await CreateManagedAndReadAsync(setup.FirstClient, kind == "Group" ? "groups" : "channels",
                PersonalCommand("پذیرش تاریخچه " + kind, setup.Second.UserId))).ConversationId;
        }
        var message = await SendMessageAndReadAsync(setup.FirstClient, conversationId, Guid.NewGuid(), "پیام شخصی پایدار");
        Assert.Contains((await HistoryAsync(setup.SecondClient, conversationId)).Messages, m => m.MessageId == message.MessageId);
        var inbox = (await setup.SecondClient.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.Contains(inbox.Items, c => c.ConversationId == conversationId);
        Assert.Single(await factory.GetMessagesAsync(conversationId));
        await AssertHistoryPersistenceAsync(conversationId, setup.First.UserId, setup.Second.UserId, message);
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task WorkspaceRoleSwitchDoesNotHideAuthorizedAccountHistory()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var id = pair.Group.ConversationId!.Value;
        var message = await SendMessageAndReadAsync(scenario.Teacher, id, Guid.NewGuid(), "پیام پذیرش تاریخچه");
        var other = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(scenario.TeacherId, other, OrganizationRole.Parent);
        await SelectOrganizationAsync(scenario.Teacher, membership.MembershipId, OrganizationRole.Parent);

        var inbox = (await scenario.Teacher.GetFromJsonAsync<AccountInboxPageResult>(
            "/api/v1/conversations/account", JsonOptions))!;
        Assert.Contains(inbox.Items, item => item.ConversationId == id);
        using var response = await scenario.Teacher.GetAsync($"/api/v1/conversations/{id}/messages");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = (await response.Content.ReadFromJsonAsync<ConversationMessagePageResult>(JsonOptions))!;
        Assert.Contains(history.Messages, item => item.MessageId == message.MessageId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await db.Messages.AsNoTracking().AnyAsync(m => m.Id == message.MessageId &&
            m.ConversationId == id && m.SenderUserId == scenario.TeacherId && m.Sequence == message.Sequence));
        var current = (await scenario.Teacher.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!;
        Assert.Equal(membership.MembershipId, current.MembershipId);
        // داشتن TeacherAssignment معتبر با تغییر Role نمایشی به Parent از بین نمی‌رود.
        Guid teacherMembership;
        await using (var roleScope = factory.Services.CreateAsyncScope())
        {
            var context = roleScope.ServiceProvider.GetRequiredService<MakanDbContext>();
            teacherMembership = await context.Memberships.Where(m => m.UserId == scenario.TeacherId && m.OrganizationId == scenario.OrganizationId)
                .Select(m => m.Id).SingleAsync();
            context.RoleAssignments.Add(RoleAssignment.CreateActive(teacherMembership, OrganizationRole.Parent, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }
        await SelectOrganizationAsync(scenario.Teacher, teacherMembership, OrganizationRole.Parent);
        Assert.Contains((await HistoryAsync(scenario.Teacher, id)).Messages, m => m.MessageId == message.MessageId);
    }

    [Theory]
    [InlineData("Membership")]
    [InlineData("Role")]
    [InlineData("Participant")]
    [InlineData("TeacherAssignment")]
    [InlineData("Enrollment")]
    [InlineData("Organization")]
    [Trait("Category", "MessengerHistory")]
    public async Task RevokedOfficialMembershipCannotReadHistory(string revocation)
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var id = pair.Group.ConversationId!.Value;
        var sent = await SendMessageAndReadAsync(scenario.Teacher, id, Guid.NewGuid(), "پیام پیش از لغو مجوز");
        var client = revocation == "Enrollment" ? scenario.Student : scenario.Teacher;
        var userId = revocation == "Enrollment" ? scenario.StudentId : scenario.TeacherId;
        await SelectPersonalAsync(client);
        Assert.Contains((await HistoryAsync(client, id)).Messages, m => m.MessageId == sent.MessageId);
        var oldChanges = (await client.GetFromJsonAsync<ConversationChangePageResult>($"/api/v1/conversations/{id}/changes", JsonOptions))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var member = await db.Memberships.SingleAsync(m => m.UserId == userId && m.OrganizationId == scenario.OrganizationId);
            switch (revocation)
            {
                case "Membership": member.End(DateTime.UtcNow); break;
                case "Role": (await db.RoleAssignments.SingleAsync(r => r.MembershipId == member.Id)).End(DateTime.UtcNow); break;
                case "Participant": (await db.ConversationParticipants.SingleAsync(p => p.UserId == userId && p.ConversationId == id)).Remove(scenario.ManagerId, DateTime.UtcNow); break;
                case "TeacherAssignment": (await db.TeacherAssignments.SingleAsync(a => a.Id == scenario.TeacherAssignmentId)).End(DateTime.UtcNow); break;
                case "Enrollment": (await db.Enrollments.SingleAsync(e => e.Id == scenario.EnrollmentId)).Withdraw(DateTime.UtcNow); break;
                case "Organization": (await db.Organizations.SingleAsync(o => o.Id == scenario.OrganizationId)).Suspend(); break;
            }
            await db.SaveChangesAsync();
        }
        var inbox = (await client.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.DoesNotContain(inbox.Items, c => c.ConversationId == id);
        foreach (var path in new[] { "messages", "summary", "changes?afterCursor=" + Uri.EscapeDataString(oldChanges.NextCursor) })
        {
            using var denied = await client.GetAsync($"/api/v1/conversations/{id}/{path}");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(denied));
        }
        Assert.Contains(await factory.GetMessagesAsync(id), m => m.Id == sent.MessageId);
    }

    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task HistoryEmptyStateAndInvalidSelectorsStayDistinctFromAuthorizationErrors()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        Assert.Empty((await HistoryAsync(setup.FirstClient, setup.ConversationId)).Messages);
        using var outsider = factory.CreateClient(); await CreateMessagingUserAsync(outsider);
        var inbox = (await outsider.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.Empty(inbox.Items);
        foreach (var id in new[] { setup.ConversationId, Guid.NewGuid() })
        {
            using var denied = await outsider.GetAsync($"/api/v1/conversations/{id}/messages");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        foreach (var query in new[] { "limit=0", "beforeSequence=0", "limit=101" })
        {
            using var invalid = await setup.FirstClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/messages?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var invalidCursor = await setup.FirstClient.GetAsync("/api/v1/conversations/account?cursor=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
    }

    private static async Task<ConversationMessagePageResult> HistoryAsync(HttpClient client, Guid id, long? before = null, int limit = 20) =>
        (await client.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{id}/messages?limit={limit}" +
            (before.HasValue ? "&beforeSequence=" + before.Value : ""), JsonOptions))!;

    private async Task AssertHistoryPersistenceAsync(Guid conversationId, Guid senderId, Guid readerId, MessageReceiptResult receipt)
    {
        // هر نوع گفتگو با DbContext تازه بررسی می‌شود؛ موفقیت HTTP یا event زنده جای persistence نیست.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await db.Conversations.AsNoTracking().AnyAsync(c => c.Id == conversationId && c.Status == ConversationStatus.Active));
        foreach (var userId in new[] { senderId, readerId }.Distinct())
            Assert.True(await db.ConversationParticipants.AsNoTracking().AnyAsync(p => p.ConversationId == conversationId &&
                p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null));
        var stored = await db.Messages.AsNoTracking().SingleAsync(m => m.Id == receipt.MessageId);
        Assert.Equal(conversationId, stored.ConversationId);
        Assert.Equal(senderId, stored.SenderUserId);
        Assert.Equal(receipt.Sequence, stored.Sequence);
        Assert.Equal(receipt.SentAtUtc, stored.SentAtUtc);
    }
}
