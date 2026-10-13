using MakanApp.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class ProductionConfigurationValidationTests
{
    [Fact]
    public void ValidProductionConfigurationIsAccepted()
    {
        var configuration = BuildConfiguration();

        ProductionConfigurationValidator.Validate(configuration, "Production");
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
