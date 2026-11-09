using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiServiceAuthenticationConfigurationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void DependencyInjectionHonorsExplicitAuthenticationModeAndPreservesLegacyDefault(string? setting, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiService:Enabled"] = "true",
            ["AiService:BaseUrl"] = "https://ai.example.test",
            ["AiService:UseServiceAuthentication"] = setting,
            ["Otp:Enabled"] = "false",
            ["Otp:AllowMockAuthentication"] = "true",
            ["Identity:SecurityKey"] = Guid.NewGuid().ToString("N"),
            ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=OptionsOnly;Trusted_Connection=True"
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(configuration, "Test");
        using var provider = services.BuildServiceProvider();
        Assert.Equal(expected, provider.GetRequiredService<AiServiceOptions>().UseServiceAuthentication);
        Assert.Empty(provider.GetRequiredService<AiServiceAuthenticationOptions>().CurrentKey);
    }
}
