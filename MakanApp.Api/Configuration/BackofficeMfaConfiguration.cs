using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MakanApp.Api.Hosting;
using MakanApp.Application.Platform;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.DataProtection;

namespace MakanApp.Api.Configuration;

public static class BackofficeMfaConfiguration
{
    public static void AddBackofficeMfa(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = DataProtectionKeyRingSettings.Create(configuration, environment);
        services.AddSingleton(settings);
        var protection = services.AddDataProtection().SetApplicationName(settings.ApplicationName);
        var keyRingPath = settings.KeyRingPath;
        var development = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var configured = false;
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
            var certificatePath = settings.CertificatePath;
            if (!string.IsNullOrWhiteSpace(certificatePath))
            {
                X509Certificate2 certificate;
                try
                {
                    certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                        settings.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
                }
                catch (Exception exception) when (exception is CryptographicException or IOException or
                                                     UnauthorizedAccessException or SecurityException or ArgumentException)
                {
                    throw new InvalidOperationException("گواهی محافظ key ring قابل بارگذاری نیست.");
                }
                if (!certificate.HasPrivateKey) throw new InvalidOperationException("گواهی محافظ key ring باید private key داشته باشد.");
                protection.ProtectKeysWithCertificate(certificate);
                configured = true;
            }
            else if (OperatingSystem.IsWindows())
            {
                protection.ProtectKeysWithDpapi();
                configured = true;
            }
            else if (!development) throw new InvalidOperationException("محافظ key ring پایدار در این محیط پیکربندی نشده است.");
        }
        // قرارداد مستقیم MFA همچنان fail-closed است؛ شروع host عملیاتی جداگانه حلقهٔ پایدار را الزام می‌کند.
        services.AddSingleton<IPlatformMfaCryptography>(sp => new TotpMfaCryptography(
            configured || development ? sp.GetRequiredService<IDataProtectionProvider>() : null));
        services.AddSingleton<DataProtectionKeyRingProbe>();
        services.AddHostedService<DataProtectionKeyRingStartupValidator>();
    }
}
