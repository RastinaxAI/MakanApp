using MakanApp.AiIntegrationProbe;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class LiveAiErrorContractTests
{
    [Theory]
    [InlineData("AI_INVALID_REQUEST")]
    [InlineData("AI_INVALID_CONTEXT")]
    [InlineData("AI_UNSUPPORTED_ROLE")]
    [InlineData("AI_MODEL_UNAVAILABLE")]
    [InlineData("AI_PROVIDER_TIMEOUT")]
    [InlineData("AI_RATE_LIMITED")]
    [InlineData("AI_CONTEXT_TOO_LARGE")]
    [InlineData("AI_RESPONSE_INVALID")]
    [InlineData("AI_SAFETY_BLOCKED")]
    [InlineData("AI_INTERNAL_ERROR")]
    [InlineData("AI_SERVICE_UNAUTHORIZED")]
    [InlineData("AI_SERVICE_FORBIDDEN")]
    public void KnownCode_IsPreservedWithoutReturningSensitiveMessage(string code)
    {
        var result = LiveAiErrorContract.Read("{\"error\":{\"code\":\"" + code + "\",\"message\":\"synthetic-private-sentinel\"}}", "application/json");
        Assert.Equal(code, result.Code);
        Assert.True(result.HasMessage);
        Assert.DoesNotContain("synthetic-private-sentinel", result.ToString());
    }

    [Theory]
    [InlineData("<html>synthetic-secret</html>", "text/html")]
    [InlineData("{invalid", "application/json")]
    [InlineData("[]", "application/json")]
    [InlineData("null", "application/json")]
    [InlineData("{\"error\":[]}", "application/json")]
    [InlineData("{\"error\":{\"code\":123}}", "application/json")]
    [InlineData("{\"error\":{\"code\":\"synthetic-secret\"}}", "application/json")]
    [InlineData("{\"error\":{\"code\":\"AI_SERVICE_UNAUTHORIZED\",\"message\":\"ok\"}}", "text/plain")]
    public void NonContractResponse_IsSafeAndDoesNotThrow(string body, string mediaType)
    {
        var result = LiveAiErrorContract.Read(body, mediaType);
        Assert.Equal("NON_CONTRACT_RESPONSE", result.Code);
        Assert.False(result.HasMessage);
    }

    [Fact]
    public void MissingMessage_DoesNotPassErrorSchema() =>
        Assert.False(LiveAiErrorContract.Read("{\"error\":{\"code\":\"AI_SERVICE_UNAUTHORIZED\"}}", "application/json").HasMessage);
}
