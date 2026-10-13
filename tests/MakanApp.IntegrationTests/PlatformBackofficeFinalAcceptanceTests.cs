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
public sealed class PlatformBackofficeFinalAcceptanceTests(
    MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 7_600_000;

    [Fact]
    public async Task OrganizationManagerNeverBecomesPlatformAdminAndRevocationTakesEffectImmediately()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        var organizationId = await factory.CreateOrganizationAsync($"Boundary {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(managerClient, manager.AccessToken);

        using var managerDenied = await managerClient.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, managerDenied.StatusCode);

        using var grantResponse = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(
                manager.User.Id,
                PlatformRoleKeys.Support,
                "دسترسی موقت برای آزمون پذیرش"),
            JsonOptions);
        var granted = await ReadAsync<PlatformRoleMutationResult>(grantResponse);
        using var allowed = await managerClient.GetAsync("/api/admin/v1/operations/health");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var revokeResponse = await SendDeleteAsync(
            ownerClient,
            $"/api/admin/v1/role-assignments/{granted.Assignment.AssignmentId:D}",
            new EndPlatformRoleAssignmentCommand("پایان دسترسی موقت آزمون پذیرش"));
        revokeResponse.EnsureSuccessStatusCode();
        using var deniedImmediately = await managerClient.GetAsync(
            "/api/admin/v1/operations/health");

        Assert.Equal(HttpStatusCode.Forbidden, deniedImmediately.StatusCode);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.role.grant"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.role.revoke"));
    }

    [Fact]
    public async Task HostileResourceIdentifiersRemainBehindPlatformAuthorization()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        var organizationId = await factory.CreateOrganizationAsync($"Hostile IDs {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(managerClient, manager.AccessToken);
        var hostileOrganizationId = Guid.NewGuid();
        var hostileResourceId = Guid.NewGuid();

        var forbiddenRoutes = new[]
        {
            $"/api/admin/v1/organizations/{hostileOrganizationId:D}",
            $"/api/admin/v1/users/{hostileResourceId:D}",
            $"/api/admin/v1/education/classes/{hostileResourceId:D}",
            $"/api/admin/v1/education/teachers?organizationId={hostileOrganizationId:D}&classId={hostileResourceId:D}",
            $"/api/admin/v1/education/enrollments?organizationId={hostileOrganizationId:D}&classId={hostileResourceId:D}",
            $"/api/admin/v1/commerce/subscriptions?organizationId={hostileOrganizationId:D}",
            $"/api/admin/v1/finance/orders/{hostileResourceId:D}",
            $"/api/admin/v1/moderation/cases/{hostileResourceId:D}"
        };
        foreach (var route in forbiddenRoutes)
        {
            using var response = await managerClient.GetAsync(route);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var organization = await ownerClient.GetAsync(
            $"/api/admin/v1/organizations/{hostileOrganizationId:D}");
        using var user = await ownerClient.GetAsync(
            $"/api/admin/v1/users/{hostileResourceId:D}");
        using var classDetail = await ownerClient.GetAsync(
            $"/api/admin/v1/education/classes/{hostileResourceId:D}");
        using var order = await ownerClient.GetAsync(
            $"/api/admin/v1/finance/orders/{hostileResourceId:D}");
        using var moderation = await ownerClient.GetAsync(
            $"/api/admin/v1/moderation/cases/{hostileResourceId:D}");
        var subscriptions = await ownerClient.GetFromJsonAsync<PlatformSubscriptionPage>(
            $"/api/admin/v1/commerce/subscriptions?organizationId={hostileOrganizationId:D}",
            JsonOptions);
        var teachers = await ownerClient.GetFromJsonAsync<PlatformTeacherAssignmentPage>(
            $"/api/admin/v1/education/teachers?organizationId={hostileOrganizationId:D}&classId={hostileResourceId:D}",
            JsonOptions);
        var enrollments = await ownerClient.GetFromJsonAsync<PlatformEnrollmentPage>(
            $"/api/admin/v1/education/enrollments?organizationId={hostileOrganizationId:D}&classId={hostileResourceId:D}",
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, organization.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, user.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, classDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, order.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, moderation.StatusCode);
        Assert.Empty(subscriptions!.Items);
        Assert.Empty(teachers!.Items);
        Assert.Empty(enrollments!.Items);
    }

    [Fact]
    public async Task OperationalAndAuditContractsAreProtectedAndDoNotExposeCredentials()
    {
        await factory.ResetPlatformIamAsync();
        using var anonymousClient = factory.CreateClient();
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);

        using var anonymous = await anonymousClient.GetAsync("/api/admin/v1/operations/health");
        using var healthResponse = await ownerClient.GetAsync("/api/admin/v1/operations/health");
        var healthJson = await healthResponse.Content.ReadAsStringAsync();
        using var auditResponse = await ownerClient.GetAsync(
            "/api/admin/v1/audit-logs?page=1&pageSize=20");
        var auditJson = await auditResponse.Content.ReadAsStringAsync();
        using var deleteAudit = await ownerClient.DeleteAsync(
            $"/api/admin/v1/audit-logs/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        healthResponse.EnsureSuccessStatusCode();
        auditResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, deleteAudit.StatusCode);
        Assert.DoesNotContain("Server=", healthJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database=", healthJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", healthJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id=", healthJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", healthJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", healthJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", auditJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private chat", auditJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(await factory.AdminAuditRejectsDeleteAsync());
    }

    [Fact]
    public async Task OpenApiContainsFinalBackofficeContractWithoutForbiddenSurfaces()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var requiredOperations = new (string Path, string[] Methods)[]
        {
            ("/api/admin/v1/me", ["get"]),
            ("/api/admin/v1/roles", ["get"]),
            ("/api/admin/v1/role-assignments", ["get", "post"]),
            ("/api/admin/v1/role-assignments/{assignmentId}", ["delete"]),
            ("/api/admin/v1/audit-logs", ["get"]),
            ("/api/admin/v1/organizations", ["get", "post"]),
            ("/api/admin/v1/organizations/{organizationId}", ["get"]),
            ("/api/admin/v1/organizations/{organizationId}/suspension-impact", ["get"]),
            ("/api/admin/v1/organizations/{organizationId}/suspend", ["post"]),
            ("/api/admin/v1/organizations/{organizationId}/reactivate", ["post"]),
            ("/api/admin/v1/organizations/{organizationId}/memberships", ["get"]),
            ("/api/admin/v1/organizations/{organizationId}/invitations", ["post"]),
            ("/api/admin/v1/organizations/{organizationId}/memberships/{membershipId}/role", ["put"]),
            ("/api/admin/v1/organizations/{organizationId}/memberships/{membershipId}", ["delete"]),
            ("/api/admin/v1/provisioning", ["get", "post"]),
            ("/api/admin/v1/users", ["get"]),
            ("/api/admin/v1/users/{userId}", ["get"]),
            ("/api/admin/v1/users/{userId}/suspend", ["post"]),
            ("/api/admin/v1/users/{userId}/reactivate", ["post"]),
            ("/api/admin/v1/users/{userId}/sessions/revoke", ["post"]),
            ("/api/admin/v1/users/{userId}/sessions/{sessionId}/revoke", ["post"]),
            ("/api/admin/v1/education/classes", ["get"]),
            ("/api/admin/v1/education/classes/{classId}", ["get"]),
            ("/api/admin/v1/education/teachers", ["get"]),
            ("/api/admin/v1/education/enrollments", ["get"]),
            ("/api/admin/v1/education/assignments", ["get"]),
            ("/api/admin/v1/education/exams", ["get"]),
            ("/api/admin/v1/messaging/conversations", ["get"]),
            ("/api/admin/v1/moderation/cases", ["get"]),
            ("/api/admin/v1/moderation/cases/{caseId}", ["get"]),
            ("/api/admin/v1/moderation/cases/{caseId}/start-review", ["post"]),
            ("/api/admin/v1/moderation/cases/{caseId}/resolve", ["post"]),
            ("/api/admin/v1/moderation/cases/{caseId}/dismiss", ["post"]),
            ("/api/admin/v1/moderation/cases/{caseId}/enforce-content-removal", ["post"]),
            ("/api/admin/v1/moderation/cases/{caseId}/evidence/files/{fileAssetId}", ["get"]),
            ("/api/admin/v1/commerce/plans", ["get", "post"]),
            ("/api/admin/v1/commerce/plans/{planId}", ["get"]),
            ("/api/admin/v1/commerce/plans/{planId}/versions", ["post"]),
            ("/api/admin/v1/commerce/plan-versions/{planVersionId}/publish", ["post"]),
            ("/api/admin/v1/commerce/subscriptions", ["get"]),
            ("/api/admin/v1/commerce/organizations/{organizationId}/subscription-override", ["post"]),
            ("/api/admin/v1/finance/orders", ["get"]),
            ("/api/admin/v1/finance/orders/{orderId}", ["get"]),
            ("/api/admin/v1/operations/notifications", ["get"]),
            ("/api/admin/v1/operations/jobs", ["get"]),
            ("/api/admin/v1/operations/health", ["get"])
        };
        foreach (var operation in requiredOperations)
        {
            Assert.True(paths.TryGetProperty(operation.Path, out var path), operation.Path);
            Assert.All(operation.Methods, method => Assert.True(path.TryGetProperty(method, out _),
                $"{method.ToUpperInvariant()} {operation.Path}"));
        }

        Assert.DoesNotContain(paths.EnumerateObject().Select(item => item.Name), path =>
            path.Contains("private-chat", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("mark-successful", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/api/admin/v1/ai", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/api/admin/v1/landing", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98919{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await ReadAsync<RequestOtpResult>(challengeResponse);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        return await ReadAsync<VerifyOtpResult>(verifyResponse);
    }

    private static Task<HttpResponseMessage> SendDeleteAsync<T>(
        HttpClient client,
        string route,
        T command)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, route)
        {
            Content = JsonContent.Create(command, options: JsonOptions)
        };
        return client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
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
