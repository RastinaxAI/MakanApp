using System.Security.Cryptography;
using MakanApp.Application.Identity;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class ConfigurableOtpOptionsTests
{
    [Theory]
    [InlineData("Production", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", false)]
    [InlineData("Staging", true)]
    [InlineData("QA", true)]
    [InlineData("", true)]
    public void DisabledOtp_RejectsNonTestEnvironmentAtComposition(string environment, bool allowMock)
    {
        var configuration = Configuration(false, allowMock);
        var failure = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration, environment));
        Assert.Contains("Otp:Enabled", failure.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Testing")]
    public void DisabledOtp_RequiresIndependentTestOptIn(string environment)
    {
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(Configuration(false, false), environment));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Testing")]
    public void ExplicitTestOptIn_BindsOptionsAndNeverRegistersSmsAdapter(string environment)
    {
        var configuration = Configuration(false, true);
        configuration["Sms:Provider"] = "MeliPayamakOtp";
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(configuration, environment);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(MeliPayamakOtpOptions));
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value.Enabled);
        Assert.False(provider.GetRequiredService<OtpOptions>().Enabled);
        Assert.IsType<UnavailableOtpDeliveryProvider>(provider.GetRequiredService<IOtpDeliveryProvider>());
    }

    [Fact]
    public void MissingConfiguration_DefaultsToRealOtp()
    {
        var options = OtpAuthenticationOptions.Load(new ConfigurationManager(), "Production");
        Assert.True(options.Enabled);
        Assert.False(options.AllowMockAuthentication);
    }

    [Fact]
    public void RealOtp_PreservesExistingDevelopmentDeliveryAdapter()
    {
        var configuration = Configuration(true, false);
        configuration["Sms:Provider"] = "Development";
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(configuration, "Development");
        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<OtpOptions>().Enabled);
        Assert.IsType<DevelopmentOtpProvider>(provider.GetRequiredService<IOtpDeliveryProvider>());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Production_RejectsMockPermissionEvenIfOtpIsEnabled(string environment) =>
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(Configuration(true, true), environment));

    [Theory]
    [InlineData("Identity:Otp:Mode", "DevelopmentBypass")]
    [InlineData("DevelopmentBypassEnabled", "true")]
    [InlineData("Identity:Otp:DevelopmentBypassEnabled", "true")]
    [InlineData("Otp:DevelopmentBypassEnabled", "true")]
    public void LegacyActivationCannotOverrideTheSingleFlag(string key, string value)
    {
        var configuration = Configuration(false, true);
        configuration[key] = value;
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(configuration, "Development"));
    }

    [Fact]
    public void OptionsPattern_RejectsUnsafePostConfiguration()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(Configuration(true, false), "Production");
        services.PostConfigure<OtpAuthenticationOptions>(options => options.AllowMockAuthentication = true);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value);
    }

    [Fact]
    public void InvalidBooleanFailsClosed() =>
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Otp:Enabled"] = "not-a-boolean" }).Build(),
            "Development"));

    private static ConfigurationManager Configuration(bool enabled, bool allowMock)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:Enabled"] = enabled.ToString(),
            ["Otp:AllowMockAuthentication"] = allowMock.ToString(),
            ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=OptionsOnly;Trusted_Connection=True",
            ["Identity:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });
        return configuration;
    }
}
