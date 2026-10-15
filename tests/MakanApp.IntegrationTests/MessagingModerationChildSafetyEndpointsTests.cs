using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task ConversationOwnerReviewsMinimalReportEvidenceAndReporterStatusFollowsLifecycle()
    {
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        using var ownerClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var reporterClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var sender = await CreateMessagingUserAsync(senderClient);
        var reporter = await CreateMessagingUserAsync(reporterClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, sender.UserId);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, reporter.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("صف تعدیل 14D", sender.UserId, reporter.UserId));
        await SendMessageAndReadAsync(
            senderClient,
            group.ConversationId,
            Guid.NewGuid(),
            "neighbor-not-evidence-14d");
        var selected = await SendMessageAndReadAsync(
            senderClient,
            group.ConversationId,
            Guid.NewGuid(),
            "selected-evidence-14d");
        var clientReportId = Guid.NewGuid();
        var reportCommand = new ReportMessageCommand(
            clientReportId,
            AbuseReportReason.Harassment,
            "بررسی شود");
        using var reportResponse = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}/reports",
            reportCommand,
            JsonOptions);
        var report = await reportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        using var retryReport = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}/reports",
            reportCommand,
            JsonOptions);
        using var changedReport = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}/reports",
            new ReportMessageCommand(clientReportId, AbuseReportReason.Threat, "بررسی شود"),
            JsonOptions);
        using var editSource = await senderClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}",
            new EditMessageCommand("edited-after-report-14d", selected.Version),
            JsonOptions);
        editSource.EnsureSuccessStatusCode();
        Assert.Equal(
            "edited-after-report-14d",
            (await factory.GetAdvancedMessageAsync(selected.MessageId)).Text);

        var page = await ownerClient.GetFromJsonAsync<ModerationCasePageResult>(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases?status=Pending&priority=Normal",
            JsonOptions);
        using var invalidPage = await ownerClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases?pageSize=101");
        var summary = Assert.Single(page!.Items);
        var createdCase = await factory.GetModerationCaseForReportAsync(report!.ReportId);
        Assert.Equal(report.ReportId, createdCase.AbuseReportId);
        Assert.Equal(summary.CaseId, createdCase.Id);
        Assert.Equal(1, await factory.CountModerationActionsAsync(summary.CaseId));
        Assert.Equal(HttpStatusCode.Created, retryReport.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changedReport.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.ReportIdempotencyConflict,
            await ReadProblemCodeAsync(changedReport));
        Assert.Equal(1, await factory.CountAbuseReportsAsync(reporter.UserId, clientReportId));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessagePageInvalid, await ReadProblemCodeAsync(invalidPage));
        var otherGroup = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("Cross conversation 14D"));
        using var crossConversation = await ownerClient.GetAsync(
            $"/api/v1/conversations/{otherGroup.ConversationId}/moderation/cases/{summary.CaseId}");
        Assert.Equal(HttpStatusCode.NotFound, crossConversation.StatusCode);
        using var detailResponse = await ownerClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{summary.CaseId}");
        var detailJson = await detailResponse.Content.ReadAsStringAsync();
        var detail = System.Text.Json.JsonSerializer.Deserialize<ModerationCaseDetailResult>(
            detailJson,
            JsonOptions)!;

        Assert.Equal(HttpStatusCode.Created, reportResponse.StatusCode);
        Assert.Equal(ModerationQueueScope.ConversationModerators,
            (await factory.GetModerationCaseForReportAsync(report!.ReportId)).QueueScope);
        Assert.Equal("selected-evidence-14d", detail.ReportedContentSnapshot);
        Assert.DoesNotContain("neighbor-not-evidence-14d", detailJson, StringComparison.Ordinal);
        Assert.DoesNotContain("internalNote", detailJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", detailJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", detailJson, StringComparison.OrdinalIgnoreCase);

        using var start = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{summary.CaseId}/start-review",
            new StartModerationReviewCommand(detail.Version),
            JsonOptions);
        var started = await start.Content.ReadFromJsonAsync<ModerationCaseDetailResult>(JsonOptions);
        using var stale = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{summary.CaseId}/start-review",
            new StartModerationReviewCommand(detail.Version),
            JsonOptions);
        var underReview = await reporterClient.GetFromJsonAsync<AbuseReportReceiptResult>(
            $"/api/v1/messaging/reports/{report.ReportId}",
            JsonOptions);
        using var resolve = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{summary.CaseId}/resolve",
            new CompleteModerationCaseCommand(started!.Version, "private-review-note-14d"),
            JsonOptions);
        var resolvedJson = await resolve.Content.ReadAsStringAsync();
        var resolved = System.Text.Json.JsonSerializer.Deserialize<ModerationCaseDetailResult>(
            resolvedJson,
            JsonOptions)!;
        var reporterReceipt = await reporterClient.GetFromJsonAsync<AbuseReportReceiptResult>(
            $"/api/v1/messaging/reports/{report.ReportId}",
            JsonOptions);

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(MessagingErrorCodes.ModerationStateConflict, await ReadProblemCodeAsync(stale));
        Assert.Equal(AbuseReportStatus.UnderReview, underReview!.Status);
        Assert.Equal(ModerationCaseStatus.Resolved, resolved.Status);
        Assert.Equal(AbuseReportStatus.Resolved, reporterReceipt!.Status);
        Assert.DoesNotContain("private-review-note-14d", resolvedJson, StringComparison.Ordinal);
        Assert.Collection(
            resolved.Actions,
            action =>
            {
                Assert.Equal(ModerationActionType.Created, action.Type);
                Assert.Equal(reporter.UserId, action.ActorUserId);
            },
            action =>
            {
                Assert.Equal(ModerationActionType.ReviewStarted, action.Type);
                Assert.Equal(owner.UserId, action.ActorUserId);
            },
            action =>
            {
                Assert.Equal(ModerationActionType.Resolved, action.Type);
                Assert.Equal(owner.UserId, action.ActorUserId);
            });
        Assert.Equal(3, await factory.CountModerationActionsAsync(summary.CaseId));
    }

    [Fact]
    public async Task UserManagedChannelAndOrganizationGroupReportsRouteToConversationModerators()
    {
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        using var channelOwnerClient = factory.CreateClient();
        using var channelReporterClient = factory.CreateClient();
        var channelOwner = await CreateMessagingUserAsync(channelOwnerClient);
        var channelReporter = await CreateMessagingUserAsync(channelReporterClient);
        await factory.SetCommunicationAgeCategoryAsync(
            channelOwner.UserId,
            CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(
            channelReporter.UserId,
            CommunicationAgeCategory.Adult);
        await factory.AllowPersonalGroupInviteAsync(channelOwner.UserId, channelReporter.UserId);
        var channel = await CreateManagedAndReadAsync(
            channelOwnerClient,
            "channels",
            PersonalCommand("Channel moderation 14D", channelReporter.UserId));
        var channelMessage = await SendMessageAndReadAsync(
            channelOwnerClient,
            channel.ConversationId,
            Guid.NewGuid(),
            "channel evidence 14d");
        using var channelReportResponse = await channelReporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{channel.ConversationId}/messages/{channelMessage.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Spam, null),
            JsonOptions);
        var channelReport = await channelReportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(
            JsonOptions);
        Assert.Equal(
            ModerationQueueScope.ConversationModerators,
            (await factory.GetModerationCaseForReportAsync(channelReport!.ReportId)).QueueScope);

        using var managerClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var reporterClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient);
        var sender = await CreateMessagingUserAsync(senderClient);
        var reporter = await CreateMessagingUserAsync(reporterClient);
        foreach (var userId in new[] { manager.UserId, sender.UserId, reporter.UserId })
        {
            await factory.SetCommunicationAgeCategoryAsync(userId, CommunicationAgeCategory.Adult);
        }

        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerMembership = await factory.CreateMembershipAsync(
            manager.UserId,
            organizationId,
            OrganizationRole.Manager);
        var senderMembership = await factory.CreateMembershipAsync(
            sender.UserId,
            organizationId,
            OrganizationRole.Teacher);
        var reporterMembership = await factory.CreateMembershipAsync(
            reporter.UserId,
            organizationId,
            OrganizationRole.Teacher);
        await SelectOrganizationAsync(managerClient, managerMembership.MembershipId, OrganizationRole.Manager);
        await SelectOrganizationAsync(senderClient, senderMembership.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(reporterClient, reporterMembership.MembershipId, OrganizationRole.Teacher);
        var organizationGroup = await CreateManagedAndReadAsync(
            managerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Organization,
                "Organization moderation 14D",
                null,
                [sender.UserId, reporter.UserId]));
        var organizationMessage = await SendMessageAndReadAsync(
            senderClient,
            organizationGroup.ConversationId,
            Guid.NewGuid(),
            "organization evidence 14d");
        using var organizationReportResponse = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{organizationGroup.ConversationId}/messages/{organizationMessage.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Harassment, null),
            JsonOptions);
        var organizationReport = await organizationReportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(
            JsonOptions);
        var organizationCase = await factory.GetModerationCaseForReportAsync(organizationReport!.ReportId);
        Assert.Equal(ModerationQueueScope.ConversationModerators, organizationCase.QueueScope);
        using var authorized = await managerClient.GetAsync(
            $"/api/v1/conversations/{organizationGroup.ConversationId}/moderation/cases/{organizationCase.Id}");
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherMembership = await factory.CreateMembershipAsync(
            manager.UserId,
            otherOrganizationId,
            OrganizationRole.Manager);
        await SelectOrganizationAsync(managerClient, otherMembership.MembershipId, OrganizationRole.Manager);
        using var crossOrganization = await managerClient.GetAsync(
            $"/api/v1/conversations/{organizationGroup.ConversationId}/moderation/cases/{organizationCase.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossOrganization.StatusCode);
    }

    [Fact]
    public async Task ConcurrentClaimAndResolveAllowExactlyOneStateTransition()
    {
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        using var ownerClient = factory.CreateClient();
        using var competingOwnerClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var reporterClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        UseBearerToken(competingOwnerClient, owner.AccessToken);
        var sender = await CreateMessagingUserAsync(senderClient);
        var reporter = await CreateMessagingUserAsync(reporterClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, sender.UserId);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, reporter.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("هم‌زمانی تعدیل 14D", sender.UserId, reporter.UserId));
        var message = await SendMessageAndReadAsync(
            senderClient,
            group.ConversationId,
            Guid.NewGuid(),
            "concurrent moderation 14d");
        using var reportResponse = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{message.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Spam, null),
            JsonOptions);
        var report = await reportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        var moderationCase = await factory.GetModerationCaseForReportAsync(report!.ReportId);
        var route = $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{moderationCase.Id}";
        var detail = await ownerClient.GetFromJsonAsync<ModerationCaseDetailResult>(route, JsonOptions);

        var claimResponses = await Task.WhenAll(
            ownerClient.PostAsJsonAsync(
                $"{route}/start-review",
                new StartModerationReviewCommand(detail!.Version),
                JsonOptions),
            competingOwnerClient.PostAsJsonAsync(
                $"{route}/start-review",
                new StartModerationReviewCommand(detail.Version),
                JsonOptions));
        try
        {
            Assert.Single(claimResponses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(claimResponses, response => response.StatusCode == HttpStatusCode.PreconditionFailed);
        }
        finally
        {
            foreach (var response in claimResponses)
            {
                response.Dispose();
            }
        }

        var claimed = await ownerClient.GetFromJsonAsync<ModerationCaseDetailResult>(route, JsonOptions);
        var resolveResponses = await Task.WhenAll(
            ownerClient.PostAsJsonAsync(
                $"{route}/resolve",
                new CompleteModerationCaseCommand(claimed!.Version, null),
                JsonOptions),
            competingOwnerClient.PostAsJsonAsync(
                $"{route}/resolve",
                new CompleteModerationCaseCommand(claimed.Version, null),
                JsonOptions));
        try
        {
            Assert.Single(resolveResponses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(resolveResponses, response => response.StatusCode == HttpStatusCode.PreconditionFailed);
        }
        finally
        {
            foreach (var response in resolveResponses)
            {
                response.Dispose();
            }
        }

        Assert.Equal(3, await factory.CountModerationActionsAsync(moderationCase.Id));
    }

    [Fact]
    public async Task ReviewReportsPermissionIsIndependentRevocableAndCannotExposeReportedReviewerCase()
    {
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var reporterClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var admin = await CreateMessagingUserAsync(adminClient);
        var sender = await CreateMessagingUserAsync(senderClient);
        var reporter = await CreateMessagingUserAsync(reporterClient);
        foreach (var userId in new[] { admin.UserId, sender.UserId, reporter.UserId })
        {
            await factory.AllowPersonalGroupInviteAsync(owner.UserId, userId);
        }

        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("مجوز تعدیل 14D", admin.UserId, sender.UserId, reporter.UserId));
        using var promote = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/role",
            new ChangeConversationMemberRoleCommand(
                ConversationParticipantRole.Admin,
                ConversationAdminPermission.DeleteMessages),
            JsonOptions);
        var promoted = await promote.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
        var version = promoted!.Participants.Single(item => item.User.UserId == admin.UserId).Version;
        var selected = await SendMessageAndReadAsync(
            senderClient,
            group.ConversationId,
            Guid.NewGuid(),
            "reported-by-member-14d");
        using var reportResponse = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Spam, null),
            JsonOptions);
        reportResponse.EnsureSuccessStatusCode();

        using var deleteOnly = await adminClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases");
        using var member = await reporterClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases");
        using var ownerList = await ownerClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases");
        using var grant = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/permissions",
            new UpdateConversationPermissionsCommand(
                ConversationAdminPermission.DeleteMessages |
                ConversationAdminPermission.ReviewReports,
                version),
            JsonOptions);
        var granted = await grant.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
        var grantedVersion = granted!.Participants.Single(item => item.User.UserId == admin.UserId).Version;
        using var authorized = await adminClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases");
        using var revoke = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/permissions",
            new UpdateConversationPermissionsCommand(
                ConversationAdminPermission.DeleteMessages,
                grantedVersion),
            JsonOptions);
        revoke.EnsureSuccessStatusCode();
        using var revoked = await adminClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases");

        using var managerClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerMembership = await factory.CreateMembershipAsync(
            manager.UserId,
            organizationId,
            OrganizationRole.Manager);
        await SelectOrganizationAsync(
            managerClient,
            managerMembership.MembershipId,
            OrganizationRole.Manager);
        using var managerOnly = await managerClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases");

        Assert.Equal(HttpStatusCode.Forbidden, deleteOnly.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(MessagingErrorCodes.ModerationPermissionRequired, await ReadProblemCodeAsync(revoked));
        Assert.Equal(HttpStatusCode.NotFound, managerOnly.StatusCode);

        var reportAboutOwnerMessage = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "owner-is-reported-14d");
        using var reportOwner = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{reportAboutOwnerMessage.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Other, null),
            JsonOptions);
        var reportOwnerReceipt = await reportOwner.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        var ownerCase = await factory.GetModerationCaseForReportAsync(reportOwnerReceipt!.ReportId);
        using var reportedOwnerRead = await ownerClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{ownerCase.Id}");
        Assert.Equal(HttpStatusCode.NotFound, reportedOwnerRead.StatusCode);
    }

    [Fact]
    public async Task ReportsRouteByConversationPolicyAndChildSafetyAlwaysEscalates()
    {
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        await factory.SetCommunicationAgeCategoryAsync(first.UserId, CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(second.UserId, CommunicationAgeCategory.Adult);
        var direct = await StartDirectAndReadAsync(firstClient, second.UserId, ConversationScope.Personal);
        var directMessage = await SendMessageAndReadAsync(
            firstClient,
            direct.ConversationId,
            Guid.NewGuid(),
            "direct report 14d");
        using var directReportResponse = await secondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{direct.ConversationId}/messages/{directMessage.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Threat, null),
            JsonOptions);
        var directReport = await directReportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        var directCase = await factory.GetModerationCaseForReportAsync(directReport!.ReportId);
        Assert.Equal(ModerationQueueScope.PlatformSafety, directCase.QueueScope);
        Assert.Equal(ModerationPriority.Normal, directCase.Priority);

        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.SetCommunicationAgeCategoryAsync(member.UserId, CommunicationAgeCategory.Unknown);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("کودک 14D"));
        await InviteAndAcceptPersonalAsync(ownerClient, memberClient, group.ConversationId, member.UserId);
        var groupMessage = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "child concern 14d");
        using var childResponse = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{groupMessage.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.ChildSafetyConcern, null),
            JsonOptions);
        var childReport = await childResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        var childCase = await factory.GetModerationCaseForReportAsync(childReport!.ReportId);
        Assert.Equal(ModerationQueueScope.PlatformSafety, childCase.QueueScope);
        Assert.Equal(ModerationPriority.Critical, childCase.Priority);

        using var academicSenderClient = factory.CreateClient();
        using var academicReporterClient = factory.CreateClient();
        var academicSender = await CreateMessagingUserAsync(academicSenderClient);
        var academicReporter = await CreateMessagingUserAsync(academicReporterClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var academicTeacherMembership = await factory.CreateMembershipAsync(academicSender.UserId, organizationId, OrganizationRole.Teacher);
        var reporterMembership = await factory.CreateMembershipAsync(
            academicReporter.UserId,
            organizationId,
            OrganizationRole.Student);
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var academicPerson = await factory.CreateOrganizationPersonAsync(organizationId, await factory.GetUserPersonIdAsync(academicReporter.UserId));
        await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, academicPerson);
        await factory.CreateTeacherAssignmentAsync(organizationId, academic.ClassId, academicTeacherMembership.MembershipId);
        await SelectOrganizationAsync(academicSenderClient, academicTeacherMembership.MembershipId, OrganizationRole.Teacher);
        using var officialResponse = await academicSenderClient.PostAsync($"/api/v1/organization/classes/{academic.ClassId}/conversation", null);
        officialResponse.EnsureSuccessStatusCode();
        var systemConversationId = (await officialResponse.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!.ConversationId;
        var systemMessageId = await factory.CreateCommittedMessageAsync(
            systemConversationId,
            academicSender.UserId,
            "system managed evidence 14d");
        await SelectOrganizationAsync(
            academicReporterClient,
            reporterMembership.MembershipId,
            OrganizationRole.Student);
        using var systemResponse = await academicReporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{systemConversationId}/messages/{systemMessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.InappropriateContent, null),
            JsonOptions);
        var systemReport = await systemResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        Assert.Equal(
            ModerationQueueScope.PlatformSafety,
            (await factory.GetModerationCaseForReportAsync(systemReport!.ReportId)).QueueScope);
    }

    [Theory]
    [InlineData(ContentSafetyDecision.Blocked, MessagingErrorCodes.MessageSafetyBlocked)]
    [InlineData(ContentSafetyDecision.ReviewRequired, MessagingErrorCodes.MessageSafetyReviewRequired)]
    public async Task ChildSensitiveDeniedMessageCreatesOneCriticalIncidentAndRetryIsStable(
        ContentSafetyDecision decision,
        string expectedCode)
    {
        factory.ContentSafetyProvider.Configure(
            decision,
            "high",
            "provider-reference-must-not-leak",
            ["policy-secret"]);
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient);
        var recipient = await CreateMessagingUserAsync(recipientClient);
        await factory.SetCommunicationAgeCategoryAsync(sender.UserId, CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(recipient.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(sender.UserId, recipient.UserId);
        var direct = await StartDirectAndReadAsync(senderClient, recipient.UserId, ConversationScope.Personal);
        var clientMessageId = Guid.NewGuid();

        using var first = await SendMessageAsync(
            senderClient,
            direct.ConversationId,
            clientMessageId,
            "blocked content 14d");
        var firstJson = await first.Content.ReadAsStringAsync();
        using var retry = await SendMessageAsync(
            senderClient,
            direct.ConversationId,
            clientMessageId,
            "blocked content 14d");
        using var changed = await SendMessageAsync(
            senderClient,
            direct.ConversationId,
            clientMessageId,
            "changed payload 14d");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, first.StatusCode);
        Assert.Equal(expectedCode, await ReadProblemCodeAsync(first));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, retry.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.DoesNotContain("provider-reference", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("policy-secret", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await factory.GetMessagesAsync(direct.ConversationId));
        Assert.Single(factory.ContentSafetyProvider.Requests);
        var incident = await factory.GetSafetyIncidentAsync(
            direct.ConversationId,
            sender.UserId,
            clientMessageId);
        Assert.NotNull(incident);
        Assert.Equal(decision, incident!.Decision);
        var moderationCase = await factory.GetModerationCaseForIncidentAsync(incident.Id);
        Assert.Equal(ModerationQueueScope.PlatformSafety, moderationCase.QueueScope);
        Assert.Equal(ModerationPriority.Critical, moderationCase.Priority);
        Assert.Equal(1, await factory.CountModerationActionsAsync(moderationCase.Id));
    }

    [Fact]
    public async Task AdultTrafficBypassesProviderWhileUnknownAllowAndUnavailableFailClosedBehaveCorrectly()
    {
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient);
        var recipient = await CreateMessagingUserAsync(recipientClient);
        await factory.SetCommunicationAgeCategoryAsync(sender.UserId, CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(recipient.UserId, CommunicationAgeCategory.Adult);
        var direct = await StartDirectAndReadAsync(senderClient, recipient.UserId, ConversationScope.Personal);

        factory.ContentSafetyProvider.ConfigureUnavailable();
        var adult = await SendMessageAndReadAsync(
            senderClient,
            direct.ConversationId,
            Guid.NewGuid(),
            "adult bypass 14d");
        Assert.Equal(1, adult.Sequence);
        Assert.Empty(factory.ContentSafetyProvider.Requests);

        await factory.SetCommunicationAgeCategoryAsync(recipient.UserId, CommunicationAgeCategory.Unknown);
        await factory.AllowPersonalGroupInviteAsync(sender.UserId, recipient.UserId);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        var allowedId = Guid.NewGuid();
        var allowed = await SendMessageAndReadAsync(
            senderClient,
            direct.ConversationId,
            allowedId,
            "unknown allowed 14d");
        Assert.Equal(2, allowed.Sequence);
        Assert.Single(factory.ContentSafetyProvider.Requests);
        Assert.Null(await factory.GetSafetyIncidentAsync(
            direct.ConversationId,
            sender.UserId,
            allowedId));

        factory.ContentSafetyProvider.ConfigureUnavailable();
        var unavailableId = Guid.NewGuid();
        using var unavailable = await SendMessageAsync(
            senderClient,
            direct.ConversationId,
            unavailableId,
            "provider unavailable 14d");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageSafetyUnavailable, await ReadProblemCodeAsync(unavailable));
        Assert.Equal(2, (await factory.GetMessagesAsync(direct.ConversationId)).Length);
        Assert.Null(await factory.GetSafetyIncidentAsync(
            direct.ConversationId,
            sender.UserId,
            unavailableId));
    }

    [Fact]
    public async Task AuthorizationIsRecheckedAfterSafetyEvaluationWhenUnknownParticipantLeaves()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.SetCommunicationAgeCategoryAsync(member.UserId, CommunicationAgeCategory.Unknown);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("TOCTOU 14D"));
        await InviteAndAcceptPersonalAsync(ownerClient, memberClient, group.ConversationId, member.UserId);
        factory.ContentSafetyProvider.Configure(
            ContentSafetyDecision.Allowed,
            beforeReturn: () => factory.RemoveConversationParticipantAsync(
                group.ConversationId,
                member.UserId));

        using var response = await SendMessageAsync(
            memberClient,
            group.ConversationId,
            Guid.NewGuid(),
            "should not commit 14d");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(factory.ContentSafetyProvider.Requests);
        Assert.Empty(await factory.GetMessagesAsync(group.ConversationId));
    }

    [Fact]
    public async Task GroupStopsUsingSafetyProviderAfterLastNonAdultParticipantLeaves()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.SetCommunicationAgeCategoryAsync(owner.UserId, CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(member.UserId, CommunicationAgeCategory.Unknown);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("Age scope 14D"));
        await InviteAndAcceptPersonalAsync(ownerClient, memberClient, group.ConversationId, member.UserId);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        var first = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "evaluated before leave 14d");
        Assert.Equal(1, first.Sequence);
        Assert.Single(factory.ContentSafetyProvider.Requests);

        await factory.RemoveConversationParticipantAsync(group.ConversationId, member.UserId);
        factory.ContentSafetyProvider.ConfigureUnavailable();
        try
        {
            var second = await SendMessageAndReadAsync(
                ownerClient,
                group.ConversationId,
                Guid.NewGuid(),
                "adult only after leave 14d");

            Assert.Equal(2, second.Sequence);
            Assert.Empty(factory.ContentSafetyProvider.Requests);
        }
        finally
        {
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        }
    }

    [Fact]
    public async Task BlockedAttachmentIsRetainedAsProtectedEvidenceWithoutCreatingMessage()
    {
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient);
        var recipient = await CreateMessagingUserAsync(recipientClient);
        await factory.SetCommunicationAgeCategoryAsync(sender.UserId, CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(recipient.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(sender.UserId, recipient.UserId);
        var direct = await StartDirectAndReadAsync(senderClient, recipient.UserId, ConversationScope.Personal);
        var file = await UploadFileAsync(senderClient, "image/png", "blocked-14d.png");
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked);
        var clientMessageId = Guid.NewGuid();

        using var blocked = await senderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = clientMessageId,
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            },
            JsonOptions);
        var incident = await factory.GetSafetyIncidentAsync(
            direct.ConversationId,
            sender.UserId,
            clientMessageId);
        var moderationCase = await factory.GetModerationCaseForIncidentAsync(incident!.Id);
        using var normalFileRead = await senderClient.GetAsync($"/api/v1/files/{file.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(direct.ConversationId));
        Assert.Equal(1, await factory.CountModerationEvidenceAttachmentsAsync(moderationCase.Id));
        Assert.Equal(HttpStatusCode.NotFound, normalFileRead.StatusCode);
        Assert.Single(Assert.Single(factory.ContentSafetyProvider.Requests).Attachments);
    }

    [Fact]
    public async Task ForwardIntoChildSensitiveGroupUsesSafetyGateBeforePublishing()
    {
        using var senderClient = factory.CreateClient();
        using var directRecipientClient = factory.CreateClient();
        using var groupMemberClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient);
        var directRecipient = await CreateMessagingUserAsync(directRecipientClient);
        var groupMember = await CreateMessagingUserAsync(groupMemberClient);
        await factory.SetCommunicationAgeCategoryAsync(sender.UserId, CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(
            directRecipient.UserId,
            CommunicationAgeCategory.Adult);
        await factory.SetCommunicationAgeCategoryAsync(groupMember.UserId, CommunicationAgeCategory.Minor);
        var sourceConversation = await StartDirectAndReadAsync(
            senderClient,
            directRecipient.UserId,
            ConversationScope.Personal);
        var source = await SendMessageAndReadAsync(
            senderClient,
            sourceConversation.ConversationId,
            Guid.NewGuid(),
            "forward source 14d");
        await factory.AllowPersonalGroupInviteAsync(sender.UserId, groupMember.UserId);
        var destination = await CreateManagedAndReadAsync(
            senderClient,
            "groups",
            PersonalCommand("Forward safety 14D"));
        await InviteAndAcceptPersonalAsync(senderClient, groupMemberClient, destination.ConversationId, groupMember.UserId);
        var changedDuringEvaluationId = Guid.NewGuid();
        factory.ContentSafetyProvider.Configure(
            ContentSafetyDecision.Allowed,
            beforeReturn: async () =>
            {
                using var edit = await senderClient.PatchAsJsonAsync(
                    $"/api/v1/conversations/{sourceConversation.ConversationId}/messages/{source.MessageId}",
                    new EditMessageCommand("forward source changed 14d", source.Version),
                    JsonOptions);
                edit.EnsureSuccessStatusCode();
            });
        using var changedDuringEvaluation = await senderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{sourceConversation.ConversationId}/messages/{source.MessageId}/forward",
            new ForwardMessageCommand(destination.ConversationId, changedDuringEvaluationId),
            JsonOptions);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, changedDuringEvaluation.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.MessageSafetyUnavailable,
            await ReadProblemCodeAsync(changedDuringEvaluation));
        Assert.Empty(await factory.GetMessagesAsync(destination.ConversationId));
        Assert.Null(await factory.GetSafetyIncidentAsync(
            destination.ConversationId,
            sender.UserId,
            changedDuringEvaluationId));

        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked);
        var clientMessageId = Guid.NewGuid();

        using var response = await senderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{sourceConversation.ConversationId}/messages/{source.MessageId}/forward",
            new ForwardMessageCommand(destination.ConversationId, clientMessageId),
            JsonOptions);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageSafetyBlocked, await ReadProblemCodeAsync(response));
        Assert.Empty(await factory.GetMessagesAsync(destination.ConversationId));
        Assert.Single(factory.ContentSafetyProvider.Requests);
        var incident = await factory.GetSafetyIncidentAsync(
            destination.ConversationId,
            sender.UserId,
            clientMessageId);
        Assert.NotNull(incident);
        Assert.Equal(ContentSafetyDecision.Blocked, incident!.Decision);
        Assert.Equal(
            ModerationPriority.Critical,
            (await factory.GetModerationCaseForIncidentAsync(incident.Id)).Priority);
    }

    [Fact]
    public async Task ConversationReviewerDownloadsOnlyCaseAttachmentAndRevocationTakesEffectImmediately()
    {
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
        using var ownerClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var reporterClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var sender = await CreateMessagingUserAsync(senderClient);
        var reporter = await CreateMessagingUserAsync(reporterClient);
        _ = await CreateMessagingUserAsync(outsiderClient);
        foreach (var userId in new[] { sender.UserId, reporter.UserId })
        {
            await factory.AllowPersonalGroupInviteAsync(owner.UserId, userId);
        }

        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("شاهد فایل 14D", sender.UserId, reporter.UserId));
        var file = await UploadFileAsync(senderClient, "image/png", "evidence-14d.png");
        using var send = await senderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            },
            JsonOptions);
        var message = await send.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions);
        using var reportResponse = await reporterClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{message!.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.InappropriateContent, null),
            JsonOptions);
        var report = await reportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        var moderationCase = await factory.GetModerationCaseForReportAsync(report!.ReportId);
        var evidenceRoute =
            $"/api/v1/conversations/{group.ConversationId}/moderation/cases/{moderationCase.Id}/evidence/files/{file.Id}";

        using var delete = await senderClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{message.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(message.Version)}");
        delete.EnsureSuccessStatusCode();

        using var ownerDownload = await ownerClient.GetAsync(evidenceRoute);
        using var outsiderDownload = await outsiderClient.GetAsync(evidenceRoute);
        await factory.RemoveConversationParticipantAsync(group.ConversationId, owner.UserId);
        using var revokedDownload = await ownerClient.GetAsync(evidenceRoute);

        Assert.Equal(HttpStatusCode.OK, ownerDownload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDownload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revokedDownload.StatusCode);
    }
}
