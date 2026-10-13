using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PlatformEducationSafetyEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 7_500_000;

    [Fact]
    public async Task EducationReadModelsAreGlobalOrganizationScopedAndDoNotExposePhone()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var teacher = await CreateAuthenticatedUserAsync(teacherClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var organizationA = await factory.CreateOrganizationAsync($"Education A {Guid.NewGuid():N}");
        var organizationB = await factory.CreateOrganizationAsync($"Education B {Guid.NewGuid():N}");
        var classA = await factory.CreateAcademicClassAsync(organizationA);
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        var membershipA = await factory.CreateMembershipAsync(teacher.User.Id, organizationA, OrganizationRole.Teacher);
        var membershipB = await factory.CreateMembershipAsync(teacher.User.Id, organizationB, OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(organizationA, classA.ClassId, membershipA.MembershipId);
        await factory.CreateTeacherAssignmentAsync(organizationB, classB.ClassId, membershipB.MembershipId);
        var learnerA = Assert.Single(await factory.CreateOrganizationPersonsAsync(organizationA, 1));
        var learnerB = Assert.Single(await factory.CreateOrganizationPersonsAsync(organizationB, 1));
        await factory.CreateEnrollmentAsync(organizationA, classA.ClassId, learnerA);

        var classes = await ownerClient.GetFromJsonAsync<PlatformClassPage>(
            "/api/admin/v1/education/classes?page=1&pageSize=100",
            JsonOptions);
        var teachers = await ownerClient.GetFromJsonAsync<PlatformTeacherAssignmentPage>(
            $"/api/admin/v1/education/teachers?search={Uri.EscapeDataString("Education")}&page=1&pageSize=100",
            JsonOptions);
        var enrollmentsJson = await ownerClient.GetStringAsync(
            $"/api/admin/v1/education/enrollments?organizationId={organizationA:D}&page=1&pageSize=100");
        var detail = await ownerClient.GetFromJsonAsync<PlatformClassDetail>(
            $"/api/admin/v1/education/classes/{classA.ClassId:D}",
            JsonOptions);

        Assert.NotNull(classes);
        Assert.Contains(classes.Items, item =>
            item.ClassId == classA.ClassId && item.Organization.Id == organizationA);
        Assert.Contains(classes.Items, item =>
            item.ClassId == classB.ClassId && item.Organization.Id == organizationB);
        Assert.NotNull(teachers);
        Assert.Contains(teachers.Items, item =>
            item.UserId == teacher.User.Id && item.Organization.Id == organizationA);
        Assert.Contains(teachers.Items, item =>
            item.UserId == teacher.User.Id && item.Organization.Id == organizationB);
        Assert.NotNull(detail);
        Assert.Equal(organizationA, detail.Class.Organization.Id);
        Assert.Equal(1, detail.Class.ActiveTeacherCount);
        Assert.Equal(1, detail.Class.ActiveLearnerCount);
        Assert.DoesNotContain(teacher.User.PhoneNumber, enrollmentsJson, StringComparison.Ordinal);
        Assert.True(await factory.CrossOrganizationTeacherAssignmentIsRejectedAsync(
            organizationA,
            classA.ClassId,
            membershipB.MembershipId));
        Assert.True(await factory.CrossOrganizationEnrollmentIsRejectedAsync(
            organizationA,
            classA.ClassId,
            learnerB));
    }

    [Fact]
    public async Task RoleMatrixAllowsSafeReadsButDeniesPlatformSafetyToManagerAndSupport()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var supportClient = factory.CreateClient();
        using var reviewerClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var support = await CreateAuthenticatedUserAsync(supportClient);
        var reviewer = await CreateAuthenticatedUserAsync(reviewerClient);
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        _ = await GrantRoleAsync(ownerClient, support.User.Id, PlatformRoleKeys.Support);
        var reviewerGrant = await GrantRoleAsync(ownerClient, reviewer.User.Id, PlatformRoleKeys.AbuseReviewer);
        var organizationId = await factory.CreateOrganizationAsync($"Role matrix {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(supportClient, support.AccessToken);
        UseBearerToken(reviewerClient, reviewer.AccessToken);
        UseBearerToken(managerClient, manager.AccessToken);

        using var supportEducation = await supportClient.GetAsync("/api/admin/v1/education/classes");
        using var supportMessaging = await supportClient.GetAsync("/api/admin/v1/messaging/conversations");
        using var supportModeration = await supportClient.GetAsync("/api/admin/v1/moderation/cases");
        using var reviewerModeration = await reviewerClient.GetAsync("/api/admin/v1/moderation/cases");
        using var reviewerUsers = await reviewerClient.GetAsync("/api/admin/v1/users");
        using var managerEducation = await managerClient.GetAsync("/api/admin/v1/education/classes");
        using var managerModeration = await managerClient.GetAsync("/api/admin/v1/moderation/cases");

        Assert.Equal(HttpStatusCode.OK, supportEducation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, supportMessaging.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, supportModeration.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewerModeration.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviewerUsers.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerEducation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerModeration.StatusCode);

        using var revoke = await SendDeleteAsync(
            ownerClient,
            $"/api/admin/v1/role-assignments/{reviewerGrant.Assignment.AssignmentId:D}",
            new EndPlatformRoleAssignmentCommand("پایان شیفت بررسی سوءاستفاده"));
        revoke.EnsureSuccessStatusCode();
        using var revokedImmediately = await reviewerClient.GetAsync("/api/admin/v1/moderation/cases");
        Assert.Equal(HttpStatusCode.Forbidden, revokedImmediately.StatusCode);
    }

    [Fact]
    public async Task MessagingMetadataNeverExposesMessageHistoryAndSecurityAuditCanReadIt()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var securityClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var security = await CreateAuthenticatedUserAsync(securityClient);
        var sender = await CreateAuthenticatedUserAsync(senderClient);
        var recipient = await CreateAuthenticatedUserAsync(recipientClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        _ = await GrantRoleAsync(ownerClient, security.User.Id, PlatformRoleKeys.SecurityAudit);
        UseBearerToken(securityClient, security.AccessToken);
        var organizationId = await factory.CreateOrganizationAsync($"Metadata {Guid.NewGuid():N}");
        var conversationId = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            sender.User.Id,
            ConversationType.Group);
        await factory.AddConversationParticipantAsync(conversationId, recipient.User.Id);
        const string privateText = "private-history-must-not-leak";
        await factory.CreateCommittedMessageAsync(conversationId, sender.User.Id, privateText);

        var metadataJson = await securityClient.GetStringAsync(
            $"/api/admin/v1/messaging/conversations?organizationId={organizationId:D}");
        var metadata = JsonSerializer.Deserialize<PlatformConversationMetadataPage>(metadataJson, JsonOptions);
        using var arbitraryHistory = await securityClient.GetAsync(
            $"/api/v1/conversations/{conversationId:D}/messages");

        Assert.NotNull(metadata);
        var item = Assert.Single(metadata.Items, value => value.ConversationId == conversationId);
        Assert.Equal(2, item.ActiveMemberCount);
        Assert.DoesNotContain(privateText, metadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sender.User.Id.ToString("D"), metadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(HttpStatusCode.OK, arbitraryHistory.StatusCode);
    }

    [Fact]
    public async Task PlatformModerationIsCaseScopedAuditedAndEnforcementRequiresSeparatePermission()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var reviewerClient = factory.CreateClient();
        using var senderClient = factory.CreateClient();
        using var reporterClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var reviewer = await CreateAuthenticatedUserAsync(reviewerClient);
        var sender = await CreateAuthenticatedUserAsync(senderClient);
        var reporter = await CreateAuthenticatedUserAsync(reporterClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        _ = await GrantRoleAsync(ownerClient, reviewer.User.Id, PlatformRoleKeys.AbuseReviewer);
        UseBearerToken(reviewerClient, reviewer.AccessToken);
        UseBearerToken(senderClient, sender.AccessToken);
        var organizationId = await factory.CreateOrganizationAsync($"Safety {Guid.NewGuid():N}");
        var conversationA = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            sender.User.Id,
            ConversationType.Group);
        var conversationB = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            sender.User.Id,
            ConversationType.Group);
        await factory.AddConversationParticipantAsync(conversationA, reporter.User.Id);
        await factory.AddConversationParticipantAsync(conversationB, reporter.User.Id);
        var messageA = await factory.CreateCommittedMessageAsync(conversationA, sender.User.Id, "case-a-evidence");
        var messageB = await factory.CreateCommittedMessageAsync(conversationB, sender.User.Id, "case-b-evidence");
        var caseA = await factory.CreatePlatformSafetyCaseAsync(
            conversationA,
            messageA,
            reporter.User.Id,
            sender.User.Id);
        var caseB = await factory.CreatePlatformSafetyCaseAsync(
            conversationB,
            messageB,
            reporter.User.Id,
            sender.User.Id);
        var fileB = await UploadFileAsync(senderClient, "text/plain", "case-b.txt");
        await factory.AttachPlatformSafetyEvidenceAsync(caseB, fileB.Id);

        var reviewerDetail = await reviewerClient.GetFromJsonAsync<PlatformModerationCaseDetail>(
            $"/api/admin/v1/moderation/cases/{caseA:D}",
            JsonOptions);
        Assert.NotNull(reviewerDetail);
        Assert.Equal("case-a-evidence", reviewerDetail.AbuseReport!.ContentSnapshot);
        Assert.DoesNotContain("case-b-evidence", JsonSerializer.Serialize(reviewerDetail, JsonOptions));

        using var reviewerStartResponse = await reviewerClient.PostAsJsonAsync(
            $"/api/admin/v1/moderation/cases/{caseA:D}/start-review",
            new PlatformModerationVersionCommand(reviewerDetail.Case.Version, "بررسی گزارش کاربر"),
            JsonOptions);
        reviewerStartResponse.EnsureSuccessStatusCode();
        var reviewerStarted = (await reviewerStartResponse.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var reviewerEnforce = await reviewerClient.PostAsJsonAsync(
            $"/api/admin/v1/moderation/cases/{caseA:D}/enforce-content-removal",
            new PlatformModerationVersionCommand(reviewerStarted.Case.Version, "حذف محتوای تخلف"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, reviewerEnforce.StatusCode);

        var ownerDetail = await ownerClient.GetFromJsonAsync<PlatformModerationCaseDetail>(
            $"/api/admin/v1/moderation/cases/{caseB:D}",
            JsonOptions);
        using var wrongCaseEvidence = await ownerClient.GetAsync(
            $"/api/admin/v1/moderation/cases/{caseA:D}/evidence/files/{fileB.Id:D}");
        using var validCaseEvidence = await ownerClient.GetAsync(
            $"/api/admin/v1/moderation/cases/{caseB:D}/evidence/files/{fileB.Id:D}");
        Assert.Equal(HttpStatusCode.NotFound, wrongCaseEvidence.StatusCode);
        Assert.Equal(HttpStatusCode.OK, validCaseEvidence.StatusCode);

        using var ownerStartResponse = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/moderation/cases/{caseB:D}/start-review",
            new PlatformModerationVersionCommand(ownerDetail!.Case.Version, "شروع بررسی مالک پلتفرم"),
            JsonOptions);
        ownerStartResponse.EnsureSuccessStatusCode();
        var ownerStarted = (await ownerStartResponse.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;
        using var enforceResponse = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/moderation/cases/{caseB:D}/enforce-content-removal",
            new PlatformModerationVersionCommand(ownerStarted.Case.Version, "حذف محتوای گزارش‌شده"),
            JsonOptions);
        enforceResponse.EnsureSuccessStatusCode();
        var enforced = (await enforceResponse.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(JsonOptions))!;

        Assert.Equal(ModerationCaseStatus.Resolved, enforced.Case.Status);
        Assert.Contains(enforced.Actions, action => action.Type == ModerationActionType.ContentRemoved);
        Assert.True(await factory.IsMessageDeletedAsync(messageB));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.moderation.content.remove"));
        Assert.Equal(2, await factory.CountAdminAuditLogsAsync("platform.moderation.review.start"));
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98917{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber));
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)));
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
    }

    private static async Task<PlatformRoleMutationResult> GrantRoleAsync(
        HttpClient ownerClient,
        Guid userId,
        string roleKey)
    {
        using var response = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(userId, roleKey, "دسترسی عملیاتی مرحله ۱۵C"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlatformRoleMutationResult>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SendDeleteAsync(
        HttpClient client,
        string route,
        EndPlatformRoleAssignmentCommand command)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, route)
        {
            Content = JsonContent.Create(command, options: JsonOptions)
        };
        return client.SendAsync(request);
    }

    private static async Task<FileAssetResult> UploadFileAsync(
        HttpClient client,
        string contentType,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(
            contentType == "text/plain" ? "case evidence"u8.ToArray() : "%PDF-1.7\n"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "File", fileName);
        using var response = await client.PostAsync("/api/v1/files", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
