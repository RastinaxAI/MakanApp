using MakanApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OtpConfigurationPrecedenceTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void DefaultWebApplicationConfiguration_UsesEnvironmentOverridesRatherThanJustTheEditedJson(bool enabled, bool allowMock)
    {
        var previousEnabled = Environment.GetEnvironmentVariable("Otp__Enabled");
        var previousAllowMock = Environment.GetEnvironmentVariable("Otp__AllowMockAuthentication");
        try
        {
            Environment.SetEnvironmentVariable("Otp__Enabled", enabled.ToString());
            Environment.SetEnvironmentVariable("Otp__AllowMockAuthentication", allowMock.ToString());
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Test",
                ContentRootPath = ApiDirectory()
            });
            var options = OtpAuthenticationOptions.Load(builder.Configuration, builder.Environment.EnvironmentName);
            Assert.Equal(enabled, options.Enabled);
            Assert.Equal(allowMock, options.AllowMockAuthentication);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Otp__Enabled", previousEnabled);
            Environment.SetEnvironmentVariable("Otp__AllowMockAuthentication", previousAllowMock);
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ProductionJson_RejectsExplicitMockPermissionAtStartup(string environment)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(ApiDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json.example").Build();
        Assert.True(configuration.GetValue<bool>("Otp:Enabled"));
        Assert.False(configuration.GetValue<bool>("Otp:AllowMockAuthentication"));
        Assert.True(OtpAuthenticationOptions.Load(configuration, environment).Enabled);
        configuration["Otp:AllowMockAuthentication"] = "true";
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(configuration, environment));
    }

    private static string ApiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد."), "MakanApp.Api");
    }
}
