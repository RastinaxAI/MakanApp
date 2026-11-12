using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Support;
using MakanApp.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task ProductV6SupportTicketRepliesAssignmentResolutionAndRetriesRespectCurrentPermissions()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var other = await ActorAsync();
        var command = new CreateSupportTicketCommand(Guid.NewGuid(), "پرسش آزمایشی", "Technical", "متن خصوصی آزمون");
        var created = await PostAsync<SupportTicketResult>(owner.Client, "/api/v1/support/tickets", command);
        var retry = await PostAsync<SupportTicketResult>(owner.Client, "/api/v1/support/tickets", command);
        Assert.Equal(created.Id, retry.Id); Assert.Single(retry.Messages); Assert.Null(retry.OwnerUserId);
        using var hidden = await other.Client.GetAsync($"/api/v1/support/tickets/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var forbidden = await other.Client.GetAsync("/api/admin/v1/support/tickets"); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var queue = await GetAsync<SupportTicketPage>(admin.Client, "/api/admin/v1/support/tickets");
        Assert.Contains(queue.Items, t => t.Id == created.Id && t.Messages.Count == 0 && t.OwnerUserId == owner.UserId);
        var assigned = await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{created.Id}/assign",
            new ManageSupportTicketCommand(Guid.NewGuid(), created.Version, "تخصیص آزمون", admin.UserId));
        var response = new ReplySupportTicketCommand(Guid.NewGuid(), assigned.Version, "پاسخ کارشناس");
        var replied = await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{created.Id}/messages", response);
        var replay = await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{created.Id}/messages", response);
        Assert.Equal(2, replay.Messages.Count); Assert.Equal(replied.Version, replay.Version);
        Assert.Equal(SupportTicketStatus.WaitingForUser, replay.Status);
        var userReply = await PostAsync<SupportTicketResult>(owner.Client, $"/api/v1/support/tickets/{created.Id}/messages",
            new ReplySupportTicketCommand(Guid.NewGuid(), replied.Version, "پاسخ کاربر"));
        Assert.Equal(SupportTicketStatus.WaitingForSupport, userReply.Status);
        var resolve = new ManageSupportTicketCommand(Guid.NewGuid(), userReply.Version, "پایان آزمون");
        var resolved = await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{created.Id}/resolve", resolve);
        Assert.Equal(SupportTicketStatus.Resolved, resolved.Status);
        using var reopen = await owner.Client.PostAsJsonAsync($"/api/v1/support/tickets/{created.Id}/messages",
            new ReplySupportTicketCommand(Guid.NewGuid(), resolved.Version, "بازگشایی بدون سیاست"), Json);
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        await WithDb(async db => {
            Assert.Equal(3, await db.SupportTicketMessages.CountAsync(m => m.TicketId == created.Id));
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.TargetId == created.Id.ToString() && a.Action == "support.Resolved"));
            Assert.False(await db.AdminAuditLogs.AnyAsync(a => a.TargetId == created.Id.ToString() && a.Reason.Contains("متن خصوصی")));
        });
        await factory.ResetPlatformIamAsync();
        using var revoked = await admin.Client.GetAsync($"/api/admin/v1/support/tickets/{created.Id}"); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Fact]
    public async Task ProductV6DeletionRequestIsIdempotentDoesNotDeleteDataOrScheduleGuessedRetention()
    {
        var admin = await AdminAsync();
        var owner = await ActorAsync(); var other = await ActorAsync(); var command = new RequestAccountDeletionCommand(Guid.NewGuid());
        var request = await PostAsync<AccountDeletionStatusResult>(owner.Client, "/api/v1/me/deletion-request", command);
        var retry = await PostAsync<AccountDeletionStatusResult>(owner.Client, "/api/v1/me/deletion-request", command);
        Assert.Equal(request.Id, retry.Id); Assert.Equal("PRODUCT_LEGAL_DECISION_REQUIRED", request.Status);
        Assert.False(request.ExecutionEnabled); Assert.Null(request.ScheduledExecutionAtUtc);
        using var unrelated = await other.Client.GetAsync("/api/v1/me/deletion-request");
        Assert.Equal(HttpStatusCode.NoContent, unrelated.StatusCode);
        Assert.DoesNotContain(request.Id.ToString(), await unrelated.Content.ReadAsStringAsync());
        using var stillLoggedIn = await owner.Client.GetAsync("/api/v1/me"); Assert.True(stillLoggedIn.IsSuccessStatusCode);
        using var forbiddenQueue = await owner.Client.GetAsync("/api/admin/v1/privacy/deletion-requests"); Assert.Equal(HttpStatusCode.Forbidden, forbiddenQueue.StatusCode);
        var queue = await GetAsync<AccountDeletionPage>(admin.Client, "/api/admin/v1/privacy/deletion-requests");
        Assert.Contains(queue.Items, i => i.UserId == owner.UserId && i.Request.Id == request.Id && !i.Request.ExecutionEnabled);
        await WithDb(async db => {
            Assert.Equal(1, await db.AccountDeletionRequests.CountAsync(r => r.UserId == owner.UserId));
            Assert.True(await db.Users.AnyAsync(u => u.Id == owner.UserId && u.AccountStatus == MakanApp.Domain.Identity.UserAccountStatus.Active));
        });
    }
}
