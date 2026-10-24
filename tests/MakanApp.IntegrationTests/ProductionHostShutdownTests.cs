using System.Xml.Linq;
using MakanApp.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class ProductionHostShutdownTests
{
    [Theory]
    [InlineData(null, 30)]
    [InlineData("5", 5)]
    [InlineData("30", 30)]
    [InlineData("90", 90)]
    public void RealGenericHostResolvesDefaultAndExplicitBoundedTimeout(string? value, int expected)
    {
        using var host = BuildHost(new Dictionary<string, string?> { [HostShutdownConfiguration.ConfigurationKey] = value });
        var options = host.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(expected), options.ShutdownTimeout);
        Assert.Equal(BackgroundServiceExceptionBehavior.StopHost, options.BackgroundServiceExceptionBehavior);
        Assert.False(options.ServicesStopConcurrently);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("60", 60)]
    [InlineData("90", 90)]
    public void MissingNewKeyPreservesLegacyGenericHostTimeout(string value, int expected)
    {
        using var host = BuildHost(new Dictionary<string, string?> { ["shutdownTimeoutSeconds"] = value });
        Assert.Equal(TimeSpan.FromSeconds(expected), host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Fact]
    public void MissingNewKeyPreservesOtherHostOptionsConfiguration()
    {
        using var host = BuildHost([], options => options.ShutdownTimeout = TimeSpan.FromSeconds(45));
        Assert.Equal(TimeSpan.FromSeconds(45), host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Fact]
    public void ExplicitKeyWinsOverLegacyAndLaterConfigureDelegates()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            [HostShutdownConfiguration.ConfigurationKey] = "5", ["shutdownTimeoutSeconds"] = "60"
        }, options => options.ShutdownTimeout = TimeSpan.FromSeconds(80));
        Assert.Equal(TimeSpan.FromSeconds(5), host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("4")]
    [InlineData("91")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("5.5")]
    [InlineData("2147483648")]
    [InlineData("synthetic-private-timeout-marker")]
    public void InvalidExplicitValueFailsWithoutEchoingIt(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = BuildHost(new Dictionary<string, string?> { [HostShutdownConfiguration.ConfigurationKey] = value });
        });
        Assert.Contains(HostShutdownConfiguration.ConfigurationKey, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-private-timeout-marker", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("91")]
    public void OutOfBoundsResolvedLegacyValueCannotBypassHostValidation(string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = BuildHost(new Dictionary<string, string?> { ["shutdownTimeoutSeconds"] = value });
        });
    }

    [Theory]
    [InlineData(4)]
    [InlineData(91)]
    [InlineData(-1)]
    public void OutOfBoundsProgrammaticHostTimeoutIsValidatedAfterAllConfigureDelegates(int seconds)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = BuildHost([], options => options.ShutdownTimeout = TimeSpan.FromSeconds(seconds));
        });
    }

    [Fact]
    public void MalformedLegacySettingRetainsFrameworkDefaultRatherThanInventingANewParser()
    {
        using var host = BuildHost(new Dictionary<string, string?> { ["shutdownTimeoutSeconds"] = "synthetic-private-legacy-marker" });
        Assert.Equal(TimeSpan.FromSeconds(30), host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void StandaloneProductionValidationUsesTheSameSafeExplicitSettingGuard(string environment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HostShutdownConfiguration.ConfigurationKey] = "synthetic-private-timeout-marker"
        }).Build();
        var exception = Assert.Throws<InvalidOperationException>(() => ProductionConfigurationValidator.Validate(configuration, environment));
        Assert.Contains(HostShutdownConfiguration.ConfigurationKey, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-private-timeout-marker", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IisTemplatePreservesPrivateConfigurationBoundaryAndDisablesStdoutWithLargerAncmBudget()
    {
        var root = FindRepositoryRoot();
        var xml = XDocument.Load(Path.Combine(root, "MakanApp.Api", "web.config"));
        var location = xml.Root!.Element("location")!;
        Assert.Equal("false", location.Attribute("inheritInChildApplications")!.Value);
        var server = location.Element("system.webServer")!;
        var module = server.Element("handlers")!.Elements("add").Single();
        Assert.Equal("AspNetCoreModuleV2", module.Attribute("modules")!.Value);
        var aspNetCore = server.Element("aspNetCore")!;
        Assert.Equal("120", aspNetCore.Attribute("shutdownTimeLimit")!.Value);
        Assert.Equal("inprocess", aspNetCore.Attribute("hostingModel")!.Value);
        Assert.Equal("false", aspNetCore.Attribute("stdoutLogEnabled")!.Value);
        Assert.Equal("dotnet", aspNetCore.Attribute("processPath")!.Value);
        Assert.Equal(".\\MakanApp.Api.dll", aspNetCore.Attribute("arguments")!.Value);
        var environment = aspNetCore.Element("environmentVariables")!.Elements("environmentVariable")
            .ToDictionary(item => item.Attribute("name")!.Value, item => item.Attribute("value")!.Value);
        Assert.Single(environment);
        Assert.Equal("Production", environment["ASPNETCORE_ENVIRONMENT"]);
        Assert.DoesNotContain("BackofficeSecurity__KeyRingPath", environment.Keys);
        Assert.Null(server.Element("applicationInitialization"));
    }

    private static IHost BuildHost(Dictionary<string, string?> values, Action<HostOptions>? configure = null)
    {
        // Host واقعی بدون environment/JSON محیط اجرا؛ هیچ SQL، worker یا IIS در این تست لازم نیست.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true, EnvironmentName = Environments.Production, ApplicationName = "MakanApp.PH5.Tests"
        });
        builder.Configuration.AddInMemoryCollection(values);
        HostShutdownConfiguration.ConfigureServices(builder.Services, builder.Configuration);
        if (configure is not null) builder.Services.Configure(configure);
        return builder.Build();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MakanApp.sln"))) return directory.FullName;
        throw new InvalidOperationException("ریشهٔ source برای بازبینی template تست پیدا نشد.");
    }
}
