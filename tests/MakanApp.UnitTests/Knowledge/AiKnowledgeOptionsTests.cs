using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Knowledge;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.UnitTests.Knowledge;

public sealed class AiKnowledgeOptionsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EitherDisabledGatePreventsKnowledgeTraffic(bool knowledge, bool business)
    {
        var gate = new KnowledgeRuntimeAvailability(Options.Create(new AiKnowledgeOptions { Enabled = knowledge }), new AiServiceOptions { Enabled = business });
        var error = Assert.Throws<AiServiceException>(gate.EnsureEnabled);
        Assert.Equal(AiServiceErrorCodes.FeatureDisabled, error.Code);
    }

    [Fact]
    public void BothExplicitGatesAreRequiredAndDoNotSelectPhysicalModelOrVectorDatabase()
    {
        new KnowledgeRuntimeAvailability(Options.Create(new AiKnowledgeOptions { Enabled = true }), new AiServiceOptions { Enabled = true }).EnsureEnabled();
        Assert.False(new AiKnowledgeOptions().Enabled);
        Assert.False(new AiKnowledgeOptions().UseServiceAuthentication);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("userinfo")]
    [InlineData("fragment")]
    [InlineData("query")]
    [InlineData("timeout")]
    [InlineData("response")]
    [InlineData("polling")]
    [InlineData("contract")]
    public void EnabledKnowledgeRejectsInvalidConfigurationShape(string invalid)
    {
        var options = new AiKnowledgeOptions { Enabled = true, BaseUrl = "https://knowledge.example.test" };
        switch (invalid)
        {
            case "empty": options.BaseUrl = ""; break;
            case "userinfo": options.BaseUrl = "https://user:password@knowledge.example.test"; break;
            case "fragment": options.BaseUrl += "/#fragment"; break;
            case "query": options.BaseUrl += "/?query=value"; break;
            case "timeout": options.TimeoutSeconds = 0; break;
            case "response": options.MaximumResponseBytes = 100000000; break;
            case "polling": options.StatusPollTimeoutSeconds = 0; break;
            default: options.ContractVersion = "v1"; break;
        }
        Assert.False(AiKnowledgeOptions.IsValid(options));
    }
}
