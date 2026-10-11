using System.Net;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductionHostingTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task SwaggerAndOpenApiAreAvailableInProduction()
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        Assert.Equal("Production", production.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

        using var swagger = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("Swagger UI", await swagger.Content.ReadAsStringAsync());

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/files", out _));
    }

    [Theory]
    [InlineData("https://arbitrary.example", "/api/v1/files")]
    [InlineData("http://localhost:5173", "/api/v1/files")]
    [InlineData("https://arbitrary.example", "/hubs/messaging/negotiate")]
    public async Task PreflightAllowsAnyOriginAndBearerHeaders(string origin, string path)
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-client-version");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("authorization", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content-type", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x-client-version", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("/api/v1/files")]
    [InlineData("/hubs/messaging/negotiate?negotiateVersion=1")]
    public async Task CorsDoesNotBypassAuthentication(string path)
    {
        using var production = CreateProductionFactory();
        using var client = CreateClient(production);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", "https://arbitrary.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
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
    public void UnconfiguredProductionStorageRemainsUnavailable()
    {
        using var production = CreateProductionFactory(string.Empty);

        Assert.IsType<UnavailableFileStorage>(production.Services.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void ProductionStorageRejectsRelativeRootPath()
    {
        using var production = CreateProductionFactory("relative-storage");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            production.Services.GetRequiredService<IFileStorage>());

        Assert.Contains("Storage:RootPath", exception.Message);
    }

    private WebApplicationFactory<Program> CreateProductionFactory(string? rootPath = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Identity:SecurityKey", new string('P', 32));
            builder.UseSetting("Sms:Provider", "Unavailable");
            builder.UseSetting("Storage:RootPath", rootPath ?? factory.StorageRootPath);
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> production) =>
        production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
}
