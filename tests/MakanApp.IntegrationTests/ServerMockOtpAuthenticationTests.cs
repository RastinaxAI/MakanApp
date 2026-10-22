using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ServerMockOtpAuthenticationTests(MakanAppWebApplicationFactory factory) : IDisposable
{
    private readonly string _keyRingPath = Path.Combine(Path.GetTempPath(), "MakanApp-ServerMockOtpKeys", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("09123456789", "123456")]
    [InlineData("09999999999", "999999")]
    [InlineData("09999999999", "987654")]
    [InlineData("09111111111", "000000")]
    [InlineData("09123456789", "456789")]
    public async Task ConfiguredMock_RequestAndVerifyArbitraryCode_UsesRealRegistrationAndExistingAccountFlow(string phone, string code)
    {
        using var app = Application();
        using var client = Client(app);
        Assert.True(PhoneNumberNormalizer.TryNormalize(phone, out var normalized));
        Guid? existingId;
        await using (var scope = factory.Services.CreateAsyncScope())
            existingId = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UserCredentials
                .Where(item => item.NormalizedIdentifier == normalized).Select(item => (Guid?)item.UserId).SingleOrDefaultAsync();
        var before = await ChallengeCountAsync();
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var challenge = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        using var verified = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, phone, code));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var result = (await verified.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
        Assert.Equal(normalized, result.User.PhoneNumber);
        if (existingId.HasValue) Assert.Equal(existingId.Value, result.User.Id);
        Assert.Equal(before, await ChallengeCountAsync());
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.UserCredentials.CountAsync(item => item.NormalizedIdentifier == normalized));
        Assert.Empty(await db.Memberships.Where(item => item.UserId == result.User.Id).ToListAsync());
        Assert.Empty(await db.PlatformRoleAssignments.Where(item => item.UserId == result.User.Id).ToListAsync());
    }

    [Fact]
    public async Task EffectiveRealOverride_ReproducesUnknownPhoneOtpFailure_AndCannotBeChangedByRequestHeaders()
    {
        using var app = Application(enabled: true, allowMock: false);
        using var client = Client(app);
        Assert.True(app.Services.GetRequiredService<OtpOptions>().Enabled);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/otp/verify")
        {
            Content = JsonContent.Create(new VerifyOtpCommand(Guid.Empty, "09999999999", "999999"))
        };
        request.Headers.Add("Otp-Enabled", "false");
        request.Headers.Add("AllowMockAuthentication", "true");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(IdentityErrorCodes.OtpInvalid, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EffectiveModeIsLoggedWithoutCodePhoneOrSessionCredentials()
    {
        var logs = new SensitiveLogTestControl();
        using var app = Application(logs: logs);
        using var client = Client(app);
        var enteredCode = Guid.NewGuid().ToString("N");
        const string phone = "09120000205";
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(Guid.Empty, phone, enteredCode));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
        var entries = logs.Snapshot();
        Assert.Contains(entries, item => item.Contains("MockOtpAuthenticationEnabled", StringComparison.Ordinal) ||
            item.Contains("احراز هویت آزمایشی فعال است", StringComparison.Ordinal));
        Assert.All(entries, item =>
        {
            Assert.DoesNotContain(enteredCode, item);
            Assert.DoesNotContain(phone, item);
            Assert.DoesNotContain(result.AccessToken, item);
            Assert.DoesNotContain(result.RefreshToken, item);
        });
    }

    [Theory]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("1234\n5678")]
    public async Task ConfiguredMock_RejectsInvalidCodeFormatWithoutCreatingAnAccount(string code)
    {
        using var app = Application();
        using var client = Client(app);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(Guid.Empty, "09999999999", code));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(IdentityErrorCodes.OtpInvalid, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("09120000201", "111111")]
    [InlineData("۰۹۱۲۰۰۰۰۲۰۲", "not-a-real-code")]
    public async Task ConfiguredTestHost_LogsInWithoutPriorChallengeOrSms_AndPreservesSessionRules(string phone, string code)
    {
        using var app = Application();
        using var client = Client(app);
        Assert.Equal("Test", app.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
        Assert.False(app.Services.GetRequiredService<OtpOptions>().Enabled);
        Assert.IsType<UnavailableOtpDeliveryProvider>(app.Services.GetRequiredService<IOtpDeliveryProvider>());
        var before = await ChallengeCountAsync();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(Guid.Empty, phone, code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal(before, await ChallengeCountAsync());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        using var profile = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var admin = await client.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        using var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionCommand(result.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ConfiguredTestHost_RequestDoesNotCreateOtp_AndSwaggerIsReachable()
    {
        using var app = Application();
        using var client = Client(app);
        var before = await ChallengeCountAsync();
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand("09120000203"));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.NotEqual(Guid.Empty, (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!.ChallengeId);
        Assert.Equal(before, await ChallengeCountAsync());
        using var swagger = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    [Fact]
    public async Task ConfiguredTestHost_ExistingPlatformOwnerStillRequiresMfa()
    {
        using var app = Application();
        using var client = Client(app);
        var user = User.Create(DateTime.UtcNow);
        var assignment = PlatformRoleAssignment.CreateActive(user.Id, PlatformCatalogIds.PlatformOwnerRole, null, DateTime.UtcNow);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.Users.Add(user);
            db.UserCredentials.Add(UserCredential.CreateMobile(user.Id, "+989120000204", DateTime.UtcNow));
            db.PlatformRoleAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }
        try
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
                new VerifyOtpCommand(Guid.NewGuid(), "09120000204", "arbitrary"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = (await response.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
            Assert.Equal(user.Id, result.User.Id);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            using var admin = await client.GetAsync("/api/admin/v1/me");
            Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
            Assert.Contains(PlatformErrorCodes.MfaRequired, await admin.Content.ReadAsStringAsync());
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.PlatformRoleAssignments.Remove(await db.PlatformRoleAssignments.SingleAsync(item => item.Id == assignment.Id));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public void ConfiguredTestHost_RefusesMockWithoutExplicitOptIn()
    {
        using var app = Application(allowMock: false);
        var error = Assert.Throws<InvalidOperationException>(() => Client(app));
        Assert.Contains("Mock Authentication", error.Message);
    }

    private WebApplicationFactory<Program> Application(bool allowMock = true, bool enabled = false, SensitiveLogTestControl? logs = null)
    {
        using var scope = factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString();
        var values = new Dictionary<string, string?>
        {
            ["Otp:Enabled"] = enabled.ToString(),
            ["Otp:AllowMockAuthentication"] = allowMock.ToString(),
            ["ConnectionStrings:MakanDatabase"] = connectionString,
            ["Identity:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["BackofficeSecurity:KeyRingPath"] = _keyRingPath,
            ["Storage:RootPath"] = factory.StorageRootPath,
            ["Sms:Provider"] = enabled ? "Unavailable" : "MeliPayamakOtp",
            ["Sms:MeliPayamakOtp:ServiceToken"] = string.Empty,
            ["Notifications:PushProvider"] = "Unavailable",
            ["Notifications:SmsProvider"] = "Unavailable",
            ["AllowedHosts"] = "localhost",
            ["Cors:AllowedOrigins:0"] = "http://localhost",
            ["Hosting:RequireHttps"] = "false",
            ["ReverseProxy:Enabled"] = "true",
            ["ReverseProxy:KnownProxies:0"] = "127.0.0.1"
        };
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            foreach (var (key, value) in values) builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureTestServices(services =>
            {
                if (logs is not null)
                {
                    services.AddSingleton(logs);
                    services.AddSingleton<ILoggerProvider, RecordingTestLoggerProvider>();
                }
                // Workerهای نامرتبط اجرا نمی‌شوند؛ DI احراز هویت، SQL، Options و MFA واقعی باقی می‌مانند.
                foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IHostedService) &&
                    item.ImplementationType?.Namespace?.StartsWith("MakanApp.Infrastructure", StringComparison.Ordinal) == true).ToArray())
                    services.Remove(descriptor);
            });
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<int> ChallengeCountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OtpChallenges.CountAsync();
    }

    public void Dispose()
    {
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MakanApp-ServerMockOtpKeys")) + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(_keyRingPath).StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_keyRingPath))
            Directory.Delete(_keyRingPath, recursive: true);
    }
}
