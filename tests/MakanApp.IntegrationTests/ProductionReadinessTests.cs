using System.Net;
using MakanApp.Api.Hosting;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductionReadinessTests(MakanAppWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqlUnavailableMakesOnlyReadinessUnhealthyAndDoesNotDiscloseDetails(bool suppliedBearer)
    {
        using var unavailable = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<MakanDbContext>();
            services.RemoveAll<DbContextOptions<MakanDbContext>>();
            services.AddDbContext<MakanDbContext>(options => options.UseSqlServer(
                "Server=tcp:127.0.0.1,1;Database=MakanApp_PH2_Unreachable_Test;Integrated Security=True;Connect Timeout=1;Encrypt=True;TrustServerCertificate=False"));
        }));
        using var client = unavailable.CreateClient();
        if (suppliedBearer)
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-ph2-session-marker");
        }
        using var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("Unhealthy", await readiness.Content.ReadAsStringAsync());
        using var live = await client.GetAsync("/health/live");
        using var compatible = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compatible.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OptionalProviderActivationFailuresCannotGateCoreReadiness()
    {
        using var optionalFailure = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiServiceClient>();
            services.RemoveAll<IPushNotificationProvider>();
            services.RemoveAll<IPaymentGateway>();
            services.AddScoped<IAiServiceClient>(_ => throw new InvalidOperationException("synthetic-ai-unavailable"));
            services.AddScoped<IPushNotificationProvider>(_ => throw new InvalidOperationException("synthetic-push-unavailable"));
            services.AddScoped<IPaymentGateway>(_ => throw new InvalidOperationException("synthetic-payment-unavailable"));
        }));
        using var client = optionalFailure.CreateClient();
        using var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Equal("Healthy", await readiness.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageFailureIsMinimalWhetherProbeReturnsFalseOrThrows(bool throws)
    {
        using var storageFailure = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStorageReadinessProbe>();
            services.AddSingleton<IStorageReadinessProbe>(new UnavailableStorageProbe(throws));
        }));
        using var client = storageFailure.CreateClient();
        using var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("Unhealthy", await readiness.Content.ReadAsStringAsync());
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.DoesNotContain("private-storage-marker", string.Join('\n',
            storageFailure.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostKeyRingMakesOnlyReadinessUnhealthyWithoutRecreatingDirectory()
    {
        var ringPath = Path.Combine(Path.GetTempPath(), "MakanApp-PH2-Keys-" + Guid.NewGuid().ToString("N"));
        using var persistent = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Security:DataProtection:KeyRingPath", ringPath);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Security:DataProtection:KeyRingPath"] = ringPath }));
        });
        using var client = persistent.CreateClient();
        using var before = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Directory.Move(ringPath, ringPath + "-retained");
        using var after = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, after.StatusCode);
        Assert.Equal("Unhealthy", await after.Content.ReadAsStringAsync());
        Assert.False(Directory.Exists(ringPath));
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task InvalidRequiredProductionConfigurationIsUnhealthyWithoutAnExceptionPayload()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var check = new CoreReadinessHealthCheck(
            scope.ServiceProvider.GetRequiredService<MakanDbContext>(),
            scope.ServiceProvider.GetRequiredService<IStorageReadinessProbe>(),
            scope.ServiceProvider.GetRequiredService<DataProtectionKeyRingProbe>(),
            new ConfigurationBuilder().Build(), new ProductionEnvironment());
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
        Assert.Empty(result.Data);
    }

    private sealed class UnavailableStorageProbe(bool throws) : IStorageReadinessProbe
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throws) throw new IOException("private-storage-marker/secret-file-path");
            return Task.FromResult(false);
        }
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "MakanApp.PH2.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
