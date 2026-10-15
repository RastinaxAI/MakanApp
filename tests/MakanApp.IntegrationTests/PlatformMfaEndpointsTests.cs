using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OtpNet;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class PlatformMfaEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _host = null!;
    private static int _sequence = 7_820_000;
    private DateTime Now => factory.Clock.GetUtcNow().UtcDateTime;
    public async Task InitializeAsync()
    {
        await factory.ResetPlatformIamAsync();
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(1));
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPlatformMfaAssurance>();
            services.RemoveAll<IPlatformStepUpAuthorization>();
            services.AddScoped<IPlatformMfaAssurance>(sp => sp.GetRequiredService<EfPlatformMfaAssurance>());
            services.AddScoped<IPlatformStepUpAuthorization>(sp => sp.GetRequiredService<EfPlatformMfaAssurance>());
            services.RemoveAll<IHostedService>();
        }));
    }
    public Task DisposeAsync() { _host.Dispose(); factory.Clock.UseSystemTime(); return Task.CompletedTask; }

    [Fact]
    public async Task EnrollmentRequiresProofAndProtectsSecretAndRecoveryCodes()
    {
        using var client = _host.CreateClient();
        var user = await OwnerAsync(client);
        client.DefaultRequestHeaders.Add("mfaVerified", "true");
        await ErrorAsync(await client.GetAsync("/api/admin/v1/me"), HttpStatusCode.Forbidden, PlatformErrorCodes.MfaRequired);
        var setup = await StartAsync(client);
        Assert.Contains("otpauth://totp/", setup.Enrollment!.SetupUri);
        var secret = Secret(setup);
        var pending = await client.GetFromJsonAsync<PlatformMfaResult>("/api/admin/v1/mfa");
        Assert.False(pending!.Enrolled);
        await ErrorAsync(await client.PostAsJsonAsync("/api/admin/v1/mfa/enrollment/confirm",
            new ConfirmPlatformMfaCommand(setup.Enrollment.EnrollmentId, "invalid")), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
        var confirmed = await ConfirmAsync(client, setup);
        Assert.True(confirmed.Enrolled);
        Assert.True(confirmed.SessionMfaVerified);
        Assert.Equal(10, confirmed.RecoveryCodes!.Count);
        Assert.Equal(10, confirmed.RecoveryCodes.Distinct().Count());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/v1/me")).StatusCode);
        var status = await client.GetStringAsync("/api/admin/v1/mfa");
        Assert.DoesNotContain(secret, status);
        Assert.DoesNotContain("otpauth", status);
        foreach (var code in confirmed.RecoveryCodes) Assert.DoesNotContain(code, status);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var credential = await db.PlatformMfaCredentials.Include(c => c.RecoveryCodes).SingleAsync(c => c.UserId == user.User.Id);
        Assert.NotEqual(secret, credential.ProtectedSecret);
        Assert.DoesNotContain(secret, credential.ProtectedSecret!);
        Assert.Null(credential.PendingProtectedSecret);
        Assert.All(credential.RecoveryCodes, code => Assert.DoesNotContain(code.CodeHash, status));
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.Action == "platform.mfa.enrolled" && a.ActorUserId == user.User.Id));
        var logs = string.Join('\n', _host.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot());
        Assert.DoesNotContain(secret, logs);
        Assert.DoesNotContain(setup.Enrollment.SetupUri, logs);
        Assert.DoesNotContain(Code(setup), logs);
        Assert.DoesNotContain(credential.ProtectedSecret!, logs);
        foreach (var code in confirmed.RecoveryCodes) Assert.DoesNotContain(code, logs);
    }

    [Fact]
    public async Task StepUpExpiresWhileBackofficeReadRemainsAssuredAndFreshTotpRestoresMutation()
    {
        using var ownerClient = _host.CreateClient();
        using var targetClient = _host.CreateClient();
        _ = await OwnerAsync(ownerClient);
        var target = await AuthenticateAsync(targetClient);
        var setup = await StartAsync(ownerClient);
        _ = await ConfirmAsync(ownerClient, setup);
        var command = new GrantPlatformRoleCommand(target.User.Id, PlatformRoleKeys.Support, "MFA integration test");
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync("/api/admin/v1/me")).StatusCode);
        await ErrorAsync(await ownerClient.PostAsJsonAsync("/api/admin/v1/role-assignments", command), HttpStatusCode.Forbidden, PlatformErrorCodes.StepUpRequired);
        var verified = await PostAsync(ownerClient, "/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(setup)));
        Assert.Equal(Now.AddMinutes(10), verified.StepUpValidUntilUtc);
        Assert.Equal(HttpStatusCode.Created, (await ownerClient.PostAsJsonAsync("/api/admin/v1/role-assignments", command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync("/api/admin/v1/roles")).StatusCode);
        await ErrorAsync(await ownerClient.PostAsJsonAsync("/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(setup))), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
    }

    [Fact]
    public async Task RecoveryCodeIsConsumedExactlyOnceUnderConcurrentRequests()
    {
        using var client = _host.CreateClient();
        _ = await OwnerAsync(client);
        var active = await ConfirmAsync(client, await StartAsync(client));
        var code = active.RecoveryCodes!.First();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(
            "/api/admin/v1/mfa/recovery/verify", new VerifyPlatformMfaCommand(code))));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        await ErrorAsync(Assert.Single(results, r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.mfa.recovery.used"));
    }

    [Fact]
    public async Task RecoveryRegenerationInvalidatesPreviousCodesAndRequiresFreshAssurance()
    {
        using var client = _host.CreateClient();
        _ = await OwnerAsync(client);
        var active = await ConfirmAsync(client, await StartAsync(client));
        var regenerated = await PostAsync(client, "/api/admin/v1/mfa/recovery-codes/regenerate", null);
        Assert.Equal(10, regenerated.RecoveryCodes!.Count);
        await ErrorAsync(await client.PostAsJsonAsync("/api/admin/v1/mfa/recovery/verify", new VerifyPlatformMfaCommand(active.RecoveryCodes!.First())), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
        await PostAsync(client, "/api/admin/v1/mfa/recovery/verify", new VerifyPlatformMfaCommand(regenerated.RecoveryCodes.First()));
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        await ErrorAsync(await client.PostAsync("/api/admin/v1/mfa/recovery-codes/regenerate", null), HttpStatusCode.Forbidden, PlatformErrorCodes.StepUpRequired);
    }

    [Fact]
    public async Task ResetRequiresFreshOwnerAndRevokesEveryTargetSessionAndRecoveryCode()
    {
        using var ownerClient = _host.CreateClient();
        using var targetClient = _host.CreateClient();
        var owner = await OwnerAsync(ownerClient);
        var ownerEnrollment = await StartAsync(ownerClient);
        _ = await ConfirmAsync(ownerClient, ownerEnrollment);
        var target = await AuthenticateAsync(targetClient);
        await GrantAsync(ownerClient, target.User.Id, PlatformRoleKeys.Support);
        var targetSetup = await StartAsync(targetClient);
        var targetActive = await ConfirmAsync(targetClient, targetSetup);
        using var targetSecondClient = _host.CreateClient();
        factory.Clock.SetUtcNow(Now.AddSeconds(61));
        await AuthenticateAsync(targetSecondClient, _phones[target.User.Id]);
        await PostAsync(targetSecondClient, "/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(targetSetup)));
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        var route = $"/api/admin/v1/mfa/users/{target.User.Id}/reset";
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route, new ResetPlatformMfaCommand("Approved device loss")), HttpStatusCode.Forbidden, PlatformErrorCodes.StepUpRequired);
        await PostAsync(ownerClient, "/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(ownerEnrollment)));
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route, new ResetPlatformMfaCommand("")), HttpStatusCode.BadRequest, PlatformErrorCodes.ReasonRequired);
        await PostAsync(ownerClient, route, new ResetPlatformMfaCommand("Approved device loss"));
        await ErrorAsync(await targetClient.GetAsync("/api/admin/v1/me"), HttpStatusCode.Forbidden, PlatformErrorCodes.MfaRequired);
        await ErrorAsync(await targetSecondClient.GetAsync("/api/admin/v1/me"), HttpStatusCode.Forbidden, PlatformErrorCodes.MfaRequired);
        await ErrorAsync(await targetClient.PostAsJsonAsync("/api/admin/v1/mfa/recovery/verify", new VerifyPlatformMfaCommand(targetActive.RecoveryCodes!.First())), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
        var status = await targetClient.GetFromJsonAsync<PlatformMfaResult>("/api/admin/v1/mfa");
        Assert.False(status!.Enrolled);
        Assert.NotNull((await StartAsync(targetClient)).Enrollment);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.All(await db.UserSessions.Where(s => s.UserId == target.User.Id).ToArrayAsync(), s => Assert.Null(s.StepUpAtUtc));
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.Action == "platform.mfa.reset" && a.ActorUserId == owner.User.Id && a.TargetId == target.User.Id.ToString()));
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task OrganizationRoleAloneCannotManagePlatformMfa(OrganizationRole role)
    {
        using var client = _host.CreateClient();
        var user = await AuthenticateAsync(client);
        var organization = await factory.CreateOrganizationAsync("MFA boundary " + Guid.NewGuid().ToString("N"));
        await factory.CreateMembershipAsync(user.User.Id, organization, role);
        await ErrorAsync(await client.PostAsync("/api/admin/v1/mfa/enrollment", null), HttpStatusCode.Forbidden, PlatformErrorCodes.AccessDenied);
        await ErrorAsync(await client.PostAsJsonAsync($"/api/admin/v1/mfa/users/{Guid.NewGuid()}/reset", new ResetPlatformMfaCommand("hostile")), HttpStatusCode.Forbidden, PlatformErrorCodes.AccessDenied);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task NonOwnerCannotResetOtherUserEvenWithMfa()
    {
        using var ownerClient = _host.CreateClient();
        using var supportClient = _host.CreateClient();
        var owner = await OwnerAsync(ownerClient);
        _ = await ConfirmAsync(ownerClient, await StartAsync(ownerClient));
        var support = await AuthenticateAsync(supportClient);
        await GrantAsync(ownerClient, support.User.Id, PlatformRoleKeys.Support);
        _ = await ConfirmAsync(supportClient, await StartAsync(supportClient));
        await ErrorAsync(await supportClient.PostAsJsonAsync($"/api/admin/v1/mfa/users/{owner.User.Id}/reset", new ResetPlatformMfaCommand("hostile")), HttpStatusCode.Forbidden, PlatformErrorCodes.AccessDenied);
    }

    [Fact]
    public async Task WrongCodesAreBoundedPersistentlyAndDoNotLeakInputs()
    {
        using var client = _host.CreateClient();
        _ = await OwnerAsync(client);
        var setup = await StartAsync(client);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/admin/v1/mfa/enrollment/confirm", new ConfirmPlatformMfaCommand(setup.Enrollment!.EnrollmentId, "secret-invalid-code"));
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("secret-invalid-code", body);
            Assert.DoesNotContain(Secret(setup), body);
            Assert.Equal(attempt < 5 ? HttpStatusCode.BadRequest : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        await ErrorAsync(await client.PostAsync("/api/admin/v1/mfa/enrollment", null), HttpStatusCode.TooManyRequests, PlatformErrorCodes.MfaRateLimited);
        factory.Clock.SetUtcNow(Now.AddMinutes(6));
        _ = await ConfirmAsync(client, setup);
    }

    [Fact]
    public async Task EnrollmentIsBoundToSessionAndExpiresAndActiveEnrollmentCannotBeReplacedWithoutMfa()
    {
        using var client = _host.CreateClient();
        var user = await OwnerAsync(client);
        var setup = await StartAsync(client);
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        await ErrorAsync(await client.PostAsJsonAsync("/api/admin/v1/mfa/enrollment/confirm", new ConfirmPlatformMfaCommand(setup.Enrollment!.EnrollmentId, Code(setup))), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
        var fresh = await StartAsync(client);
        using var second = _host.CreateClient();
        factory.Clock.SetUtcNow(Now.AddSeconds(61));
        await AuthenticateAsync(second, _phones[user.User.Id]);
        await ErrorAsync(await second.PostAsJsonAsync("/api/admin/v1/mfa/enrollment/confirm", new ConfirmPlatformMfaCommand(fresh.Enrollment!.EnrollmentId, Code(fresh))), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
        _ = await ConfirmAsync(client, fresh);
        await ErrorAsync(await second.PostAsync("/api/admin/v1/mfa/enrollment", null), HttpStatusCode.Forbidden, PlatformErrorCodes.StepUpRequired);
        await ErrorAsync(await second.GetAsync("/api/admin/v1/me"), HttpStatusCode.Forbidden, PlatformErrorCodes.MfaRequired);
        await ErrorAsync(await second.PostAsJsonAsync("/api/admin/v1/mfa/enrollment/confirm", new ConfirmPlatformMfaCommand(fresh.Enrollment!.EnrollmentId, Code(fresh))), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
    }

    [Fact]
    public async Task CurrentPlatformPermissionStillRequiredAfterMfaAndAnonymousIsRejected()
    {
        using var anonymous = _host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/v1/mfa")).StatusCode);
        using var ownerClient = _host.CreateClient();
        using var supportClient = _host.CreateClient();
        _ = await OwnerAsync(ownerClient);
        _ = await ConfirmAsync(ownerClient, await StartAsync(ownerClient));
        var support = await AuthenticateAsync(supportClient);
        var assignment = await GrantAsync(ownerClient, support.User.Id, PlatformRoleKeys.Support);
        _ = await ConfirmAsync(supportClient, await StartAsync(supportClient));
        Assert.Equal(HttpStatusCode.OK, (await supportClient.GetAsync("/api/admin/v1/me")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/v1/role-assignments/{assignment.Assignment.AssignmentId}")
        { Content = JsonContent.Create(new EndPlatformRoleAssignmentCommand("MFA revocation test")) };
        (await ownerClient.SendAsync(request)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await supportClient.GetAsync("/api/admin/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supportClient.GetAsync("/api/admin/v1/mfa")).StatusCode);
    }

    private readonly Dictionary<Guid, string> _phones = [];

    [Fact]
    public async Task TotpReplayIsRejectedAcrossConcurrentRequests()
    {
        using var client = _host.CreateClient();
        _ = await OwnerAsync(client);
        var setup = await StartAsync(client);
        _ = await ConfirmAsync(client, setup);
        factory.Clock.SetUtcNow(Now.AddSeconds(31));
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(
            "/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(setup)))));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        await ErrorAsync(Assert.Single(results, r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.BadRequest, PlatformErrorCodes.MfaInvalid);
    }

    [Fact]
    public async Task PlainAccountCannotEnrollAndMfaCannotBeGrantedByFrontendClaim()
    {
        using var client = _host.CreateClient();
        _ = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Add("mfaVerified", "true");
        await ErrorAsync(await client.PostAsync("/api/admin/v1/mfa/enrollment", null), HttpStatusCode.Forbidden, PlatformErrorCodes.AccessDenied);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/v1/me")).StatusCode);
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
        var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        var code = _host.Services.GetRequiredService<OtpProviderTestControl>().GetLatestCode(phone);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, phone, code));
        verify.EnsureSuccessStatusCode();
        var result = (await verify.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        _phones[result.User.Id] = phone;
        return result;
    }
    private async Task<PlatformMfaResult> StartAsync(HttpClient client)
    {
        var setup = await PostAsync(client, "/api/admin/v1/mfa/enrollment", null);
        return setup;
    }
    private Task<PlatformMfaResult> ConfirmAsync(HttpClient client, PlatformMfaResult setup) => PostAsync(client,
        "/api/admin/v1/mfa/enrollment/confirm", new ConfirmPlatformMfaCommand(setup.Enrollment!.EnrollmentId, Code(setup)));
    private static string Secret(PlatformMfaResult setup) => Uri.UnescapeDataString(setup.Enrollment!.SetupUri.Split("secret=")[1].Split('&')[0]);
    private string Code(PlatformMfaResult setup) => new Totp(Base32Encoding.ToBytes(Secret(setup)), step: 30, totpSize: 6).ComputeTotp(Now);
    private static async Task<PlatformMfaResult> PostAsync(HttpClient client, string route, object? command)
    {
        using var response = command is null ? await client.PostAsync(route, null) : await client.PostAsJsonAsync(route, command);
        Assert.True(response.IsSuccessStatusCode, $"{route}: {(int)response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<PlatformMfaResult>())!;
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
        using var response = await owner.PostAsJsonAsync("/api/admin/v1/role-assignments", new GrantPlatformRoleCommand(userId, role, "MFA test role assignment"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlatformRoleMutationResult>())!;
    }
}
