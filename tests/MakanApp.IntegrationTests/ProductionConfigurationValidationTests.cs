using MakanApp.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class ProductionConfigurationValidationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("staging")]
    public void ValidProductionLikeConfigurationIsAccepted(string environmentName)
    {
        var configuration = BuildConfiguration();

        ProductionConfigurationValidator.Validate(configuration, environmentName);
    }

    [Theory]
    [InlineData("Cors:AllowedOrigins:0", "*")]
    [InlineData("Cors:AllowedOrigins:0", "http://app.example.test")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("ReverseProxy:KnownProxies:0", "any-proxy")]
    [InlineData("Storage:RootPath", "relative/path")]
    public void UnsafeProductionConfigurationIsRejected(string key, string value)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?> { [key] = value });

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, "Production"));
    }

    [Fact]
    public void UntrustedSqlCertificateConfigurationIsRejected()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MakanDatabase"] =
                "Server=sql.internal;Database=MakanApp;User Id=runtime;Password=placeholder;Encrypt=True;TrustServerCertificate=True"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, "Production"));

        Assert.DoesNotContain("placeholder", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ConnectionStrings:MakanDatabase", "")]
    [InlineData("ConnectionStrings:MakanDatabase", "Password=secret-marker;not-sql")]
    [InlineData("ConnectionStrings:MakanDatabase", "Server=sql.internal;Database=Staging;Integrated Security=True;Encrypt=False")]
    [InlineData("ConnectionStrings:MakanDatabase", "Server=sql.internal;Database=Staging;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Identity:SecurityKey", "secret-marker")]
    [InlineData("Storage:RootPath", "relative/storage")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("Cors:AllowedOrigins:0", "http://app.example.test")]
    [InlineData("ReverseProxy:Enabled", "false")]
    [InlineData("ReverseProxy:KnownProxies:0", "untrusted")]
    [InlineData("Hosting:RequireHttps", "false")]
    public void UnsafeStagingConfigurationFailsWithoutPrintingSecret(string key, string value)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?> { [key] = value });
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, "Staging"));
        Assert.DoesNotContain("secret-marker", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void DevelopmentLikeEnvironmentsRetainExistingConfigurationBehavior(string environmentName) =>
        ProductionConfigurationValidator.Validate(new ConfigurationBuilder().Build(), environmentName);

    private static IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MakanDatabase"] =
                "Server=sql.internal;Database=MakanApp;Integrated Security=True;Encrypt=True;TrustServerCertificate=False",
            ["Identity:SecurityKey"] = new string('S', 32),
            ["Storage:RootPath"] = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "makan-production-storage")),
            ["AllowedHosts"] = "api.example.test",
            ["Cors:AllowedOrigins:0"] = "https://app.example.test",
            ["Hosting:RequireHttps"] = "true",
            ["ReverseProxy:Enabled"] = "true",
            ["ReverseProxy:KnownProxies:0"] = "127.0.0.1"
        };
        if (overrides is not null)
        {
            foreach (var item in overrides)
            {
                values[item.Key] = item.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
