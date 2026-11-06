using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountSummarySurvivesWorkspaceSwitchWithoutGrantingHistoryFileOrHubAccess()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var id = pair.Group.ConversationId!.Value;
        var route = $"/api/v1/conversations/{id}";
        var file = await UploadFileAsync(scenario.Teacher, "text/plain", "workspace.txt");
        var token = scenario.Teacher.DefaultRequestHeaders.Authorization!.Parameter!;
        Guid membershipId;
        await using (var scope = factory.Services.CreateAsyncScope())
            membershipId = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Memberships
                .Where(m => m.UserId == scenario.TeacherId && m.OrganizationId == scenario.OrganizationId)
                .Select(m => m.Id).SingleAsync();

        await using var hub = CreateHubConnection(token);
        await hub.StartAsync();
        await AssertFullAccessAsync();
        await SelectPersonalAsync(scenario.Teacher);
        await AssertSummaryAsync();
        await AssertFullAccessDeniedAsync();

        var otherOrganization = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherMembership = await factory.CreateMembershipAsync(scenario.TeacherId, otherOrganization, OrganizationRole.Teacher);
        await SelectOrganizationAsync(scenario.Teacher, otherMembership.MembershipId, OrganizationRole.Teacher);
        await AssertSummaryAsync();
        await AssertFullAccessDeniedAsync();

        await SelectOrganizationAsync(scenario.Teacher, membershipId, OrganizationRole.Teacher);
        await AssertFullAccessAsync();

        async Task AssertSummaryAsync()
        {
            using var response = await scenario.Teacher.GetAsync(route + "/summary");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var summary = (await response.Content.ReadFromJsonAsync<ConversationSummaryResult>(JsonOptions))!;
            Assert.Equal(id, summary.ConversationId);
            Assert.Equal(scenario.OrganizationId, summary.OrganizationId);
            var inbox = (await scenario.Teacher.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
            Assert.Equal(JsonSerializer.Serialize(inbox.Items.Single(c => c.ConversationId == id), JsonOptions),
                JsonSerializer.Serialize(summary, JsonOptions));
        }

        async Task AssertFullAccessAsync()
        {
            foreach (var path in new[] { route, route + "/messages", $"/api/v1/files/{file.Id}/content" })
            { using var response = await scenario.Teacher.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
            await hub.InvokeAsync("SubscribeConversation", id);
            await hub.InvokeAsync("UnsubscribeConversation", id);
        }

        async Task AssertFullAccessDeniedAsync()
        {
            foreach (var path in new[] { route, route + "/messages" })
            { using var response = await scenario.Teacher.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
            // مالک فایلِ هنوز متصل‌نشده شناخته شده است؛ قرارداد فعلی اختلاف scope را 403 می‌دهد.
            using var deniedFile = await scenario.Teacher.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.Forbidden, deniedFile.StatusCode);
            Assert.Equal(StorageErrorCodes.FileNotAllowed, await ReadProblemCodeAsync(deniedFile));
            await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("SubscribeConversation", id));
        }
    }

    [Theory]
    [InlineData("Membership")]
    [InlineData("Role")]
    [InlineData("Participant")]
    [InlineData("TeacherAssignment")]
    [InlineData("Organization")]
    public async Task AccountSummaryImmediatelyConcealsRevokedAccessWithoutReconciliation(string revocation)
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var id = pair.Group.ConversationId!.Value;
        var route = $"/api/v1/conversations/{id}/summary";
        await SelectPersonalAsync(scenario.Teacher);
        using (var before = await scenario.Teacher.GetAsync(route)) Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var membership = await db.Memberships.SingleAsync(m => m.UserId == scenario.TeacherId && m.OrganizationId == scenario.OrganizationId);
            switch (revocation)
            {
                case "Membership": membership.End(DateTime.UtcNow); break;
                case "Role": (await db.RoleAssignments.SingleAsync(r => r.MembershipId == membership.Id)).End(DateTime.UtcNow); break;
                case "Participant": (await db.ConversationParticipants.SingleAsync(p => p.ConversationId == id && p.UserId == scenario.TeacherId)).Remove(scenario.ManagerId, DateTime.UtcNow); break;
                case "TeacherAssignment": (await db.TeacherAssignments.SingleAsync(a => a.Id == scenario.TeacherAssignmentId)).End(DateTime.UtcNow); break;
                case "Organization": (await db.Organizations.SingleAsync(o => o.Id == scenario.OrganizationId)).Suspend(); break;
            }
            await db.SaveChangesAsync();
        }
        using var after = await scenario.Teacher.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(after));
        var inbox = (await scenario.Teacher.GetFromJsonAsync<AccountInboxPageResult>("/api/v1/conversations/account", JsonOptions))!;
        Assert.DoesNotContain(inbox.Items, item => item.ConversationId == id);
    }

    [Fact]
    public async Task AccountSummaryConcealsGuessedAndForeignConversationAndRejectsRevokedSession()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var pair = (await scenario.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>(
            $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        using var outsider = factory.CreateClient();
        var user = await CreateMessagingUserAsync(outsider);
        var other = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(user.UserId, other, OrganizationRole.Manager);
        await SelectOrganizationAsync(outsider, membership.MembershipId, OrganizationRole.Manager);
        foreach (var id in new[] { pair.Group.ConversationId!.Value, Guid.NewGuid(), Guid.Empty })
        {
            using var response = await outsider.GetAsync($"/api/v1/conversations/{id}/summary");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(response));
        }
        await factory.RevokeSessionByTokenAsync(scenario.Teacher.DefaultRequestHeaders.Authorization!.Parameter!);
        using var revoked = await scenario.Teacher.GetAsync($"/api/v1/conversations/{pair.Group.ConversationId}/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }
}
