using System.Security.Cryptography;
using MakanApp.Application.Notification;
using Xunit;

namespace MakanApp.UnitTests.Notification;

public sealed class WebPushSubscriptionValidationTests
{
    [Theory]
    [InlineData("http://push.example.test/token")]
    [InlineData("https://push.example.test:444/token")]
    [InlineData("https://push.example.test.attacker.test/token")]
    [InlineData("https://user:secret@push.example.test/token")]
    [InlineData("https://push.example.test/token#fragment")]
    [InlineData("https://127.0.0.1/token")]
    [InlineData("https://[::1]/token")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://push.example.test/token with space")]
    public void EndpointMustBeExactApprovedHttpsOriginWithoutCredentials(string endpoint) =>
        Assert.Throws<ArgumentException>(() => WebPushSubscriptionValidation.ValidateEndpoint(endpoint, ["push.example.test"]));

    [Fact]
    public void PublicPointAndAuthKeyAreCanonicalAndInvalidCurvePointIsRejected()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var publicBytes = new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray();
        var auth = RandomNumberGenerator.GetBytes(16);
        var normalized = WebPushSubscriptionValidation.ValidateKeys(
            Convert.ToBase64String(publicBytes).Replace('+', '-').Replace('/', '_'),
            Convert.ToBase64String(auth).Replace('+', '-').Replace('/', '_'));
        Assert.Equal(87, normalized.P256dh.Length);
        Assert.Equal(22, normalized.Auth.Length);
        publicBytes.AsSpan(1).Clear();
        var invalidPoint = Record.Exception(() => WebPushSubscriptionValidation.ValidateKeys(
            Convert.ToBase64String(publicBytes).Replace('+', '-').Replace('/', '_'), normalized.Auth));
        Assert.True(invalidPoint is ArgumentException or CryptographicException, invalidPoint?.GetType().FullName ?? "No exception");
        Assert.Throws<ArgumentException>(() => WebPushSubscriptionValidation.ValidateKeys("short", normalized.Auth));
    }
}
