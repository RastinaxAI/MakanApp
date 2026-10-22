using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ConfigurableOtpAuthenticationTests(MakanAppWebApplicationFactory factory)
{
    private WebApplicationFactory<Program> MockApplication() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<OtpOptions>();
            services.AddSingleton(new OtpOptions { Enabled = false });
            services.RemoveAll<IOptions<OtpAuthenticationOptions>>();
            services.AddSingleton(Options.Create(new OtpAuthenticationOptions { Enabled = false, AllowMockAuthentication = true }));
            services.RemoveAll<IOtpDeliveryProvider>();
            services.AddSingleton<IOtpDeliveryProvider, UnavailableOtpDeliveryProvider>();
            services.RemoveAll<IPlatformMfaAssurance>();
            services.AddScoped<IPlatformMfaAssurance>(provider => provider.GetRequiredService<EfPlatformMfaAssurance>());
            services.RemoveAll<IPlatformStepUpAuthorization>();
            services.AddScoped<IPlatformStepUpAuthorization>(provider => provider.GetRequiredService<EfPlatformMfaAssurance>());
        }));

    [Theory]
    [InlineData("+12025550171")]
    [InlineData("۰۰۱۲۰۲۵۵۵۰۱۷۲")]
    public async Task MockRequest_AcceptsNormalizedFictionalPhone_WithoutSmsOrChallenge(string phone)
    {
        using var app = MockApplication();
        using var client = app.CreateClient();
        var before = await ChallengeCountAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RequestOtpResult>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ChallengeId);
        Assert.Equal(before, await ChallengeCountAsync());
    }

    [Theory]
    [InlineData("1111")]
    [InlineData("definitely-not-an-otp")]
    [InlineData("۱۲۳۴")]
    public async Task MockVerify_RequiresNeitherChallengeNorMatchingCode_AndUsesNormalTokens(string code)
    {
        using var app = MockApplication();
        using var client = app.CreateClient();
        var phone = "+12025550173";
        var before = await ChallengeCountAsync();
        var result = await LoginAsync(client, phone, code);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.False(result.AccessToken.Contains('.'));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        using var profile = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionCommand(result.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(before, await ChallengeCountAsync());
    }

    [Fact]
    public async Task MockVerify_IgnoresAnExpiredRealChallengeWithoutMutatingIt()
    {
        using var real = factory.CreateClient();
        using var requested = await real.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand("+12025550178"));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var challenge = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        await factory.ExpireChallengeAsync(challenge.ChallengeId);
        using var app = MockApplication();
        using var mock = app.CreateClient();
        using var response = await mock.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, "+12025550178", "not-the-provider-code"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OtpChallenges
            .SingleAsync(item => item.Id == challenge.ChallengeId);
        Assert.Equal(0, row.FailedAttempts);
    }

    [Fact]
    public async Task MockRequestIdentifier_IsNotOtpProofWhenRealModeIsEnabled()
    {
        using var app = MockApplication();
        using var mock = app.CreateClient();
        using var requested = await mock.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand("+12025550179"));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var challenge = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        using var real = factory.CreateClient();
        using var rejected = await real.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, "+12025550179", "arbitrary-code"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task RepeatedMockLogin_ResolvesExistingAccountWithoutDuplicateRegistration()
    {
        using var app = MockApplication();
        using var client = app.CreateClient();
        var first = await LoginAsync(client, "+12025550174", "any-code");
        var second = await LoginAsync(client, "+12025550174", "a-different-code");
        Assert.Equal(first.User.Id, second.User.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.UserCredentials.CountAsync(item => item.NormalizedIdentifier == "+12025550174"));
    }

    [Fact]
    public async Task MockAuthentication_DoesNotBypassAccountSuspension()
    {
        using var app = MockApplication();
        using var client = app.CreateClient();
        var loggedIn = await LoginAsync(client, "+12025550175", "arbitrary");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Users.SingleAsync(item => item.Id == loggedIn.User.Id)).Suspend(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(Guid.NewGuid(), "+12025550175", "anything-else"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MockSession_DoesNotGrantPlatformRoleOrMfaAssurance()
    {
        using var app = MockApplication();
        using var client = app.CreateClient();
        var result = await LoginAsync(client, "+12025550176", "not-a-secret");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        using (var forbidden = await client.GetAsync("/api/admin/v1/me"))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.PlatformRoleAssignments.Add(PlatformRoleAssignment.CreateActive(
                result.User.Id, PlatformCatalogIds.PlatformOwnerRole, null, DateTime.UtcNow));
            await db.SaveChangesAsync();
            var session = await db.UserSessions.SingleAsync(item => item.UserId == result.User.Id);
            Assert.Equal(AuthenticationAssuranceLevel.Primary, session.AssuranceLevel);
        }
        try
        {
            using var requiresMfa = await client.GetAsync("/api/admin/v1/me");
            Assert.Equal(HttpStatusCode.Forbidden, requiresMfa.StatusCode);
            Assert.Contains(PlatformErrorCodes.MfaRequired, await requiresMfa.Content.ReadAsStringAsync());
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.PlatformRoleAssignments.RemoveRange(await db.PlatformRoleAssignments
                .Where(item => item.UserId == result.User.Id).ToListAsync());
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task MockStillRejectsInvalidPhone_AndDoesNotExposeCodeInLogs()
    {
        using var app = MockApplication();
        using var client = app.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand("invalid-phone"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var marker = Guid.NewGuid().ToString("N");
        await LoginAsync(client, "+12025550177", marker);
        var logs = app.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot();
        Assert.NotEmpty(logs);
        Assert.All(logs, line => Assert.False(line.Contains(marker, StringComparison.Ordinal)));
    }

    private async Task<int> ChallengeCountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OtpChallenges.CountAsync();
    }

    private static async Task<VerifyOtpResult> LoginAsync(HttpClient client, string phone, string code)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(Guid.Empty, phone, code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
    }
}
