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

    [Fact]
    public void ExplicitProductionExceptionsAllowApprovedHttpAndUntrustedCertificate()
    {
        var configuration = BuildConfiguration(ApprovedExceptions());

        ProductionConfigurationValidator.Validate(configuration, "Production");
    }

    [Theory]
    [InlineData("Hosting:AllowInsecureHttp")]
    [InlineData("Database:AllowUntrustedServerCertificate")]
    public void EachExceptionRequiresItsOwnExplicitOptIn(string key)
    {
        var values = ApprovedExceptions();
        values[key] = "false";

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(BuildConfiguration(values), "Production"));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("staging")]
    public void ProductionExceptionsCannotWeakenStaging(string environmentName)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(BuildConfiguration(ApprovedExceptions()), environmentName));
    }

    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("Cors:AllowedOrigins:0", "http://*.example.test")]
    [InlineData("Cors:AllowedOrigins:0", "ftp://app.example.test")]
    [InlineData("Cors:AllowedOrigins:0", "http://app.example.test/path")]
    [InlineData("Cors:AllowedOrigins:0", "http://user:secret-marker@app.example.test")]
    [InlineData("Cors:AllowedOrigins:0", "http://app.example.test?token=secret-marker")]
    [InlineData("Cors:AllowedOrigins:0", "http://app.example.test#secret-marker")]
    [InlineData("Cors:AllowedOrigins:0", "")]
    [InlineData("ReverseProxy:KnownProxies:0", "any-proxy")]
    [InlineData("ReverseProxy:Enabled", "false")]
    [InlineData("Storage:RootPath", "relative/path")]
    [InlineData("Identity:SecurityKey", "secret-marker")]
    [InlineData("ConnectionStrings:MakanDatabase", "Server=sql.internal;Database=Tests;Integrated Security=True;Encrypt=False;TrustServerCertificate=True")]
    public void ApprovedExceptionsDoNotBypassOtherProductionGuards(string key, string value)
    {
        var values = ApprovedExceptions();
        values[key] = value;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(BuildConfiguration(values), "Production"));

        Assert.DoesNotContain("secret-marker", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpWithoutExplicitExceptionRemainsRejected()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Hosting:RequireHttps"] = "false"
        });

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, "Production"));
    }

    private static Dictionary<string, string?> ApprovedExceptions() => new()
    {
        ["Hosting:RequireHttps"] = "false",
        ["Hosting:AllowInsecureHttp"] = "true",
        ["Database:AllowUntrustedServerCertificate"] = "true",
        ["Cors:AllowedOrigins:0"] = "http://app.example.test",
        ["ConnectionStrings:MakanDatabase"] =
            "Server=sql.internal;Database=Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    };

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
