using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PlatformOperationsEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 7_300_000;

    [Fact]
    public async Task OrganizationAndUserReadModelsAreGlobalPaginatedAndMaskPhone()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var target = await CreateAuthenticatedUserAsync(targetClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var organizationId = await factory.CreateOrganizationAsync($"Global read {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(target.User.Id, organizationId, OrganizationRole.Student);

        var organizations = await ownerClient.GetFromJsonAsync<PlatformOrganizationPage>(
            $"/api/admin/v1/organizations?search=Global%20read&page=1&pageSize=10",
            JsonOptions);
        var organization = Assert.Single(organizations!.Items, item => item.Id == organizationId);
        Assert.Equal(1, organization.ActiveMemberCount);

        var detail = await ownerClient.GetFromJsonAsync<PlatformOrganizationDetail>(
            $"/api/admin/v1/organizations/{organizationId}",
            JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(organizationId, detail.Id);

        var memberships = await ownerClient.GetFromJsonAsync<PlatformMembershipPage>(
            $"/api/admin/v1/organizations/{organizationId}/memberships",
            JsonOptions);
        Assert.Contains(memberships!.Items, item => item.UserId == target.User.Id);
        using var invitation = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/organizations/{organizationId}/invitations",
            new CreatePlatformInvitationCommand(
                target.User.Id,
                OrganizationRole.Manager,
                factory.Clock.GetUtcNow().UtcDateTime.AddDays(2),
                "دعوت نقش تکمیلی"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.organization.invitation.create"));

        var users = await ownerClient.GetFromJsonAsync<PlatformUserPage>(
            "/api/admin/v1/users?page=1&pageSize=100",
            JsonOptions);
        var user = Assert.Single(users!.Items, item => item.UserId == target.User.Id);
        Assert.NotNull(user.MaskedPhone);
        Assert.Contains('*', user.MaskedPhone!);
        Assert.NotEqual(target.User.PhoneNumber, user.MaskedPhone);

        var userDetail = await ownerClient.GetFromJsonAsync<PlatformUserDetail>(
            $"/api/admin/v1/users/{target.User.Id}",
            JsonOptions);
        Assert.NotNull(userDetail);
        Assert.Equal(target.User.Id, userDetail.UserId);
        Assert.DoesNotContain(target.User.PhoneNumber, await ownerClient.GetStringAsync(
            $"/api/admin/v1/users/{target.User.Id}"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProvisioningRetryCreatesOrganizationSubscriptionAndManagerInvitationExactlyOnce()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(managerClient, manager.AccessToken);
        var planId = await factory.CreateCommercePlanAsync();
        var planVersionId = await factory.CreatePublishedPlanVersionAsync(planId, 1);
        var clientOperationId = $"provision-{Guid.NewGuid():N}";
        var organizationName = $"Provisioned {Guid.NewGuid():N}";
        var command = new ProvisionOrganizationCommand(
            clientOperationId,
            organizationName,
            planVersionId,
            manager.User.Id,
            factory.Clock.GetUtcNow().UtcDateTime.AddDays(365),
            factory.Clock.GetUtcNow().UtcDateTime.AddDays(7),
            "فروش سازمانی تأییدشده");

        using var firstResponse = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/provisioning",
            command,
            JsonOptions);
        var first = await firstResponse.Content.ReadFromJsonAsync<PlatformProvisioningResult>(JsonOptions);
        using var retryResponse = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/provisioning",
            command,
            JsonOptions);
        var retry = await retryResponse.Content.ReadFromJsonAsync<PlatformProvisioningResult>(JsonOptions);

        Assert.True(
            firstResponse.StatusCode == HttpStatusCode.Created,
            $"status={firstResponse.StatusCode};body={await firstResponse.Content.ReadAsStringAsync()};logs={string.Join(" | ", factory.SensitiveLogs.Snapshot().TakeLast(5))}");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(retry);
        Assert.Equal(first.Request.Id, retry.Request.Id);
        Assert.True(retry.Replayed);
        Assert.Equal(PlatformProvisioningStatus.Completed, first.Request.Status);
        Assert.Equal(1, await factory.CountProvisioningRequestsAsync(clientOperationId));
        Assert.Equal(1, await factory.CountOrganizationsByNameAsync(organizationName));
        Assert.Equal(1, await factory.CountSubscriptionsAsync(first.Request.OrganizationId!.Value));
        Assert.Equal(1, await factory.CountPendingManagerInvitationsAsync(
            first.Request.OrganizationId.Value,
            manager.User.Id));

        using var accept = await managerClient.PostAsync(
            $"/api/v1/invitations/{first.Request.ManagerInvitationId}/accept",
            null);
        accept.EnsureSuccessStatusCode();
        var acceptance = await accept.Content.ReadFromJsonAsync<MakanApp.Application.Organization.AcceptInvitationResult>(JsonOptions);
        Assert.Equal(1, await factory.CountMembershipsAsync(
            manager.User.Id,
            first.Request.OrganizationId.Value));
        Assert.Equal(1, await factory.CountRoleAssignmentsAsync(
            acceptance!.MembershipId,
            OrganizationRole.Manager));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.provisioning.complete"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.provisioning.subscription.assign"));
    }

    [Fact]
    public async Task OrganizationSuspensionUsesImpactStepUpAndAuditAndCanReactivate()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var createCommand = new CreatePlatformOrganizationCommand(
            $"create-{Guid.NewGuid():N}",
            $"Lifecycle {Guid.NewGuid():N}",
            "ایجاد دستی سازمان");
        using var createResponse = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/organizations",
            createCommand,
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<PlatformProvisioningResult>(JsonOptions);
        using var createRetry = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/organizations",
            createCommand,
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createRetry.StatusCode);
        var organizationId = created!.Request.OrganizationId!.Value;
        await factory.CreateMembershipAsync(owner.User.Id, organizationId, OrganizationRole.Manager);

        var impact = await ownerClient.GetFromJsonAsync<PlatformOrganizationImpactPreview>(
            $"/api/admin/v1/organizations/{organizationId}/suspension-impact",
            JsonOptions);
        Assert.Equal(1, impact!.ActiveMembershipCount);

        factory.PlatformStepUp.SetSatisfied(false);
        using var denied = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/organizations/{organizationId}/suspend",
            new PlatformOrganizationStateCommand("تعلیق عملیاتی"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        factory.PlatformStepUp.SetSatisfied(true);
        using var suspended = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/organizations/{organizationId}/suspend",
            new PlatformOrganizationStateCommand("تعلیق عملیاتی"),
            JsonOptions);
        suspended.EnsureSuccessStatusCode();
        using var reactivated = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/organizations/{organizationId}/reactivate",
            new PlatformOrganizationStateCommand("رفع علت تعلیق"),
            JsonOptions);
        reactivated.EnsureSuccessStatusCode();

        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.organization.suspend"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.organization.reactivate"));
    }

    [Fact]
    public async Task MembershipRoleChangeTargetsOnlyExplicitOrganization()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var teacher = await CreateAuthenticatedUserAsync(teacherClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var firstOrganizationId = await factory.CreateOrganizationAsync($"First {Guid.NewGuid():N}");
        var secondOrganizationId = await factory.CreateOrganizationAsync($"Second {Guid.NewGuid():N}");
        var first = await factory.CreateMembershipAsync(
            teacher.User.Id,
            firstOrganizationId,
            OrganizationRole.Teacher);
        var second = await factory.CreateMembershipAsync(
            teacher.User.Id,
            secondOrganizationId,
            OrganizationRole.Teacher);

        using var response = await ownerClient.PutAsJsonAsync(
            $"/api/admin/v1/organizations/{firstOrganizationId}/memberships/{first.MembershipId}/role",
            new ChangePlatformMembershipRoleCommand(
                OrganizationRole.Teacher,
                OrganizationRole.Student,
                "اصلاح نقش سازمان اول"),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        Assert.Equal([OrganizationRole.Student], await factory.GetActiveRolesAsync(first.MembershipId));
        Assert.Equal([OrganizationRole.Teacher], await factory.GetActiveRolesAsync(second.MembershipId));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.organization.membership.role.change"));
    }

    [Fact]
    public async Task ConcurrentMembershipEndsCannotRemoveAllManagersAndFinalManagerCannotBeSuspended()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var competingClient = factory.CreateClient();
        using var firstManagerClient = factory.CreateClient();
        using var secondManagerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var firstManager = await CreateAuthenticatedUserAsync(firstManagerClient);
        var secondManager = await CreateAuthenticatedUserAsync(secondManagerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(competingClient, owner.AccessToken);
        var organizationId = await factory.CreateOrganizationAsync($"Managers {Guid.NewGuid():N}");
        var first = await factory.CreateMembershipAsync(firstManager.User.Id, organizationId, OrganizationRole.Manager);
        var second = await factory.CreateMembershipAsync(secondManager.User.Id, organizationId, OrganizationRole.Manager);

        var responses = await Task.WhenAll(
            SendDeleteAsync(ownerClient, organizationId, first.MembershipId, "پایان مدیر اول"),
            SendDeleteAsync(competingClient, organizationId, second.MembershipId, "پایان مدیر دوم"));
        try
        {
            Assert.Single(responses, item => item.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, await factory.CountActiveManagersAsync(organizationId));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var remainingUserId = (await factory.GetActiveRolesAsync(first.MembershipId)).Contains(OrganizationRole.Manager)
            ? firstManager.User.Id
            : secondManager.User.Id;
        var remainingMembershipId = remainingUserId == firstManager.User.Id
            ? first.MembershipId
            : second.MembershipId;
        using var roleRemoval = await ownerClient.PutAsJsonAsync(
            $"/api/admin/v1/organizations/{organizationId}/memberships/{remainingMembershipId}/role",
            new ChangePlatformMembershipRoleCommand(
                OrganizationRole.Manager,
                OrganizationRole.Teacher,
                "آزمون آخرین نقش مدیر"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, roleRemoval.StatusCode);
        Assert.Equal(PlatformErrorCodes.LastManagerRequired, await ReadProblemCodeAsync(roleRemoval));
        using var suspend = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{remainingUserId}/suspend",
            new PlatformUserStateCommand("آزمون آخرین مدیر"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, suspend.StatusCode);
        Assert.Equal(PlatformErrorCodes.LastManagerRequired, await ReadProblemCodeAsync(suspend));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.organization.membership.end"));
    }

    [Fact]
    public async Task UserSuspensionRevokesSessionsRejectsAuthenticationAndProtectsLastOwner()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var target = await CreateAuthenticatedUserAsync(targetClient);
        var targetSessionId = await factory.GetLatestSessionIdAsync(target.User.Id);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(targetClient, target.AccessToken);

        using var suspend = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{target.User.Id}/suspend",
            new PlatformUserStateCommand("حساب مشکوک"),
            JsonOptions);
        suspend.EnsureSuccessStatusCode();
        Assert.Equal(UserAccountStatus.Suspended, await factory.GetUserAccountStatusAsync(target.User.Id));
        Assert.False(await factory.IsSessionActiveAsync(targetSessionId));
        using var rejected = await targetClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        using var suspendedChallengeResponse = await targetClient.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(target.User.PhoneNumber));
        suspendedChallengeResponse.EnsureSuccessStatusCode();
        var suspendedChallenge = (await suspendedChallengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        using var suspendedVerify = await targetClient.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                suspendedChallenge.ChallengeId,
                target.User.PhoneNumber,
                factory.GetLatestOtpCode(target.User.PhoneNumber)));
        Assert.Equal(HttpStatusCode.Forbidden, suspendedVerify.StatusCode);
        Assert.Equal(IdentityErrorCodes.AccountSuspended, await ReadProblemCodeAsync(suspendedVerify));

        using var reactivate = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{target.User.Id}/reactivate",
            new PlatformUserStateCommand("بررسی تکمیل شد"),
            JsonOptions);
        reactivate.EnsureSuccessStatusCode();
        using var stillRevoked = await targetClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, stillRevoked.StatusCode);

        using var lastOwner = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{owner.User.Id}/suspend",
            new PlatformUserStateCommand("نباید مجاز باشد"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, lastOwner.StatusCode);
        Assert.Equal(PlatformErrorCodes.LastOwnerRequired, await ReadProblemCodeAsync(lastOwner));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.user.suspend"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.user.reactivate"));
    }

    [Fact]
    public async Task SupportCanReadSafeDataAndRevokeSessionButCannotManageOrganizations()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var supportClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var support = await CreateAuthenticatedUserAsync(supportClient);
        var target = await CreateAuthenticatedUserAsync(targetClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        using var grant = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(support.User.Id, PlatformRoleKeys.Support, "شیفت پشتیبانی"),
            JsonOptions);
        grant.EnsureSuccessStatusCode();
        UseBearerToken(supportClient, support.AccessToken);

        using var read = await supportClient.GetAsync("/api/admin/v1/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var manage = await supportClient.PostAsJsonAsync(
            "/api/admin/v1/organizations",
            new CreatePlatformOrganizationCommand($"support-{Guid.NewGuid():N}", "Denied", "نباید اجرا شود"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, manage.StatusCode);

        using var revoke = await supportClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{target.User.Id}/sessions/revoke",
            new RevokePlatformSessionsCommand("درخواست امنیتی کاربر"),
            JsonOptions);
        revoke.EnsureSuccessStatusCode();
        using var targetRejected = await targetClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, targetRejected.StatusCode);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.sessions.revoke-all"));
    }

    [Fact]
    public async Task AdministrativeSessionRevocationSupportsOneSessionAndAllSessions()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var target = await CreateAuthenticatedUserAsync(targetClient);
        await CreateSessionForPhoneAsync(targetClient, target.User.PhoneNumber);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var sessionIds = (await factory.GetSessionIdsAsync(target.User.Id)).ToArray();
        Assert.Equal(2, sessionIds.Length);

        using var one = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{target.User.Id}/sessions/{sessionIds[0]}/revoke",
            new RevokePlatformSessionsCommand("ابطال یک دستگاه"),
            JsonOptions);
        one.EnsureSuccessStatusCode();
        Assert.False(await factory.IsSessionActiveAsync(sessionIds[0]));
        Assert.True(await factory.IsSessionActiveAsync(sessionIds[1]));
        Assert.DoesNotContain("accessToken", await one.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var all = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/users/{target.User.Id}/sessions/revoke",
            new RevokePlatformSessionsCommand("ابطال همه دستگاه‌ها"),
            JsonOptions);
        all.EnsureSuccessStatusCode();
        Assert.False(await factory.IsSessionActiveAsync(sessionIds[1]));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.session.revoke"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.sessions.revoke-all"));
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98916{sequence:D7}";
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

    private async Task<VerifyOtpResult> CreateSessionForPhoneAsync(HttpClient client, string phoneNumber)
    {
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

    private static Task<HttpResponseMessage> SendDeleteAsync(
        HttpClient client,
        Guid organizationId,
        Guid membershipId,
        string reason)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/admin/v1/organizations/{organizationId}/memberships/{membershipId}")
        {
            Content = JsonContent.Create(new EndPlatformMembershipCommand(reason), options: JsonOptions)
        };
        return client.SendAsync(request);
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
