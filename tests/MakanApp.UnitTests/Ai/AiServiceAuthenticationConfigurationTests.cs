using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Knowledge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiServiceAuthenticationConfigurationTests
{
    [Theory]
    [InlineData("Test")]
    [InlineData("Production")]
    public void RepositoryBaseExplicitlyDisablesBothServiceCredentialsWithoutEnablingRuntime(string environment)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "MakanApp.sln"))) root = root.Parent;
        var path = Path.Combine(root?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد."), "MakanApp.Api", "appsettings.json");
        // فقط تنظیم پایهٔ عمومی خوانده می‌شود؛ هیچ فایل خصوصی یا credential محیط به آزمون وارد نمی‌شود.
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiService:Enabled"] = json.RootElement.GetProperty("AiService").GetProperty("Enabled").ToString(),
            ["AiService:UseServiceAuthentication"] = json.RootElement.GetProperty("AiService").GetProperty("UseServiceAuthentication").ToString(),
            ["AiKnowledge:UseServiceAuthentication"] = json.RootElement.GetProperty("AiKnowledge").GetProperty("UseServiceAuthentication").ToString(),
            ["Otp:Enabled"] = "true", ["Otp:AllowMockAuthentication"] = "false",
            ["Identity:SecurityKey"] = Guid.NewGuid().ToString("N"),
            ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=OptionsOnly;Trusted_Connection=True"
        }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddInfrastructure(config, environment);
        using var provider = services.BuildServiceProvider();
        var business = provider.GetRequiredService<AiServiceOptions>();
        var knowledge = provider.GetRequiredService<IOptions<AiKnowledgeOptions>>().Value;
        Assert.False(business.UseServiceAuthentication); Assert.False(knowledge.UseServiceAuthentication);
        Assert.False(business.Enabled); Assert.False(knowledge.Enabled);
        Assert.Empty(provider.GetRequiredService<AiServiceAuthenticationOptions>().CurrentKey);
    }

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
