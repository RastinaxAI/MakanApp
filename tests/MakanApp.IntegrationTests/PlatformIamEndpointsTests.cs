using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PlatformIamEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 7_100_000;

    [Fact]
    public async Task AnonymousAndOrganizationManagerCannotAccessBackofficeButOwnerCan()
    {
        await factory.ResetPlatformIamAsync();
        using var anonymousClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        var organizationId = await factory.CreateOrganizationAsync($"Platform boundary {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(managerClient, manager.AccessToken);

        using var anonymous = await anonymousClient.GetAsync("/api/admin/v1/me");
        using var organizationManager = await managerClient.GetAsync("/api/admin/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, organizationManager.StatusCode);

        await factory.BootstrapPlatformOwnerAsync(manager.User.Id);
        var owner = await managerClient.GetFromJsonAsync<PlatformMeResult>(
            "/api/admin/v1/me",
            JsonOptions);

        Assert.NotNull(owner);
        Assert.Equal(manager.User.Id, owner.UserId);
        Assert.Contains(PlatformRoleKeys.PlatformOwner, owner.Roles);
        Assert.Contains(PlatformPermissionKeys.RolesManage, owner.Permissions);
    }

    [Fact]
    public async Task BootstrapIsConcurrentSafeIdempotentAndClosesAfterFirstOwner()
    {
        await factory.ResetPlatformIamAsync();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateAuthenticatedUserAsync(firstClient);
        var second = await CreateAuthenticatedUserAsync(secondClient);

        var attempts = await Task.WhenAll(
            TryBootstrapAsync(first.User.Id),
            TryBootstrapAsync(second.User.Id));
        var succeeded = Assert.Single(attempts, item => item.Result is not null);
        var rejected = Assert.Single(attempts, item => item.Code == PlatformErrorCodes.BootstrapClosed);

        Assert.NotEqual(succeeded.UserId, rejected.UserId);
        Assert.Equal(1, await factory.CountActivePlatformOwnersAsync());
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.owner.bootstrap"));

        var replay = await factory.BootstrapPlatformOwnerAsync(succeeded.UserId);
        Assert.True(replay.Replayed);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.owner.bootstrap"));

        async Task<BootstrapAttempt> TryBootstrapAsync(Guid userId)
        {
            try
            {
                return new BootstrapAttempt(
                    userId,
                    await factory.BootstrapPlatformOwnerAsync(userId),
                    null);
            }
            catch (PlatformException exception)
            {
                return new BootstrapAttempt(userId, null, exception.Code);
            }
        }
    }

    [Fact]
    public async Task RoleGrantAndRevokeUseCurrentDatabasePermissionsAndWriteAudit()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var target = await CreateAuthenticatedUserAsync(targetClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(targetClient, target.AccessToken);

        var roles = await ownerClient.GetFromJsonAsync<PlatformRoleCatalogItem[]>(
            "/api/admin/v1/roles",
            JsonOptions);
        Assert.Equal(7, roles!.Length);
        Assert.DoesNotContain(
            roles.SelectMany(role => role.Permissions),
            permission => permission == "platform.private_chats.read_all");

        using var grantResponse = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(target.User.Id, PlatformRoleKeys.Support, "پشتیبانی موقت"),
            JsonOptions);
        var granted = await grantResponse.Content.ReadFromJsonAsync<PlatformRoleMutationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);
        Assert.NotNull(granted);
        using var targetAllowed = await targetClient.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.OK, targetAllowed.StatusCode);

        using var endResponse = await SendDeleteAsync(
            ownerClient,
            $"/api/admin/v1/role-assignments/{granted.Assignment.AssignmentId}",
            new EndPlatformRoleAssignmentCommand("پایان دسترسی موقت"));
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

        using var targetRevoked = await targetClient.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, targetRevoked.StatusCode);

        var assignments = await ownerClient.GetFromJsonAsync<PlatformRoleAssignmentPage>(
            $"/api/admin/v1/role-assignments?userId={target.User.Id:D}&activeOnly=false",
            JsonOptions);
        var ended = Assert.Single(assignments!.Items);
        Assert.False(ended.IsActive);

        var audit = await ownerClient.GetFromJsonAsync<AdminAuditLogPage>(
            "/api/admin/v1/audit-logs?page=1&pageSize=20&sortBy=occurredAtUtc&sortDirection=asc",
            JsonOptions);
        Assert.NotNull(audit);
        Assert.Contains(audit.Items, item => item.Action == "platform.role.grant");
        Assert.Contains(audit.Items, item => item.Action == "platform.role.revoke");
        Assert.All(audit.Items, item => Assert.DoesNotContain("token", item.AfterSummary ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.True(await factory.AdminAuditRejectsDeleteAsync());
    }

    [Fact]
    public async Task SensitiveRoleMutationRequiresPermissionAndReason()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var target = await CreateAuthenticatedUserAsync(targetClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);

        UseBearerToken(targetClient, target.AccessToken);
        using var denied = await targetClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(target.User.Id, PlatformRoleKeys.Support, "نیاز عملیاتی"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var missingReason = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(target.User.Id, PlatformRoleKeys.Support, " "),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(PlatformErrorCodes.ReasonRequired, await ReadProblemCodeAsync(missingReason));
    }

    [Fact]
    public async Task ConcurrentRevocationsCannotRemoveLastPlatformOwner()
    {
        await factory.ResetPlatformIamAsync();
        using var actorClient = factory.CreateClient();
        using var competingClient = factory.CreateClient();
        using var secondOwnerClient = factory.CreateClient();
        var actor = await CreateAuthenticatedUserAsync(actorClient);
        var secondOwner = await CreateAuthenticatedUserAsync(secondOwnerClient);
        var firstAssignment = await factory.BootstrapPlatformOwnerAsync(actor.User.Id);
        UseBearerToken(actorClient, actor.AccessToken);
        UseBearerToken(competingClient, actor.AccessToken);

        using var grantResponse = await actorClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(secondOwner.User.Id, PlatformRoleKeys.PlatformOwner, "مالک پشتیبان"),
            JsonOptions);
        var secondAssignment = (await grantResponse.Content.ReadFromJsonAsync<PlatformRoleMutationResult>(JsonOptions))!;
        grantResponse.EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(
            SendDeleteAsync(
                actorClient,
                $"/api/admin/v1/role-assignments/{firstAssignment.Assignment.AssignmentId}",
                new EndPlatformRoleAssignmentCommand("آزمون رقابت مالک اول")),
            SendDeleteAsync(
                competingClient,
                $"/api/admin/v1/role-assignments/{secondAssignment.Assignment.AssignmentId}",
                new EndPlatformRoleAssignmentCommand("آزمون رقابت مالک دوم")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response =>
                response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden);
            Assert.Equal(1, await factory.CountActivePlatformOwnersAsync());
            Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.role.revoke"));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task PlatformOwnerDoesNotBypassMessengerContactsOrModerationPrivacy()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var first = await CreateAuthenticatedUserAsync(firstClient);
        var second = await CreateAuthenticatedUserAsync(secondClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);

        var organizationId = await factory.CreateOrganizationAsync($"Privacy {Guid.NewGuid():N}");
        var conversationId = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            first.User.Id,
            MakanApp.Domain.Messaging.ConversationType.Group);
        await factory.AddConversationParticipantAsync(conversationId, second.User.Id);
        var messageId = await factory.CreateCommittedMessageAsync(
            conversationId,
            first.User.Id,
            "private platform evidence");
        var caseId = await factory.CreatePlatformSafetyCaseAsync(
            conversationId,
            messageId,
            second.User.Id,
            first.User.Id);
        await factory.AllowPersonalGroupInviteAsync(second.User.Id, first.User.Id);

        using var messages = await ownerClient.GetAsync($"/api/v1/conversations/{conversationId}/messages");
        using var evidence = await ownerClient.GetAsync(
            $"/api/v1/conversations/{conversationId}/moderation/cases/{caseId}");
        var contactsJson = await ownerClient.GetStringAsync("/api/v1/contacts");

        Assert.NotEqual(HttpStatusCode.OK, messages.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, evidence.StatusCode);
        Assert.DoesNotContain(first.User.Id.ToString("D"), contactsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(second.User.Id.ToString("D"), contactsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditQueryRejectsNonAllowlistedSort()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);

        using var response = await ownerClient.GetAsync(
            "/api/admin/v1/audit-logs?sortBy=reason&sortDirection=desc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PlatformErrorCodes.QueryInvalid, await ReadProblemCodeAsync(response));
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98915{sequence:D7}";
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
        string route,
        EndPlatformRoleAssignmentCommand command)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, route)
        {
            Content = JsonContent.Create(command, options: JsonOptions)
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

    private sealed record BootstrapAttempt(
        Guid UserId,
        PlatformBootstrapResult? Result,
        string? Code);
}
