using System.Net;
using System.Security.Cryptography;

namespace MakanApp.Application.Notification;

public static class WebPushSubscriptionValidation
{
    public static Uri ValidateEndpoint(string endpoint, IReadOnlyCollection<string> allowedHosts)
    {
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 2048 || endpoint.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Port != 443 || uri.IsLoopback || Uri.CheckHostName(uri.IdnHost) != UriHostNameType.Dns ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || IPAddress.TryParse(uri.Host, out _) ||
            !allowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("مقصد subscription مجاز نیست.");
        return uri;
    }

    public static (string P256dh, string Auth) ValidateKeys(string? p256dh, string? auth)
    {
        var publicKey = Decode(p256dh, 65);
        var authKey = Decode(auth, 16);
        if (publicKey[0] != 4) throw new ArgumentException("کلید subscription معتبر نیست.");
        try
        {
            using var key = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] }
            });
            // بعضی providerهای سیستم‌عامل import را تنبل انجام می‌دهند؛ یک agreement واقعی نقطه را نیز می‌سنجد.
            using var verifier = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var verificationMaterial = verifier.DeriveKeyMaterial(key.PublicKey);
            CryptographicOperations.ZeroMemory(verificationMaterial);
        }
        catch (PlatformNotSupportedException)
        {
            // Windows برای بعضی نقاط نامعتبر همین خطا را می‌دهد؛ ورودی نامعتبر نباید به خطای داخلی HTTP تبدیل شود.
            throw new ArgumentException("نقطه کلید subscription معتبر نیست.");
        }
        return (Encode(publicKey), Encode(authKey));
    }

    private static byte[] Decode(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value.TrimEnd('=').Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("کلید subscription معتبر نیست.");
        var normalized = value.TrimEnd('=').Replace('-', '+').Replace('_', '/');
        try
        {
            var bytes = Convert.FromBase64String(normalized.PadRight((normalized.Length + 3) / 4 * 4, '='));
            if (bytes.Length != length) throw new ArgumentException("طول کلید subscription معتبر نیست.");
            return bytes;
        }
        catch (FormatException) { throw new ArgumentException("کلید subscription معتبر نیست."); }
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
