using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Infrastructure.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class HmacAiServiceCredentialProviderTests
{
    private const string Key = "synthetic-test-key-with-at-least-32-bytes";
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetCredential_CreatesShortLivedSignedServiceTokenWithoutUserContext()
    {
        var provider = new HmacAiServiceCredentialProvider(
            CreateOptions(),
            new FixedTimeProvider(Now));

        var credential = await provider.GetCredentialAsync(CancellationToken.None);
        var parts = credential.Token.Split('.');
        var header = Decode(parts[0]);
        var payload = Decode(parts[1]);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Key));
        var expected = Base64UrlEncode(hmac.ComputeHash(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}")));

        Assert.Equal("Bearer", credential.Scheme);
        Assert.Equal(3, parts.Length);
        Assert.Equal(expected, parts[2]);
        Assert.Equal("HS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("current", header.RootElement.GetProperty("kid").GetString());
        Assert.Equal("makan-backend", payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal("makan-ai", payload.RootElement.GetProperty("aud").GetString());
        Assert.Equal("makan.ai.invoke", payload.RootElement.GetProperty("scope").GetString());
        Assert.Equal(Now.AddSeconds(120).UtcDateTime, credential.ExpiresAtUtc);
        Assert.False(payload.RootElement.TryGetProperty("userId", out _));
        Assert.False(payload.RootElement.TryGetProperty("organizationId", out _));
        Assert.False(payload.RootElement.TryGetProperty("role", out _));
    }

    [Fact]
    public async Task GetCredential_RejectsWeakKey()
    {
        var options = CreateOptions("short");
        var provider = new HmacAiServiceCredentialProvider(options, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetCredentialAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCredential_HonorsCancellation()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var provider = new HmacAiServiceCredentialProvider(CreateOptions(), new FixedTimeProvider(Now));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await provider.GetCredentialAsync(source.Token));
    }

    private static AiServiceAuthenticationOptions CreateOptions(string key = Key) => new()
    {
        Issuer = "makan-backend",
        Audience = "makan-ai",
        ServiceName = "makan-backend",
        Scope = "makan.ai.invoke",
        CurrentKeyId = "current",
        CurrentKey = key,
        TokenLifetimeSeconds = 120
    };

    private static JsonDocument Decode(string value) =>
        JsonDocument.Parse(Convert.FromBase64String(Pad(value.Replace('-', '+').Replace('_', '/'))));

    private static string Pad(string value) => value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
