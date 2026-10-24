using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Platform;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AbuseReviewerSeesOnlyReportedItemAndTwoNeighborsPerSideWithAuditedReads()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient(); using var reviewerClient = factory.CreateClient();
        await AuthorizePolicyReviewersAsync(ownerClient, reviewerClient);
        using var senderClient = factory.CreateClient(); using var reporterClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient); var reporter = await CreateMessagingUserAsync(reporterClient);
        var conversation = await StartDirectAndReadAsync(senderClient, reporter.UserId, ConversationScope.Personal);
        MessageReceiptResult selected = null!;
        for (var i = 0; i < 9; i++)
        {
            var sent = await SendMessageAndReadAsync(senderClient, conversation.ConversationId, Guid.NewGuid(), $"bounded-evidence-{i}");
            if (i == 4) selected = sent;
        }
        var caseId = await ReportPolicyMessageAsync(reporterClient, conversation.ConversationId, selected.MessageId);
        using var response = await reviewerClient.GetAsync($"/api/admin/v1/moderation/cases/{caseId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var detail = (await response.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        Assert.Equal("bounded-evidence-4", detail.AbuseReport!.ContentSnapshot);
        Assert.Equal(4, detail.ContextMessages!.Count);
        Assert.Equal(new long[] { 3, 4, 6, 7 }, detail.ContextMessages.Select(m => m.Sequence));
        Assert.DoesNotContain("bounded-evidence-0", json); Assert.DoesNotContain("bounded-evidence-8", json);
        Assert.DoesNotContain(sender.PhoneNumber, json);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.moderation.evidence.view"));
        using var unclaimedExpansion = await ownerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/expanded-context",
            new PlatformModerationVersionCommand(detail.Case.Version, "بدون ارجاع مجاز نیست"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, unclaimedExpansion.StatusCode);
        using var reviewerExpansion = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/expanded-context",
            new PlatformModerationVersionCommand(detail.Case.Version, "نقش محدود است"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, reviewerExpansion.StatusCode);
        using var privateBrowse = await reviewerClient.GetAsync($"/api/v1/conversations/{conversation.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, privateBrowse.StatusCode);
    }

    [Fact]
    public async Task ExpandedEvidenceRequiresEscalationStrongerPermissionAndReason()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient(); using var reviewerClient = factory.CreateClient();
        await AuthorizePolicyReviewersAsync(ownerClient, reviewerClient);
        using var senderClient = factory.CreateClient(); using var reporterClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(senderClient); var reporter = await CreateMessagingUserAsync(reporterClient);
        var conversation = await StartDirectAndReadAsync(senderClient, reporter.UserId, ConversationScope.Personal);
        var message = await SendMessageAndReadAsync(senderClient, conversation.ConversationId, Guid.NewGuid(), "selected-case-only");
        var caseId = await ReportPolicyMessageAsync(reporterClient, conversation.ConversationId, message.MessageId);
        var detail = (await reviewerClient.GetFromJsonAsync<PlatformModerationCaseDetail>($"/api/admin/v1/moderation/cases/{caseId}", JsonOptions))!;
        using var start = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/start-review",
            new PlatformModerationVersionCommand(detail.Case.Version, "شروع بررسی"), JsonOptions);
        start.EnsureSuccessStatusCode(); detail = (await start.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var escalate = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "نیاز به بررسی قوی‌تر", ModerationActionType.Escalated), JsonOptions);
        escalate.EnsureSuccessStatusCode(); detail = (await escalate.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var ownerClaim = await ownerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/start-review",
            new PlatformModerationVersionCommand(detail.Case.Version, "پذیرش ارجاع"), JsonOptions);
        ownerClaim.EnsureSuccessStatusCode(); detail = (await ownerClaim.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var missingReason = await ownerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/expanded-context",
            new PlatformModerationVersionCommand(detail.Case.Version, ""), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using var expanded = await ownerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/expanded-context",
            new PlatformModerationVersionCommand(detail.Case.Version, "بررسی ارجاع‌شده با دلیل"), JsonOptions);
        expanded.EnsureSuccessStatusCode();
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.moderation.evidence.expanded"));
        using var hostile = await ownerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{Guid.NewGuid()}/expanded-context",
            new PlatformModerationVersionCommand(detail.Case.Version, "شناسه نامرتبط"), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, hostile.StatusCode);
    }

    [Fact]
    public async Task CaseScopedReviewerCanWarnAndRestrictMessagingButCannotSuspendAccount()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient(); using var reviewerClient = factory.CreateClient();
        await AuthorizePolicyReviewersAsync(ownerClient, reviewerClient);
        using var senderClient = factory.CreateClient(); using var reporterClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient); var reporter = await CreateMessagingUserAsync(reporterClient);
        var conversation = await StartDirectAndReadAsync(senderClient, reporter.UserId, ConversationScope.Personal);
        var message = await SendMessageAndReadAsync(senderClient, conversation.ConversationId, Guid.NewGuid(), "reported-case-content");
        var caseId = await ReportPolicyMessageAsync(reporterClient, conversation.ConversationId, message.MessageId);
        var detail = (await reviewerClient.GetFromJsonAsync<PlatformModerationCaseDetail>($"/api/admin/v1/moderation/cases/{caseId}", JsonOptions))!;
        using var start = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/start-review",
            new PlatformModerationVersionCommand(detail.Case.Version, "شروع بررسی"), JsonOptions);
        start.EnsureSuccessStatusCode(); detail = (await start.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var warn = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "ثبت اخطار در پرونده", ModerationActionType.Warned), JsonOptions);
        warn.EnsureSuccessStatusCode(); detail = (await warn.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var restrict = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "محدودیت موقت پیام", ModerationActionType.MessagingRestricted, DateTime.UtcNow.AddHours(1)), JsonOptions);
        restrict.EnsureSuccessStatusCode();
        using var denied = await SendMessageAsync(senderClient, conversation.ConversationId, Guid.NewGuid(), "بعد از محدودیت");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var profile = await senderClient.GetAsync("/api/v1/me"); Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var suspend = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/users/{sender.UserId}/suspend", new PlatformUserStateCommand("عملیات قوی‌تر"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, suspend.StatusCode);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.moderation.action.warned"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.moderation.action.messagingrestricted"));
    }

    private async Task AuthorizePolicyReviewersAsync(HttpClient ownerClient, HttpClient reviewerClient)
    {
        var owner = await CreateMessagingUserAsync(ownerClient); var reviewer = await CreateMessagingUserAsync(reviewerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.UserId);
        using var grant = await ownerClient.PostAsJsonAsync("/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(reviewer.UserId, PlatformRoleKeys.AbuseReviewer, "بازبین محدود پرونده"), JsonOptions);
        grant.EnsureSuccessStatusCode();
    }
    private async Task<Guid> ReportPolicyMessageAsync(HttpClient reporter, Guid conversationId, Guid messageId)
    {
        using var report = await reporter.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages/{messageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Harassment, null), JsonOptions);
        report.EnsureSuccessStatusCode(); var receipt = (await report.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions))!;
        return (await factory.GetModerationCaseForReportAsync(receipt.ReportId)).Id;
    }
}
