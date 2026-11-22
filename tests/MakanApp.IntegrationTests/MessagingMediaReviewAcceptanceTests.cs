using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("application/pdf", "گزارش درس.pdf", false)]
    [InlineData("application/pdf", "گزارش با توضیح.pdf", true)]
    [InlineData("image/jpeg", "تصویر درس.jpg", false)]
    [InlineData("image/jpeg", "تصویر با توضیح.jpg", true)]
    [InlineData("image/png", "تمرین فارسی.png", false)]
    [InlineData("image/png", "تمرین با توضیح.png", true)]
    public async Task MediaReviewActualProviderApprovedBytesReachRealtimeDeltaAndAuthorizedReceiver(string mime, string name, bool caption)
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, mime, name);
        var beforeReview = await MediaStatusAsync(setup.SenderClient, file.Id);
        Assert.Equal(MediaReviewState.NotRequested, beforeReview.State); Assert.False(beforeReview.AttachmentAllowed);
        var command = MediaCommand(file, caption ? "فایل را ببین" : null);
        using (var pending = await MediaPostAsync(setup, command)) await AssertMediaPendingAsync(pending);
        var status = await MediaStatusAsync(setup.SenderClient, file.Id);
        Assert.Equal(MediaReviewState.PendingReview, status.State); Assert.False(status.AttachmentAllowed);
        using (var denied = await setup.RecipientClient.GetAsync($"/api/v1/files/{file.Id}/content"))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var reviewed = await DecideMediaAsync(setup.ModeratorClient, status.ReviewReference!.Value, ModerationActionType.MediaApproved);
        Assert.Equal(MediaReviewState.Approved, reviewed.MediaReview!.State);
        Assert.True((await MediaStatusAsync(setup.SenderClient, file.Id)).AttachmentAllowed);

        var baseline = await GetChangesAsync(setup.RecipientClient, setup.ConversationId);
        await using var hub = CreateHubConnection(setup.Recipient.AccessToken);
        var received = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, notification =>
        {
            if (notification.ConversationId == setup.ConversationId && notification.Type == MessagingChangeType.MessageCreated)
                received.TrySetResult(notification);
        });
        await hub.StartAsync(); await hub.InvokeAsync("SubscribeConversation", setup.ConversationId);
        using var send = await MediaPostAsync(setup, command); send.EnsureSuccessStatusCode();
        var receipt = (await send.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
        var notification = await factory.WaitForRealtimeAsync(received.Task, receipt.MessageId, TimeSpan.FromSeconds(10));
        Assert.Equal(receipt.MessageId, notification.ResourceId);
        var delta = await GetChangesAsync(setup.RecipientClient, setup.ConversationId, baseline.NextCursor);
        Assert.Contains(delta.Changes, change => change.ResourceId == receipt.MessageId && change.Type == MessagingChangeType.MessageCreated);
        var history = (await setup.RecipientClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!;
        var message = Assert.Single(history.Messages);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal(file.Id, attachment.FileAssetId); Assert.Equal(name, attachment.FileName); Assert.Equal(mime, attachment.ContentType);
        Assert.Equal(command.Text, message.Text);
        using var download = await setup.RecipientClient.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(file.Sha256Hash, Convert.ToHexString(SHA256.HashData(await download.Content.ReadAsByteArrayAsync())), ignoreCase: true);
        using var replay = await MediaPostAsync(setup, command); replay.EnsureSuccessStatusCode();
        Assert.Equal(receipt.MessageId, (await replay.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!.MessageId);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.Messages.CountAsync(m => m.ConversationId == setup.ConversationId));
        Assert.Equal(1, await db.MessageAttachments.CountAsync(a => a.FileAssetId == file.Id));
        Assert.Equal(1, await db.ModerationCases.CountAsync(c => c.MediaFileAssetId == file.Id));
        Assert.False(await db.EducationalSourceVersions.AnyAsync(v => v.FileAssetId == file.Id));
        Assert.False(await db.Subscriptions.AnyAsync(s => s.OwnerId == setup.Sender.UserId));
    }

    [Fact]
    public async Task MediaReviewConcurrentRequestsAcrossConversationsReuseOneFileCaseAndKeepRecipientsPrivate()
    {
        using var setup = await CreateMediaReviewSetupAsync(); using var otherRecipient = factory.CreateClient();
        var other = await CreateMessagingUserAsync(otherRecipient);
        var otherConversation = await StartDirectAndReadAsync(setup.SenderClient, other.UserId, ConversationScope.Personal);
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "بررسی هم‌زمان.pdf");
        var command = MediaCommand(file);
        var tasks = Enumerable.Range(0, 6).Select(async index =>
        {
            using var response = await setup.SenderClient.PostAsJsonAsync($"/api/v1/conversations/{(index % 2 == 0 ? setup.ConversationId : otherConversation.ConversationId)}/messages", command, JsonOptions);
            await AssertMediaPendingAsync(response);
        });
        await Task.WhenAll(tasks);
        var status = await MediaStatusAsync(setup.SenderClient, file.Id);
        using (var changed = await MediaPostAsync(setup, MediaCommand(file, "متن متفاوت", command.ClientMessageId)))
        {
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            Assert.Equal(MessagingErrorCodes.MessageIdempotencyConflict, await ReadProblemCodeAsync(changed));
        }
        foreach (var client in new[] { setup.RecipientClient, otherRecipient })
        {
            using var content = await client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, content.StatusCode);
            using var review = await client.GetAsync($"/api/v1/files/{file.Id}/messaging-review"); Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
        }
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.ModerationCases.CountAsync(c => c.MediaFileAssetId == file.Id));
        Assert.Equal(1, await db.ModerationEvidenceAttachments.CountAsync(a => a.FileAssetId == file.Id));
        Assert.Equal(2, await db.SafetyIncidents.CountAsync(i => i.ActorUserId == setup.Sender.UserId && i.ClientMessageId == command.ClientMessageId));
        Assert.False(await db.MessageAttachments.AnyAsync(a => a.FileAssetId == file.Id));
        Assert.NotNull(status.ReviewReference);
    }

    [Fact]
    public async Task MediaReviewRejectedFileStaysDeniedWithoutDuplicateCasesOrBindings()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "image/png", "رد رسانه.png"); var command = MediaCommand(file);
        using (var pending = await MediaPostAsync(setup, command)) await AssertMediaPendingAsync(pending);
        var status = await MediaStatusAsync(setup.SenderClient, file.Id);
        await DecideMediaAsync(setup.ModeratorClient, status.ReviewReference!.Value, ModerationActionType.MediaRejected);
        for (var index = 0; index < 2; index++)
        {
            using var denied = await MediaPostAsync(setup, command);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
            using var error = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            Assert.Equal("Rejected", error.RootElement.GetProperty("safetyState").GetString());
            Assert.Equal("MEDIA_REJECTED_KEEP_DRAFT", error.RootElement.GetProperty("requiredAction").GetString());
        }
        var after = await MediaStatusAsync(setup.SenderClient, file.Id); Assert.Equal(MediaReviewState.Rejected, after.State); Assert.False(after.AttachmentAllowed);
        using var ownerDownload = await setup.SenderClient.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.OK, ownerDownload.StatusCode);
        using var receiverDownload = await setup.RecipientClient.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, receiverDownload.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.ModerationCases.CountAsync(c => c.MediaFileAssetId == file.Id));
        Assert.Empty(await db.Messages.Where(m => m.ConversationId == setup.ConversationId).ToArrayAsync());
    }

    [Fact]
    public async Task MediaReviewRequiresAssignedEnforcementPermissionAndExplicitDecisionInsteadOfCaseClosure()
    {
        using var setup = await CreateMediaReviewSetupAsync(); using var reviewerClient = factory.CreateClient();
        var reviewer = await CreateMessagingUserAsync(reviewerClient);
        using (var grant = await setup.ModeratorClient.PostAsJsonAsync("/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(reviewer.UserId, PlatformRoleKeys.AbuseReviewer, "بازبین محدود آزمایشی"), JsonOptions)) grant.EnsureSuccessStatusCode();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "مجوز بررسی.pdf");
        using (var pending = await MediaPostAsync(setup, MediaCommand(file))) await AssertMediaPendingAsync(pending);
        var status = await MediaStatusAsync(setup.SenderClient, file.Id); var caseId = status.ReviewReference!.Value;
        var detail = await ClaimMediaAsync(reviewerClient, caseId);
        using (var denied = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "تلاش بدون مجوز قوی", ModerationActionType.MediaApproved), JsonOptions))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var close = await reviewerClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{caseId}/resolve",
            new PlatformCompleteModerationCommand(detail.Case.Version, "بستن بدون تأیید رسانه", null), JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        Assert.False((await MediaStatusAsync(setup.SenderClient, file.Id)).AttachmentAllowed);
    }

    [Fact]
    public async Task MediaReviewApprovalDoesNotBypassCurrentBlockUnsafeCaptionOrOtherUserOwnership()
    {
        using var setup = await CreateMediaReviewSetupAsync(); using var outsiderClient = factory.CreateClient();
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "کنترل مجدد.pdf");
        using (var pending = await MediaPostAsync(setup, MediaCommand(file))) await AssertMediaPendingAsync(pending);
        await DecideMediaAsync(setup.ModeratorClient, (await MediaStatusAsync(setup.SenderClient, file.Id)).ReviewReference!.Value, ModerationActionType.MediaApproved);
        using (var unsafeCaption = await MediaPostAsync(setup, MediaCommand(file, "می کشمت")))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, unsafeCaption.StatusCode);
            Assert.Equal(MessagingErrorCodes.MessageSafetyBlocked, await ReadProblemCodeAsync(unsafeCaption));
        }
        var outsidersConversation = await StartDirectAndReadAsync(outsiderClient, setup.Sender.UserId, ConversationScope.Personal);
        using (var foreignFile = await outsiderClient.PostAsJsonAsync($"/api/v1/conversations/{outsidersConversation.ConversationId}/messages", MediaCommand(file), JsonOptions))
            Assert.Equal(MessagingErrorCodes.MessageAttachmentNotAllowed, await ReadProblemCodeAsync(foreignFile));
        using (var block = await setup.RecipientClient.PostAsync($"/api/v1/messaging/blocks/{setup.Sender.UserId}", null)) block.EnsureSuccessStatusCode();
        using (var blocked = await MediaPostAsync(setup, MediaCommand(file))) Assert.False(blocked.IsSuccessStatusCode);
        using var outsiderDownload = await outsiderClient.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, outsiderDownload.StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
    }

    [Fact]
    public async Task MediaReviewOpenApiExposesActualStatusAndExistingExplicitModerationActions()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        using var api = JsonDocument.Parse(await setup.SenderClient.GetStringAsync("/openapi/v1.json"));
        Assert.True(api.RootElement.GetProperty("paths").TryGetProperty("/api/v1/files/{fileId}/messaging-review", out _));
        var schemas = api.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Contains(schemas.EnumerateObject(), s => s.Name.EndsWith("MediaReviewStatusResult", StringComparison.Ordinal));
        var action = schemas.GetProperty("ModerationActionType").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("MediaApproved", action); Assert.Contains("MediaRejected", action);
    }

    [Fact]
    public async Task MediaReviewCannotBeSelfApprovedAndStaleDecisionCannotCreateSecondEffect()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        using (var grant = await setup.ModeratorClient.PostAsJsonAsync("/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(setup.Sender.UserId, PlatformRoleKeys.PlatformOwner, "مالک دوم فقط در Fixture ایزوله"), JsonOptions)) grant.EnsureSuccessStatusCode();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "عدم خودتأییدی.pdf");
        using (var pending = await MediaPostAsync(setup, MediaCommand(file))) await AssertMediaPendingAsync(pending);
        var id = (await MediaStatusAsync(setup.SenderClient, file.Id)).ReviewReference!.Value;
        var detail = await ClaimMediaAsync(setup.ModeratorClient, id);
        using (var self = await setup.SenderClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "تلاش خودتأییدی", ModerationActionType.MediaApproved), JsonOptions))
            Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        var command = new PlatformModerationActionCommand(detail.Case.Version, "تصمیم صریح بازبین مستقل", ModerationActionType.MediaApproved);
        using (var approved = await setup.ModeratorClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/actions", command, JsonOptions)) approved.EnsureSuccessStatusCode();
        using (var stale = await setup.ModeratorClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/actions", command, JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.ModerationActions.CountAsync(a => a.ModerationCaseId == id && a.Type == ModerationActionType.MediaApproved));
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.Action == "platform.moderation.action.mediaapproved"));
    }

    [Fact]
    public async Task MediaReviewSqlEnforcesUniqueFileAndRejectsNullDecisionByCheckConstraint()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "قیدهای پایگاه داده.pdf");
        using (var pending = await MediaPostAsync(setup, MediaCommand(file))) await AssertMediaPendingAsync(pending);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.ModerationCases.Add(ModerationCase.CreateForMediaReview(setup.ConversationId, file.Id, file.Sha256Hash!, file.ContentType, file.SizeBytes,
                factory.Clock.GetUtcNow().UtcDateTime));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var review = await db.ModerationCases.SingleAsync(c => c.MediaFileAssetId == file.Id);
            db.Entry(review).Property(c => c.MediaDecision).CurrentValue = null;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task MediaReviewApprovedOrganizationFileCannotMoveToAnotherOrganization()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var firstOrg = await factory.CreateOrganizationAsync(NewOrganizationName()); var secondOrg = await factory.CreateOrganizationAsync(NewOrganizationName());
        var firstManager = await factory.CreateMembershipAsync(setup.Sender.UserId, firstOrg, OrganizationRole.Manager);
        var secondManager = await factory.CreateMembershipAsync(setup.Sender.UserId, secondOrg, OrganizationRole.Manager);
        await factory.CreateMembershipAsync(setup.Recipient.UserId, firstOrg, OrganizationRole.Teacher);
        await factory.CreateMembershipAsync(setup.Recipient.UserId, secondOrg, OrganizationRole.Teacher);
        await SelectOrganizationAsync(setup.SenderClient, firstManager.MembershipId, OrganizationRole.Manager);
        var firstConversation = await StartDirectAndReadAsync(setup.SenderClient, setup.Recipient.UserId, ConversationScope.Organization);
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "سازمان اول.pdf", WorkspaceType.Organization);
        using (var pending = await setup.SenderClient.PostAsJsonAsync($"/api/v1/conversations/{firstConversation.ConversationId}/messages", MediaCommand(file), JsonOptions))
            await AssertMediaPendingAsync(pending);
        await DecideMediaAsync(setup.ModeratorClient, (await MediaStatusAsync(setup.SenderClient, file.Id)).ReviewReference!.Value, ModerationActionType.MediaApproved);
        await SelectOrganizationAsync(setup.SenderClient, secondManager.MembershipId, OrganizationRole.Manager);
        var secondConversation = await StartDirectAndReadAsync(setup.SenderClient, setup.Recipient.UserId, ConversationScope.Organization);
        using var denied = await setup.SenderClient.PostAsJsonAsync($"/api/v1/conversations/{secondConversation.ConversationId}/messages", MediaCommand(file), JsonOptions);
        Assert.False(denied.IsSuccessStatusCode);
        Assert.Equal(MessagingErrorCodes.MessageAttachmentNotAllowed, await ReadProblemCodeAsync(denied));
        Assert.Empty(await factory.GetMessagesAsync(secondConversation.ConversationId));
    }

    [Fact]
    public async Task MediaReviewEveryAttachmentNeedsApprovalAndPartialApprovalDoesNotPublish()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var first = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "پیوست اول.pdf");
        var second = await UploadMediaReviewFileAsync(setup.SenderClient, "image/png", "پیوست دوم.png");
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.File, AttachmentIds = [first.Id, second.Id] };
        using (var pending = await MediaPostAsync(setup, command)) await AssertMediaPendingAsync(pending);
        await DecideMediaAsync(setup.ModeratorClient, (await MediaStatusAsync(setup.SenderClient, first.Id)).ReviewReference!.Value, ModerationActionType.MediaApproved);
        using (var pending = await MediaPostAsync(setup, command)) await AssertMediaPendingAsync(pending);
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await DecideMediaAsync(setup.ModeratorClient, (await MediaStatusAsync(setup.SenderClient, second.Id)).ReviewReference!.Value, ModerationActionType.MediaApproved);
        using var sent = await MediaPostAsync(setup, command); sent.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(2, await db.MessageAttachments.CountAsync(a => a.FileAssetId == first.Id || a.FileAssetId == second.Id));
    }

    [Fact]
    public async Task MediaReviewApprovalRacingAnotherConversationSendIsDeterministicAndDoesNotDeadlock()
    {
        using var setup = await CreateMediaReviewSetupAsync(); using var otherClient = factory.CreateClient();
        var other = await CreateMessagingUserAsync(otherClient);
        var otherConversation = await StartDirectAndReadAsync(setup.SenderClient, other.UserId, ConversationScope.Personal);
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "رقابت تأیید و ارسال.pdf");
        using (var pending = await MediaPostAsync(setup, MediaCommand(file))) await AssertMediaPendingAsync(pending);
        var id = (await MediaStatusAsync(setup.SenderClient, file.Id)).ReviewReference!.Value;
        var detail = await ClaimMediaAsync(setup.ModeratorClient, id); var command = MediaCommand(file);
        var approval = setup.ModeratorClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "تأیید هم‌زمان با درخواست ارسال", ModerationActionType.MediaApproved), JsonOptions);
        var sends = Enumerable.Range(0, 4).Select(async _ =>
        {
            using var response = await setup.SenderClient.PostAsJsonAsync($"/api/v1/conversations/{otherConversation.ConversationId}/messages", command, JsonOptions);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity });
        }).ToArray();
        await Task.WhenAll(sends); using var approved = await approval; approved.EnsureSuccessStatusCode();
        using var final = await setup.SenderClient.PostAsJsonAsync($"/api/v1/conversations/{otherConversation.ConversationId}/messages", command, JsonOptions);
        final.EnsureSuccessStatusCode(); Assert.Single(await factory.GetMessagesAsync(otherConversation.ConversationId));
    }

    [Fact]
    public async Task MediaReviewCannotUseAnotherModuleBindingToCreateAReleasePermission()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "image/png", "مصرف مستقل عکس پروفایل.png");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Users.SingleAsync(u => u.Id == setup.Sender.UserId)).SetProfilePhoto(file.Id, factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
        using var denied = await MediaPostAsync(setup, MediaCommand(file));
        Assert.False(denied.IsSuccessStatusCode);
        Assert.Equal(MessagingErrorCodes.MessageAttachmentNotAllowed, await ReadProblemCodeAsync(denied));
        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<MakanDbContext>().ModerationCases.AnyAsync(c => c.MediaFileAssetId == file.Id));
    }

    [Fact]
    public async Task MediaReviewClaimedCaseDoesNotKeepAuthorityAfterReviewerRoleRevocation()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "لغو مجوز بازبین.pdf");
        using (var pending = await MediaPostAsync(setup, MediaCommand(file))) await AssertMediaPendingAsync(pending);
        var id = (await MediaStatusAsync(setup.SenderClient, file.Id)).ReviewReference!.Value;
        var detail = await ClaimMediaAsync(setup.ModeratorClient, id);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var roles = await db.PlatformRoleAssignments.Where(a => a.UserId == detail.Case.AssignedReviewerUserId && a.Status == PlatformRoleAssignmentStatus.Active).ToArrayAsync();
            foreach (var role in roles) role.End(setup.Sender.UserId, factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
        using var revoked = await setup.ModeratorClient.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "تلاش با نقش لغوشده", ModerationActionType.MediaApproved), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.False((await MediaStatusAsync(setup.SenderClient, file.Id)).AttachmentAllowed);
    }

    [Fact]
    public async Task MediaReviewApprovedFingerprintCannotAuthorizeChangedFileMetadata()
    {
        using var setup = await CreateMediaReviewSetupAsync();
        var file = await UploadMediaReviewFileAsync(setup.SenderClient, "application/pdf", "اثر انگشت محتوای بررسی‌شده.pdf"); var command = MediaCommand(file);
        using (var pending = await MediaPostAsync(setup, command)) await AssertMediaPendingAsync(pending);
        await DecideMediaAsync(setup.ModeratorClient, (await MediaStatusAsync(setup.SenderClient, file.Id)).ReviewReference!.Value, ModerationActionType.MediaApproved);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var asset = await db.FileAssets.SingleAsync(f => f.Id == file.Id);
            // شبیه‌سازی تغییر خارج از قرارداد immutable، فقط در دیتابیس موقت تست.
            db.Entry(asset).Property(f => f.Sha256Hash).CurrentValue = new string('B', 64); await db.SaveChangesAsync();
        }
        using var changed = await MediaPostAsync(setup, command);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageSafetyBlocked, await ReadProblemCodeAsync(changed));
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        Assert.False((await MediaStatusAsync(setup.SenderClient, file.Id)).AttachmentAllowed);
    }

    private async Task<MediaReviewSetup> CreateMediaReviewSetupAsync()
    {
        await factory.ResetPlatformIamAsync();
        var senderBase = factory.CreateClient(); var receiver = factory.CreateClient(); var moderator = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderBase); var recipient = await CreateMessagingUserAsync(receiver);
        var admin = await CreateMessagingUserAsync(moderator); await factory.BootstrapPlatformOwnerAsync(admin.UserId);
        var conversation = await StartDirectAndReadAsync(senderBase, recipient.UserId, ConversationScope.Personal);
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Messaging:ContentSafetyProvider"] = "Internal", ["Commerce:EnforcementEnabled"] = "false", ["AiUsage:Mode"] = "Unmetered" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IContentSafetyProvider>(); services.AddScoped<IContentSafetyProvider, ReviewedMediaContentSafetyProvider>();
                services.RemoveAll<InternalMakanContentSafetyProvider>(); services.AddSingleton<InternalMakanContentSafetyProvider>();
                services.RemoveAll<IMediaSafetyProvider>(); services.AddScoped<IMediaSafetyProvider, ManualMediaSafetyProvider>();
                services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
                services.RemoveAll<IHostedService>();
            });
        });
        var realSender = host.CreateClient(); UseBearerToken(realSender, sender.AccessToken);
        return new(senderBase, realSender, receiver, moderator, sender, recipient, conversation.ConversationId, host);
    }

    private static SendMessageCommand MediaCommand(FileAssetResult file, string? text = null, Guid? operation = null) =>
        new() { ClientMessageId = operation ?? Guid.NewGuid(), Kind = file.ContentType.StartsWith("image/", StringComparison.Ordinal) ? MessageKind.Image : MessageKind.File,
            Text = text, AttachmentIds = [file.Id] };
    private static async Task<FileAssetResult> UploadMediaReviewFileAsync(HttpClient client, string mime, string name, WorkspaceType scope = WorkspaceType.Personal)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(StabilizationMediaFixtures.Bytes(mime));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        form.Add(content, "file", name);
        using var response = await client.PostAsync($"/api/v1/files?workspaceScope={scope}", form);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
    }
    private static Task<HttpResponseMessage> MediaPostAsync(MediaReviewSetup setup, SendMessageCommand command) =>
        setup.SenderClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", command, JsonOptions);
    private static async Task AssertMediaPendingAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PendingReview", body.RootElement.GetProperty("safetyState").GetString());
        Assert.Equal("WAIT_FOR_MEDIA_REVIEW", body.RootElement.GetProperty("requiredAction").GetString());
        Assert.True(body.RootElement.GetProperty("mediaReviewAvailable").GetBoolean());
        Assert.False(body.RootElement.GetProperty("newMessageCreated").GetBoolean());
    }
    private static async Task<MediaReviewStatusResult> MediaStatusAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<MediaReviewStatusResult>($"/api/v1/files/{id}/messaging-review", JsonOptions))!;
    private static async Task<PlatformModerationCaseDetail> ClaimMediaAsync(HttpClient client, Guid id)
    {
        var detail = (await client.GetFromJsonAsync<PlatformModerationCaseDetail>($"/api/admin/v1/moderation/cases/{id}", JsonOptions))!;
        using var claim = await client.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/start-review",
            new PlatformModerationVersionCommand(detail.Case.Version, "بررسی واقعی فایل در تست ایزوله"), JsonOptions);
        claim.EnsureSuccessStatusCode(); return (await claim.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
    }
    private static async Task<PlatformModerationCaseDetail> DecideMediaAsync(HttpClient client, Guid id, ModerationActionType action)
    {
        var detail = await ClaimMediaAsync(client, id);
        using var evidence = await client.GetAsync($"/api/admin/v1/moderation/cases/{id}/evidence/files/{detail.MediaReview!.FileAssetId}");
        evidence.EnsureSuccessStatusCode(); Assert.NotEmpty(await evidence.Content.ReadAsByteArrayAsync());
        using var decision = await client.PostAsJsonAsync($"/api/admin/v1/moderation/cases/{id}/actions",
            new PlatformModerationActionCommand(detail.Case.Version, "تصمیم صریح بازبین مجاز پس از مشاهده شاهد", action), JsonOptions);
        decision.EnsureSuccessStatusCode(); return (await decision.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
    }
    private sealed record MediaReviewSetup(HttpClient SenderBase, HttpClient SenderClient, HttpClient RecipientClient,
        HttpClient ModeratorClient, MessagingUser Sender, MessagingUser Recipient, Guid ConversationId, WebApplicationFactory<Program> Host) : IDisposable
    {
        public void Dispose() { SenderClient.Dispose(); Host.Dispose(); SenderBase.Dispose(); RecipientClient.Dispose(); ModeratorClient.Dispose(); }
    }
}
