namespace MakanApp.Api.Configuration;

public sealed class DataProtectionKeyRingSettings
{
    public const string LegacyApplicationName = "MakanApp.BackofficeMfa.v1";
    private const string SectionName = "Security:DataProtection";

    public required string ApplicationName { get; init; }
    public string? KeyRingPath { get; init; }
    public string? CertificatePath { get; init; }
    public string? CertificatePassword { get; init; }
    public required bool PersistenceRequired { get; init; }

    public static DataProtectionKeyRingSettings Create(IConfiguration configuration, IHostEnvironment environment)
    {
        var applicationName = configuration[$"{SectionName}:ApplicationName"] ?? LegacyApplicationName;
        if (string.IsNullOrWhiteSpace(applicationName) || applicationName.Length > 200 ||
            applicationName.Any(char.IsControl) || applicationName != applicationName.Trim())
        {
            throw Invalid("Security:DataProtection:ApplicationName باید نام ثابت و معتبر داشته باشد.");
        }

        var root = ResolveAlias(configuration, "KeyRingPath", normalizePath: true);
        var certificatePath = ResolveAlias(configuration, "CertificatePath", normalizePath: true);
        var certificatePassword = ResolveAlias(configuration, "CertificatePassword", normalizePath: false);
        if (root is not null)
        {
            var appRoot = Path.GetFullPath(environment.ContentRootPath);
            var storageRoot = configuration["Storage:RootPath"];
            if (Overlaps(root, appRoot) || (!string.IsNullOrWhiteSpace(storageRoot) &&
                Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(storageRoot)) &&
                Overlaps(root, Path.GetFullPath(Environment.ExpandEnvironmentVariables(storageRoot)))))
            {
                throw Invalid("key ring باید از artifact و storage عمومی فایل جدا باشد.");
            }
        }
        else if (certificatePath is not null || certificatePassword is not null)
        {
            throw Invalid("محافظ گواهی به مسیر پایدار key ring نیاز دارد.");
        }

        return new DataProtectionKeyRingSettings
        {
            ApplicationName = applicationName,
            KeyRingPath = root,
            CertificatePath = certificatePath,
            CertificatePassword = certificatePassword,
            PersistenceRequired = !environment.IsDevelopment() && !environment.IsEnvironment("Testing")
        };
    }

    private static string? ResolveAlias(IConfiguration configuration, string name, bool normalizePath)
    {
        var current = configuration[$"{SectionName}:{name}"];
        var legacy = configuration[$"BackofficeSecurity:{name}"];
        var normalizedCurrent = Normalize(current, normalizePath);
        var normalizedLegacy = Normalize(legacy, normalizePath);
        var comparison = normalizePath && OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (normalizedCurrent is not null && normalizedLegacy is not null &&
            !normalizedCurrent.Equals(normalizedLegacy, comparison))
        {
            throw Invalid($"تنظیم‌های {SectionName}:{name} و BackofficeSecurity:{name} متعارض‌اند.");
        }

        return normalizedCurrent ?? normalizedLegacy;
    }

    private static string? Normalize(string? value, bool path)
    {
        if (value is null || value.Length == 0 || (path && string.IsNullOrWhiteSpace(value))) return null;
        if (!path) return value;
        var expanded = Environment.ExpandEnvironmentVariables(value.Trim());
        if (!Path.IsPathFullyQualified(expanded))
        {
            throw Invalid("مسیر key ring و گواهی محافظ باید مطلق باشند.");
        }

        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded)); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Invalid("مسیر key ring یا گواهی محافظ معتبر نیست.");
        }
    }

    private static bool Overlaps(string first, string second)
    {
        first = Path.TrimEndingDirectorySeparator(first);
        second = Path.TrimEndingDirectorySeparator(second);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return first.Equals(second, comparison) ||
               first.StartsWith(Path.EndsInDirectorySeparator(second) ? second : second + Path.DirectorySeparatorChar, comparison) ||
               second.StartsWith(Path.EndsInDirectorySeparator(first) ? first : first + Path.DirectorySeparatorChar, comparison);
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
