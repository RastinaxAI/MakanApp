using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using MakanApp.Application.Identity;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class ServerMockOtpConfigurationTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json.example")]
    public void VersionedSettings_ContainExplicitBooleanOtpKeys(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), fileName)));
        var otp = document.RootElement.GetProperty("Otp");
        Assert.Equal(JsonValueKind.False, otp.GetProperty("Enabled").ValueKind);
        Assert.Equal(JsonValueKind.True, otp.GetProperty("AllowMockAuthentication").ValueKind);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Testing")]
    public void ConflictingFlags_AreRejectedEvenInTestEnvironments(string environment)
    {
        var configuration = Configuration();
        configuration["Otp:Enabled"] = "true";
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(configuration, environment));
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("test")]
    public void ServerTestSettings_EnableMockWithoutRegisteringSms(string environment)
    {
        var configuration = Configuration();
        configuration["Sms:Provider"] = "MeliPayamakOtp";
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(configuration, environment);

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(MeliPayamakOtpOptions));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value;
        Assert.False(options.Enabled);
        Assert.True(options.AllowMockAuthentication);
        Assert.False(provider.GetRequiredService<OtpOptions>().Enabled);
        Assert.IsType<UnavailableOtpDeliveryProvider>(provider.GetRequiredService<IOtpDeliveryProvider>());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ServerTestSettings_DoNotAuthorizeMockInProductionLikeEnvironments(string environment) =>
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(Configuration(), environment));

    [Fact]
    public void ServerTestSettings_StillRequireTheIndependentGuard()
    {
        var configuration = Configuration();
        configuration["Otp:AllowMockAuthentication"] = "false";
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(configuration, "Test"));
    }

    [Fact]
    public void ExplicitOverrides_CanRestoreRealOtpWithoutChangingApiContracts()
    {
        var configuration = Configuration();
        configuration["Otp:Enabled"] = "true";
        configuration["Otp:AllowMockAuthentication"] = "false";
        var options = OtpAuthenticationOptions.Load(configuration, "Production");
        Assert.True(options.Enabled);
        Assert.False(options.AllowMockAuthentication);
    }

    [Fact]
    public void TestPublishProfile_SelectsTestAndExcludesPrivateProductionSettings()
    {
        var profile = XDocument.Load(Path.Combine(ApiDirectory(), "Properties", "PublishProfiles", "TestIis.pubxml"));
        Assert.Equal("Test", Assert.Single(profile.Descendants("EnvironmentName")).Value);
        var exclusion = Assert.Single(profile.Descendants("Content"));
        Assert.Equal("Never", exclusion.Attribute("CopyToPublishDirectory")?.Value);
        Assert.Equal("appsettings.Production.json;appsettings.Development.json", exclusion.Attribute("Update")?.Value);
    }

    private static IConfigurationRoot Configuration()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), "appsettings.Test.json")));
        var otp = document.RootElement.GetProperty("Otp");
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:Enabled"] = otp.GetProperty("Enabled").GetBoolean().ToString(),
            ["Otp:AllowMockAuthentication"] = otp.GetProperty("AllowMockAuthentication").GetBoolean().ToString(),
            ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=OptionsOnly;Trusted_Connection=True",
            ["Identity:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
    }

    private static string ApiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد."), "MakanApp.Api");
    }
}
