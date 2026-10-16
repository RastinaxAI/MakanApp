using System.Security.Cryptography;

namespace MakanApp.Infrastructure.Notification;

public sealed class WebPushTransportOptions
{
    public string? Subject { get; init; }
    public string? PublicKey { get; init; }
    public string? PrivateKey { get; init; }
    public int TimeToLiveSeconds { get; init; } = 3600;

    public bool HasConfiguration => !string.IsNullOrWhiteSpace(Subject) || !string.IsNullOrWhiteSpace(PublicKey) || !string.IsNullOrWhiteSpace(PrivateKey);

    public bool IsConfigured
    {
        get
        {
            if (TimeToLiveSeconds is < 0 or > 86400 || Subject is null || Subject.Length > 256 ||
                Subject.Any(c => char.IsControl(c) || c is '"' or '\\') ||
                !Uri.TryCreate(Subject, UriKind.Absolute, out var subject) ||
                (subject.Scheme != "mailto" && subject.Scheme != Uri.UriSchemeHttps) ||
                subject.Scheme == Uri.UriSchemeHttps && subject.UserInfo.Length != 0 ||
                subject.Scheme == "mailto" && !Subject.AsSpan("mailto:".Length).Contains('@')) return false;
            try
            {
                var publicBytes = PushKeyEncoding.Decode(PublicKey, 65);
                var privateBytes = PushKeyEncoding.Decode(PrivateKey, 32);
                if (publicBytes[0] != 4) return false;
                var parameters = new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = publicBytes[1..33], Y = publicBytes[33..65] }, D = privateBytes };
                using var signer = ECDsa.Create(parameters);
                parameters.D = null;
                using var verifier = ECDsa.Create(parameters);
                var signature = signer.SignData("Makan VAPID configuration"u8, HashAlgorithmName.SHA256);
                return verifier.VerifyData("Makan VAPID configuration"u8, signature, HashAlgorithmName.SHA256);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException or PlatformNotSupportedException)
            { return false; }
        }
    }
}

public sealed class FcmTransportOptions
{
    public string? ProjectId { get; init; }
    public string? ServiceAccountPath { get; init; }
    public int TimeToLiveSeconds { get; init; } = 3600;
    public bool HasConfiguration => !string.IsNullOrWhiteSpace(ProjectId) || !string.IsNullOrWhiteSpace(ServiceAccountPath);
    public bool HasValidProjectSettings => ProjectId is { Length: >= 6 and <= 63 } && char.IsAsciiLetterLower(ProjectId[0]) &&
        char.IsAsciiLetterOrDigit(ProjectId[^1]) && ProjectId.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') &&
        TimeToLiveSeconds is >= 0 and <= 86400;
    public bool IsConfigured => HasValidProjectSettings && ServiceAccountPath is not null &&
        Path.IsPathFullyQualified(ServiceAccountPath) && File.Exists(ServiceAccountPath);
}

internal static class PushKeyEncoding
{
    internal static byte[] Decode(string? value, int expectedLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value.TrimEnd('=').Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("کلید Push معتبر نیست.");
        var normalized = value.TrimEnd('=').Replace('-', '+').Replace('_', '/');
        var bytes = Convert.FromBase64String(normalized.PadRight((normalized.Length + 3) / 4 * 4, '='));
        if (bytes.Length != expectedLength) throw new ArgumentException("طول کلید Push معتبر نیست.");
        return bytes;
    }
}
