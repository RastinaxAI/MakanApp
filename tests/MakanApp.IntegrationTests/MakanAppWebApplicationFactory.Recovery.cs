using System.Text.RegularExpressions;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    private readonly string? _recoveryDataProtectionRoot;

    internal MakanAppWebApplicationFactory(
        string recoveryDatabaseName,
        string recoveryStorageRoot,
        string recoverySecurityKey,
        string? recoveryDataProtectionRoot = null)
    {
        if (!Regex.IsMatch(recoveryDatabaseName, "^MakanApp_Recovery_[A-Za-z0-9_]{8,80}\\z") ||
            !Path.IsPathFullyQualified(recoveryStorageRoot) || recoverySecurityKey.Length < 32)
        {
            throw new ArgumentException("پارامترهای محیط مستقل بازیابی معتبر نیستند.");
        }
        if (recoveryDataProtectionRoot is not null &&
            (!Path.IsPathFullyQualified(recoveryDataProtectionRoot) ||
             !Path.GetFullPath(recoveryDataProtectionRoot).StartsWith(
                 Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar,
                 StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Ring بازیابی تست باید داخل مسیر موقت مستقل باشد.");
        }
        _instanceConnectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={recoveryDatabaseName};Trusted_Connection=True;TrustServerCertificate=True";
        StorageRootPath = recoveryStorageRoot;
        _recoverySecurityKey = recoverySecurityKey;
        _recoveryDataProtectionRoot = recoveryDataProtectionRoot;
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _instanceConnectionString);
    }
}
