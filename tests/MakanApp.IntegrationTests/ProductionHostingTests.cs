using System.Net;
using System.Text;
using MakanApp.Application.Identity;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductionHostingTests(MakanAppWebApplicationFactory factory)
{
    [Theory]
    [InlineData("Production", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", false)]
    [InlineData("Staging", true)]
    public void UnsafeMockConfiguration_StopsTheActualApiHost(string environmentName, bool allowMock)
    {
        using var production = CreateProductionFactory(environmentName: environmentName, overrides: new Dictionary<string, string?>
        {
            ["Otp:Enabled"] = "false",
            ["Otp:AllowMockAuthentication"] = allowMock.ToString()
        });
        var failure = Assert.Throws<InvalidOperationException>(() => production.CreateClient());
        Assert.Contains("Mock Authentication", failure.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SwaggerAndOpenApiAreDisabledByDefaultInProductionLikeHost(string environmentName)
    {
        using var production = CreateProductionFactory(environmentName: environmentName);
        using var client = CreateClient(production);
        Assert.Equal(environmentName, production.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

        using var swagger = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/files")]
    [InlineData("/hubs/messaging/negotiate")]
    public async Task PreflightAllowsConfiguredOriginAndBearerHeaders(string path)
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "https://app.example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-client-version");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "https://app.example.test",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("authorization", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content-type", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x-client-version", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task PreflightDoesNotGrantAnUnlistedOrigin()
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/files");
        request.Headers.Add("Origin", "https://arbitrary.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/api/v1/files")]
    [InlineData("/hubs/messaging/negotiate?negotiateVersion=1")]
    public async Task CorsDoesNotBypassAuthentication(string path)
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", "https://app.example.test");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "https://app.example.test",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ConfiguredProductionStoragePersistsAcrossHostRestart()
    {
        var rootPath = Path.Combine(factory.StorageRootPath, Guid.NewGuid().ToString("N"));
        const string storageKey = "production/example.txt";
        var bytes = Encoding.UTF8.GetBytes("persistent production storage");

        using (var production = CreateProductionFactory(rootPath))
        {
            var storage = production.Services.GetRequiredService<IFileStorage>();
            Assert.IsType<LocalFileStorage>(storage);
            await using var input = new MemoryStream(bytes);
            await storage.SaveAsync(storageKey, input, 1024, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(rootPath, "production", "example.txt")));
        }

        using var restarted = CreateProductionFactory(rootPath);
        var restoredStorage = restarted.Services.GetRequiredService<IFileStorage>();
        Assert.True(await restoredStorage.ExistsAsync(storageKey, CancellationToken.None));
        await using (var content = await restoredStorage.OpenReadAsync(storageKey, CancellationToken.None))
        {
            using var output = new MemoryStream();
            await content.CopyToAsync(output);
            Assert.Equal(bytes, output.ToArray());
        }

        await restoredStorage.DeleteAsync(storageKey, CancellationToken.None);
        Assert.False(await restoredStorage.ExistsAsync(storageKey, CancellationToken.None));
    }

    [Fact]
    public void UnconfiguredProductionStorageFailsStartupValidation()
    {
        using var production = CreateProductionFactory(string.Empty);

        var exception = Assert.ThrowsAny<Exception>(() =>
            production.Services.GetRequiredService<IFileStorage>());

        Assert.Contains("Storage:RootPath", exception.ToString());
    }

    [Fact]
    public void ProductionStorageRejectsRelativeRootPath()
    {
        using var production = CreateProductionFactory("relative-storage");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            production.Services.GetRequiredService<IFileStorage>());

        Assert.Contains("Storage:RootPath", exception.Message);
    }

    [Fact]
    public async Task ProductionResponsesIncludeSecurityAndCorrelationHeadersWithoutDetails()
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "release-smoke-1");

        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("release-smoke-1", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.DoesNotContain("ConnectionStrings", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.StorageRootPath, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerCanBeExplicitlyEnabledWithoutApplyingApiCspToItsHtml()
    {
        using var production = CreateProductionFactory(overrides: new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" });
        using var client = CreateClient(production);
        using var response = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task ApprovedHttpHostRespondsWithoutRedirectOrHsts()
    {
        using var production = CreateProductionFactory(overrides: HttpOverrides());
        using var client = CreateClient(production, useHttps: false);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData("http://app.example.test", true)]
    [InlineData("http://unlisted.example.test", false)]
    public async Task ApprovedHttpCorsStillRequiresExplicitOrigin(string origin, bool allowed)
    {
        using var production = CreateProductionFactory(overrides: HttpOverrides());
        using var client = CreateClient(production, useHttps: false);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/files");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("/api/v1/files")]
    [InlineData("/hubs/messaging/negotiate?negotiateVersion=1")]
    public async Task ApprovedHttpDoesNotBypassAuthentication(string path)
    {
        using var production = CreateProductionFactory(overrides: HttpOverrides());
        using var client = CreateClient(production, useHttps: false);

        using var response = await client.PostAsync(path, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApprovedHttpStillRejectsUnlistedHost()
    {
        using var production = CreateProductionFactory(overrides: HttpOverrides());
        using var client = CreateClient(production, useHttps: false);

        using var response = await client.GetAsync("http://unlisted.example.test/health/live");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Dictionary<string, string?> HttpOverrides() => new()
    {
        ["Hosting:RequireHttps"] = "false",
        ["Hosting:AllowInsecureHttp"] = "true",
        ["Cors:AllowedOrigins:0"] = "http://app.example.test"
    };

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ProductionLikeHostCannotInheritPrivateDeploymentSecretsOrProviders(string environmentName)
    {
        using var production = CreateProductionFactory(environmentName: environmentName);
        using var scope = production.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<MakanDbContext>();
        var sql = new SqlConnectionStringBuilder(context.Database.GetConnectionString());

        Assert.Equal("(localdb)\\MSSQLLocalDB", sql.DataSource);
        Assert.Equal("MakanApp_ProductionHostingTests", sql.InitialCatalog);
        Assert.True(sql.IntegratedSecurity);
        Assert.Empty(sql.Password);
        Assert.IsType<UnavailableOtpDeliveryProvider>(services.GetRequiredService<IOtpDeliveryProvider>());
        Assert.Equal(factory.StorageRootPath, services.GetRequiredService<StorageOptions>().RootPath);
        // برای جلوگیری از افشای کلید واقعی، failure فقط نتیجهٔ boolean را نمایش می‌دهد.
        Assert.True(production.Services.GetRequiredService<IConfiguration>()["Identity:SecurityKey"] == new string('P', 32));
    }

    private WebApplicationFactory<Program> CreateProductionFactory(
        string? rootPath = null,
        string environmentName = "Production",
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environmentName);
            // کانفیگ خصوصی انتشار نباید دیتابیس یا سرویس پیامک واقعی را وارد تست کند.
            var values = new Dictionary<string, string?>
            {
                ["Otp:Enabled"] = "true",
                ["Otp:AllowMockAuthentication"] = "false",
                ["ConnectionStrings:MakanDatabase"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=MakanApp_ProductionHostingTests;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=False",
                ["Identity:SecurityKey"] = new string('P', 32),
                ["Sms:Provider"] = "Unavailable",
                ["Sms:MeliPayamakOtp:ServiceToken"] = string.Empty,
                ["Storage:RootPath"] = rootPath ?? factory.StorageRootPath,
                ["AllowedHosts"] = "localhost",
                ["Cors:AllowedOrigins:0"] = "https://app.example.test",
                ["Hosting:RequireHttps"] = "true",
                ["Hosting:AllowInsecureHttp"] = "false",
                ["Database:AllowUntrustedServerCertificate"] = "false",
                ["ReverseProxy:Enabled"] = "true",
                ["ReverseProxy:KnownProxies:0"] = "127.0.0.1",
                ["ReverseProxy:KnownProxies:1"] = "::1",
                ["Swagger:Enabled"] = "false"
            };
            if (overrides is not null)
            {
                foreach (var (key, value) in overrides)
                {
                    values[key] = value;
                }
            }
            // UseSetting پیش از کد startup و provider حافظه‌ای هنگام ساخت host اعمال می‌شود.
            foreach (var (key, value) in values)
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> production, bool useHttps = true) =>
        production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(useHttps ? "https://localhost" : "http://localhost"),
            AllowAutoRedirect = false
        });
}
