using System.Security.Cryptography;
using MakanApp.Application.Identity;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class DevelopmentOtpBypassTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    [InlineData("")]
    public void UnsafeEnvironment_RejectsBypassAtComposition(string environment)
    {
        var services = new ServiceCollection();
        var failure = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(Configuration(), environment));
        Assert.Contains("DevelopmentBypass", failure.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Test")]
    public void ExplicitDevelopmentEnvironments_AcceptValidatedOptions(string environment)
    {
        var options = DevelopmentOtpBypassOptions.Load(Configuration(), environment);
        Assert.Equal(OtpMode.DevelopmentBypass, options.Mode);
        Assert.Single(options.TestUsers);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("AllowAll")]
    [InlineData("")]
    public void UnknownMode_IsRejected(string mode)
    {
        var configuration = Configuration();
        configuration["Identity:Otp:Mode"] = mode;
        Assert.Throws<InvalidOperationException>(() => DevelopmentOtpBypassOptions.Load(configuration, "Development"));
    }

    [Fact]
    public void RealIsTheDefault_WithoutDevelopmentSecrets()
    {
        var options = DevelopmentOtpBypassOptions.Load(new ConfigurationManager(), "Production");
        Assert.Equal(OtpMode.Real, options.Mode);
        Assert.Empty(options.TestUsers);
        Assert.Empty(options.DevelopmentCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad-code")]
    [InlineData("\u001fhidden")]
    public void MissingOrInvalidCode_FailsWithoutDisclosingValue(string code)
    {
        var configuration = Configuration();
        configuration["Identity:Otp:DevelopmentCode"] = code;
        var failure = Assert.Throws<InvalidOperationException>(() =>
            DevelopmentOtpBypassOptions.Load(configuration, "Development"));
        if (code.Length != 0) Assert.DoesNotContain(code, failure.Message);
    }

    [Theory]
    [InlineData("PhoneNumber", "invalid")]
    [InlineData("UserId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("UserId", "invalid")]
    public void InvalidAccountBinding_IsRejected(string field, string value)
    {
        var configuration = Configuration();
        configuration["Identity:Otp:TestUsers:0:" + field] = value;
        Assert.Throws<InvalidOperationException>(() => DevelopmentOtpBypassOptions.Load(configuration, "Testing"));
    }

    [Fact]
    public void EmptyOrDuplicateNormalizedAllowlist_IsRejected()
    {
        var configuration = Configuration();
        configuration["Identity:Otp:TestUsers:1:PhoneNumber"] = "0012025550101";
        configuration["Identity:Otp:TestUsers:1:UserId"] = Guid.NewGuid().ToString();
        Assert.Throws<InvalidOperationException>(() => DevelopmentOtpBypassOptions.Load(configuration, "Testing"));
        configuration = Configuration();
        configuration["Identity:Otp:TestUsers:0:PhoneNumber"] = null;
        configuration["Identity:Otp:TestUsers:0:UserId"] = null;
        Assert.Throws<InvalidOperationException>(() => DevelopmentOtpBypassOptions.Load(configuration, "Testing"));
    }

    [Fact]
    public void LegacyBypassComposition_IsRejectedInsteadOfCompetingWithOtpEnabled()
    {
        var configuration = Configuration();
        configuration["Sms:Provider"] = "MeliPayamakOtp";
        var services = new ServiceCollection().AddLogging();
        var failure = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration, "Development"));
        Assert.Contains("Otp:Enabled", failure.Message);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(MeliPayamakOtpOptions));
    }

    private static ConfigurationManager Configuration()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=MakanApp_DevelopmentTestUsers_UnitOnly;Trusted_Connection=True",
            ["Identity:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Identity:Otp:Mode"] = "DevelopmentBypass",
            ["Identity:Otp:DevelopmentCode"] = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6"),
            ["Identity:Otp:TestUsers:0:PhoneNumber"] = "+12025550101",
            ["Identity:Otp:TestUsers:0:UserId"] = Guid.NewGuid().ToString()
        });
        return configuration;
    }
}
