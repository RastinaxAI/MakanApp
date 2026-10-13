using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Ai;

namespace MakanApp.Infrastructure.Ai;

public sealed class HmacAiServiceCredentialProvider(
    AiServiceAuthenticationOptions options,
    TimeProvider timeProvider) : IAiServiceCredentialProvider
{
    public ValueTask<AiServiceCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(options.TokenLifetimeSeconds);
        var header = SerializeSegment(new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
            ["kid"] = options.CurrentKeyId
        });
        var payload = SerializeSegment(new Dictionary<string, object>
        {
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["sub"] = options.ServiceName,
            ["scope"] = options.Scope,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N")
        });
        var unsignedToken = $"{header}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.CurrentKey));
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes(unsignedToken)));

        return ValueTask.FromResult(new AiServiceCredential(
            "Bearer",
            $"{unsignedToken}.{signature}",
            expiresAt.UtcDateTime));
    }

    private static string SerializeSegment(IReadOnlyDictionary<string, object> value) =>
        Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(value));

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static void ValidateOptions(AiServiceAuthenticationOptions value)
    {
        if (string.IsNullOrWhiteSpace(value.Issuer) ||
            string.IsNullOrWhiteSpace(value.Audience) ||
            string.IsNullOrWhiteSpace(value.ServiceName) ||
            string.IsNullOrWhiteSpace(value.Scope) ||
            string.IsNullOrWhiteSpace(value.CurrentKeyId) ||
            Encoding.UTF8.GetByteCount(value.CurrentKey) < 32 ||
            value.TokenLifetimeSeconds is < 30 or > 300)
        {
            throw new InvalidOperationException(
                "AiService authentication requires non-empty identifiers, a key of at least 32 UTF-8 bytes, and a lifetime between 30 and 300 seconds.");
        }
    }
}
