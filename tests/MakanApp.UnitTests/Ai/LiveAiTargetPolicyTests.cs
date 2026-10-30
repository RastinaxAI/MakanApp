using MakanApp.AiIntegrationProbe;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class LiveAiTargetPolicyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:8011")]
    [InlineData("https://localhost:443")]
    [InlineData("http://[::1]:8011")]
    public void Loopback_IsAllowedWithoutRemoteOptIn(string target) =>
        Assert.True(LiveAiTargetPolicy.Validate(target, false, null).IsLoopback);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("file:///tmp/ai")]
    [InlineData("https://ai.example.test/private")]
    [InlineData("https://user:password@ai.example.test")]
    [InlineData("https://ai.example.test?key=synthetic")]
    [InlineData("https://ai.example.test#fragment")]
    public void UnsafeOrMissingTarget_IsRejected(string? target) =>
        Assert.Throws<InvalidOperationException>(() =>
            LiveAiTargetPolicy.Validate(target, true, "https://ai.example.test"));

    [Theory]
    [InlineData(false, "https://ai.example.test")]
    [InlineData(true, null)]
    [InlineData(true, "https://other.example.test")]
    [InlineData(true, "https://ai.example.test:8443")]
    [InlineData(true, "https://ai.example.test/path")]
    [InlineData(true, "http://ai.example.test")]
    [InlineData(true, "https://secret@ai.example.test")]
    public void Remote_RequiresExplicitFlagAndExactApprovedOrigin(bool optIn, string? approved) =>
        Assert.Throws<InvalidOperationException>(() =>
            LiveAiTargetPolicy.Validate("https://ai.example.test", optIn, approved));

    [Fact]
    public void ApprovedRemoteHttps_IsAccepted() =>
        Assert.Equal("https://ai.example.test/",
            LiveAiTargetPolicy.Validate("https://ai.example.test", true, "https://ai.example.test/").AbsoluteUri);

    [Fact]
    public void RemoteHttp_IsRejectedEvenWhenApproved() =>
        Assert.Throws<InvalidOperationException>(() =>
            LiveAiTargetPolicy.Validate("http://ai.example.test", true, "http://ai.example.test"));

    [Fact]
    public void ApprovedWireGuardRequiresExplicitFlagAndBackendInterface()
    {
        Assert.Equal("http://10.77.0.2:8001/", LiveAiTargetPolicy.Validate("http://10.77.0.2:8001", false,
            "http://10.77.0.2:8001", true, true).AbsoluteUri);
        Assert.Throws<InvalidOperationException>(() => LiveAiTargetPolicy.Validate("http://10.77.0.2:8001", false, "http://10.77.0.2:8001", true, false));
        Assert.Throws<InvalidOperationException>(() => LiveAiTargetPolicy.Validate("http://10.77.0.2:8001", false, "http://10.77.0.2:8001"));
    }
    [Theory]
    [InlineData("http://10.77.0.3:8001", "http://10.77.0.3:8001")]
    [InlineData("http://10.77.0.2:8002", "http://10.77.0.2:8002")]
    [InlineData("http://10.77.0.2:8001", "http://10.77.0.3:8001")]
    public void WireGuardOptInCannotPermitArbitraryRemoteHttp(string target, string approved) =>
        Assert.Throws<InvalidOperationException>(() => LiveAiTargetPolicy.Validate(target, false, approved, true, true));
}
