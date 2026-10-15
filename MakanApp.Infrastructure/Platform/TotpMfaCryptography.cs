using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Platform;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;

namespace MakanApp.Infrastructure.Platform;

public sealed class TotpMfaCryptography(IDataProtectionProvider? provider) : IPlatformMfaCryptography
{
    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
    public string Protect(Guid userId, string secret)
    {
        try { return Convert.ToBase64String(Protector(userId).Protect(Encoding.UTF8.GetBytes(secret))); }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new PlatformException(PlatformErrorCodes.MfaUnavailable, "محافظت از عامل امنیتی در دسترس نیست.");
        }
    }
    public bool Verify(Guid userId, string protectedSecret, string code, DateTime nowUtc, out long timeStep)
    {
        timeStep = -1;
        if (code is null || code.Length != 6 || code.Any(c => c is < '0' or > '9')) return false;
        try
        {
            var secret = Encoding.UTF8.GetString(Protector(userId).Unprotect(Convert.FromBase64String(protectedSecret)));
            var totp = new Totp(Base32Encoding.ToBytes(secret), step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
            return totp.VerifyTotp(nowUtc, code, out timeStep, new VerificationWindow(1, 1));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or IOException or UnauthorizedAccessException)
        {
            throw new PlatformException(PlatformErrorCodes.MfaUnavailable, "محافظت از عامل امنیتی در دسترس نیست.");
        }
    }
    public string GenerateRecoveryCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
    public string HashRecoveryCode(Guid userId, string code) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes("makan-platform-recovery-v1:" + userId.ToString("N") + ":" + code.Trim().ToUpperInvariant())));
    public bool MatchesRecoveryCode(Guid userId, string code, string hash) =>
        code is not null && code.Length <= 100 && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(HashRecoveryCode(userId, code)), Convert.FromHexString(hash));
    private IDataProtector Protector(Guid userId) => provider?.CreateProtector("MakanApp.PlatformMfa.v1." + userId.ToString("N"))
        ?? throw new PlatformException(PlatformErrorCodes.MfaUnavailable, "کلید پایدار محافظت از MFA پیکربندی نشده است.");
}
