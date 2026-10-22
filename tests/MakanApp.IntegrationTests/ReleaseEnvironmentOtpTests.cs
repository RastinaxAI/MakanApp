using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ReleaseEnvironmentOtpTests(MakanAppWebApplicationFactory factory) : IDisposable
{
    private readonly string _keys = Path.Combine(Path.GetTempPath(), "MakanApp-ReleaseOtpKeys", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Test", false)]
    [InlineData("Production", true)]
    public async Task ExistingAccount_UsesEnvironmentOtpAndNormalSessionWithoutGrantingPermissions(string environment, bool real)
    {
        var phone = real ? "+989120008101" : "+989120008102";
        var user = await AccountAsync(phone);
        var delivery = new ControlledDelivery(real);
        using var app = Application(environment, delivery);
        using var client = Client(app);
        Assert.Equal(environment, app.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
        var flags = app.Services.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value;
        Assert.Equal(real, flags.Enabled);
        Assert.Equal(!real, flags.AllowMockAuthentication);
        Assert.Equal(real, app.Services.GetRequiredService<OtpOptions>().Enabled);

        using var requested = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var challenge = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        Assert.Equal(real ? 1 : 0, delivery.Calls);
        using var verified = await VerifyAsync(client, challenge.ChallengeId, phone, real ? delivery.Code : "arbitrary-valid-format");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var result = (await verified.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
        Assert.Equal(user.Id, result.User.Id);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        // قرارداد واقعی نشست opaque است؛ تبدیل آن به JWT جزو این مرحله نیست.
        Assert.DoesNotContain(".", result.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        using var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var admin = await client.GetAsync("/api/admin/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        using var organization = await client.GetAsync("/api/v1/organization/people");
        Assert.Equal(HttpStatusCode.Forbidden, organization.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionCommand(result.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(real ? 1 : 0, await db.OtpChallenges.CountAsync(row => row.NormalizedPhoneNumber == phone));
        Assert.True(await db.UserSessions.AnyAsync(row => row.UserId == user.Id));
    }

    [Theory]
    [InlineData("Test", "+989120008103")]
    [InlineData("Production", "+989120008104")]
    public async Task SuspendedExistingAccount_IsRejectedEvenWithValidEnvironmentAuthentication(string environment, string phone)
    {
        await AccountAsync(phone, suspended: true);
        var delivery = new ControlledDelivery(environment == "Production");
        using var app = Application(environment, delivery);
        using var client = Client(app);
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var challenge = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        using var verified = await VerifyAsync(client, challenge.ChallengeId, phone, delivery.Code);
        Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
        Assert.Contains(IdentityErrorCodes.AccountSuspended, await verified.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Production_ArbitraryCodeCannotBypassMissingOrPersistedChallenge_AndAttemptsAreLimited()
    {
        const string phone = "+989120008105";
        var user = await AccountAsync(phone);
        var delivery = new ControlledDelivery(true);
        using var app = Application("Production", delivery);
        using var client = Client(app);
        using var missing = await VerifyAsync(client, Guid.Empty, phone, "arbitrary");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var id = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!.ChallengeId;
        var maximum = app.Services.GetRequiredService<OtpOptions>().MaxFailedAttempts;
        for (var attempt = 1; attempt <= maximum; attempt++)
        {
            using var rejected = await VerifyAsync(client, id, phone, "wrong-value");
            Assert.Equal(attempt < maximum ? HttpStatusCode.BadRequest : HttpStatusCode.TooManyRequests, rejected.StatusCode);
        }
        using var locked = await VerifyAsync(client, id, phone, delivery.Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var row = await db.OtpChallenges.SingleAsync(item => item.Id == id);
        Assert.Equal(maximum, row.FailedAttempts);
        Assert.NotEmpty(row.Salt);
        Assert.NotEmpty(row.CodeHash);
        Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == user.Id));
        Assert.Equal(1, delivery.Calls);
    }

    [Fact]
    public async Task Production_ExpiredChallengeIsRejected()
    {
        const string phone = "+989120008106";
        await AccountAsync(phone);
        var delivery = new ControlledDelivery(true);
        using var app = Application("Production", delivery);
        using var client = Client(app);
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        var id = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>())!.ChallengeId;
        await factory.ExpireChallengeAsync(id);
        using var expired = await VerifyAsync(client, id, phone, delivery.Code);
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Contains(IdentityErrorCodes.OtpExpired, await expired.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Production_RejectsMockPermissionAtActualHostStartup(bool enabled)
    {
        using var app = Application("Production", new ControlledDelivery(false), new Dictionary<string, string?>
        {
            ["Otp:Enabled"] = enabled.ToString(),
            ["Otp:AllowMockAuthentication"] = "true"
        });
        var failure = Assert.Throws<InvalidOperationException>(() => Client(app));
        Assert.Contains("Mock Authentication", failure.Message);
    }

    private WebApplicationFactory<Program> Application(string environment, ControlledDelivery delivery,
        Dictionary<string, string?>? overrides = null)
    {
        var source = SourceDirectory();
        using var settings = new ConfigurationManager();
        settings.SetBasePath(source).AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environment}.json.example");
        var values = settings.AsEnumerable().Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value);
        using var scope = factory.Services.CreateScope();
        var sql = new SqlConnectionStringBuilder(scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString())
        {
            Encrypt = true, TrustServerCertificate = true
        };
        values["ConnectionStrings:MakanDatabase"] = sql.ConnectionString;
        // فقط LocalDB ایزولهٔ تست گواهی سرور معتبر ندارد؛ کنترل Production حذف نمی‌شود.
        values["Database:AllowUntrustedServerCertificate"] = "true";
        values["Identity:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        values["Storage:RootPath"] = factory.StorageRootPath;
        values["Security:DataProtection:KeyRingPath"] = _keys;
        values["BackofficeSecurity:KeyRingPath"] = _keys;
        values["Sms:MeliPayamakOtp:ServiceToken"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        values["AllowedHosts"] = "localhost";
        if (overrides is not null)
            foreach (var (key, value) in overrides) values[key] = value;
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            foreach (var (key, value) in values) builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureTestServices(services =>
            {
                // فقط مرز ارسال خارجی جایگزین می‌شود؛ Options، use case، SQL و نشست واقعی باقی می‌مانند.
                Assert.Equal(environment == "Production", services.Any(item => item.ServiceType == typeof(MeliPayamakOtpOptions)));
                services.RemoveAll<IOtpDeliveryProvider>();
                services.AddSingleton<IOtpDeliveryProvider>(delivery);
                foreach (var item in services.Where(item => item.ServiceType == typeof(IHostedService) &&
                    item.ImplementationType?.Namespace?.StartsWith("MakanApp.Infrastructure", StringComparison.Ordinal) == true).ToArray())
                    services.Remove(item);
            });
        });
    }

    private async Task<User> AccountAsync(string phone, bool suspended = false)
    {
        var user = User.Create(DateTime.UtcNow);
        if (suspended) user.Suspend(DateTime.UtcNow);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        db.Users.Add(user);
        db.UserCredentials.Add(UserCredential.CreateMobile(user.Id, phone, DateTime.UtcNow));
        await db.SaveChangesAsync();
        return user;
    }

    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, Guid id, string phone, string code) =>
        client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(id, phone, code));

    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد."), "MakanApp.Api");
    }

    public void Dispose()
    {
        if (Directory.Exists(_keys)) Directory.Delete(_keys, recursive: true);
    }

    private sealed class ControlledDelivery(bool real) : IOtpDeliveryProvider
    {
        public string Code { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        public int Calls { get; private set; }
        public Task<OtpDeliveryResult> RequestOtpAsync(string phone, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (!real) throw new InvalidOperationException("مسیر Mock نباید provider را فراخوانی کند.");
            return Task.FromResult(new OtpDeliveryResult(Code, "ReleaseTestDouble"));
        }
    }
}
