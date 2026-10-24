using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class PlatformAuthorizationRegressionTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _host = null!;
    private static int _sequence = 7_820_000;
    private DateTime Now => factory.Clock.GetUtcNow().UtcDateTime;

    public async Task InitializeAsync()
    {
        await factory.ResetPlatformIamAsync();
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(1));
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.RemoveAll<IHostedService>()));
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        factory.Clock.UseSystemTime();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OtpAuthenticatedOwnerGetsActualRolesAndPermissionsWithoutSecondaryFactor()
    {
        using var client = _host.CreateClient();
        var owner = await OwnerAsync(client);
        using var response = await client.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<PlatformMeResult>())!;
        Assert.Equal(owner.User.Id, result.UserId);
        Assert.Contains(PlatformRoleKeys.PlatformOwner, result.Roles);
        Assert.Contains(PlatformPermissionKeys.Access, result.Permissions);
        Assert.Contains(PlatformPermissionKeys.RolesManage, result.Permissions);
    }

    [Theory]
    [InlineData(OrganizationRole.Manager)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Student)]
    public async Task SchoolRoleDoesNotGrantPlatformAccess(OrganizationRole role)
    {
        using var client = _host.CreateClient();
        var user = await AuthenticateAsync(client);
        var organizationId = await factory.CreateOrganizationAsync($"Platform isolation {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(user.User.Id, organizationId, role);
        using var response = await client.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SensitiveRoleMutationKeepsPermissionReasonAndAuditRequirements()
    {
        using var ownerClient = _host.CreateClient();
        using var targetClient = _host.CreateClient();
        _ = await OwnerAsync(ownerClient);
        var target = await AuthenticateAsync(targetClient);
        var command = new GrantPlatformRoleCommand(target.User.Id, PlatformRoleKeys.Support, "نیاز عملیاتی تأییدشده");
        using var denied = await targetClient.PostAsJsonAsync("/api/admin/v1/role-assignments", command);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await ErrorAsync(await ownerClient.PostAsJsonAsync("/api/admin/v1/role-assignments", command with { Reason = "" }),
            HttpStatusCode.BadRequest, PlatformErrorCodes.ReasonRequired);
        using var allowed = await ownerClient.PostAsJsonAsync("/api/admin/v1/role-assignments", command);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.role.grant"));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("absolute-expired")]
    [InlineData("suspended")]
    public async Task OwnerStillNeedsAnActiveAccountAndCurrentSession(string invalidState)
    {
        using var client = _host.CreateClient();
        var owner = await OwnerAsync(client);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var sessions = db.UserSessions.Where(session => session.UserId == owner.User.Id);
            var past = Now.AddSeconds(-1);
            if (invalidState == "revoked")
                await sessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAtUtc, past));
            else if (invalidState == "expired")
                await sessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.ExpiresAtUtc, past));
            else if (invalidState == "absolute-expired")
                await sessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.AbsoluteExpiresAtUtc, past));
            else
            {
                var user = await db.Users.SingleAsync(user => user.Id == owner.User.Id);
                user.Suspend(Now);
                await db.SaveChangesAsync();
            }
        }
        using var response = await client.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousPlatformAccessIsUnauthorizedAndRemovedRoutesAreNotPublished()
    {
        using var client = _host.CreateClient();
        using var anonymous = await client.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.DoesNotContain(document.RootElement.GetProperty("paths").EnumerateObject(),
            path => path.Name.StartsWith("/api/admin/v1/mfa", StringComparison.OrdinalIgnoreCase));
        using var removed = await client.GetAsync("/api/admin/v1/mfa");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    [Fact]
    public async Task AgeStoreRechecksTheSessionInsteadOfTrustingOnlyTheOwnerRole()
    {
        using var ownerClient = _host.CreateClient();
        using var targetClient = _host.CreateClient();
        var owner = await OwnerAsync(ownerClient);
        var target = await AuthenticateAsync(targetClient);
        await using var scope = _host.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICommunicationAgeStore>();
        var error = await Assert.ThrowsAsync<PlatformException>(() => store.GetPlatformAsync(
            owner.User.Id, Guid.NewGuid(), target.User.Id, "invalid-session-regression", CancellationToken.None));
        Assert.Equal(PlatformErrorCodes.AccessDenied, error.Code);
    }

    private async Task<VerifyOtpResult> OwnerAsync(HttpClient client)
    {
        var user = await AuthenticateAsync(client);
        await factory.BootstrapPlatformOwnerAsync(user.User.Id);
        return user;
    }

    private async Task<VerifyOtpResult> AuthenticateAsync(HttpClient client, string? existingPhone = null)
    {
        var phone = existingPhone ?? $"+98917{Interlocked.Increment(ref _sequence):D7}";
        using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        var code = _host.Services.GetRequiredService<OtpProviderTestControl>().GetLatestCode(phone);
        using var verify = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, phone, code));
        verify.EnsureSuccessStatusCode();
        var result = (await verify.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return result;
    }

    private static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        }
    }

    private static async Task<PlatformRoleMutationResult> GrantAsync(HttpClient owner, Guid userId, string role)
    {
        using var response = await owner.PostAsJsonAsync("/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(userId, role, "تخصیص نقش برای آزمون مجوز"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlatformRoleMutationResult>())!;
    }
}
