using System.Security.Cryptography.X509Certificates;
using MakanApp.Application.Platform;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.DataProtection;

namespace MakanApp.Api.Configuration;

public static class BackofficeMfaConfiguration
{
    public static void AddBackofficeMfa(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var protection = services.AddDataProtection().SetApplicationName("MakanApp.BackofficeMfa.v1");
        var keyRingPath = configuration["BackofficeSecurity:KeyRingPath"];
        var development = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var configured = false;
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            if (!Path.IsPathFullyQualified(keyRingPath)) throw new InvalidOperationException("مسیر key ring باید مطلق باشد.");
            var root = Path.GetFullPath(keyRingPath).TrimEnd(Path.DirectorySeparatorChar);
            var appRoot = Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar);
            var storageRoot = configuration["Storage:RootPath"];
            if (Overlaps(root, appRoot) || (!string.IsNullOrWhiteSpace(storageRoot) && Path.IsPathFullyQualified(storageRoot) &&
                Overlaps(root, Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar))))
                throw new InvalidOperationException("key ring باید از artifact و storage عمومی فایل جدا باشد.");
            protection.PersistKeysToFileSystem(new DirectoryInfo(root));
            var certificatePath = configuration["BackofficeSecurity:CertificatePath"];
            if (!string.IsNullOrWhiteSpace(certificatePath))
            {
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                    configuration["BackofficeSecurity:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
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
        // فقدان key ring در محیط عملیاتی MFA را fail-closed می‌کند، نه workflow معمول آموزشی را.
        services.AddSingleton<IPlatformMfaCryptography>(sp => new TotpMfaCryptography(
            configured || development ? sp.GetRequiredService<IDataProtectionProvider>() : null));
    }
    private static bool Overlaps(string first, string second) => first.Equals(second, StringComparison.OrdinalIgnoreCase) ||
        first.StartsWith(second + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        second.StartsWith(first + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
