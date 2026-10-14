using System.Text.RegularExpressions;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    internal MakanAppWebApplicationFactory(
        string recoveryDatabaseName,
        string recoveryStorageRoot,
        string recoverySecurityKey)
    {
        if (!Regex.IsMatch(recoveryDatabaseName, "^MakanApp_Recovery_[A-Za-z0-9_]{8,80}\\z") ||
            !Path.IsPathFullyQualified(recoveryStorageRoot) || recoverySecurityKey.Length < 32)
        {
            throw new ArgumentException("پارامترهای محیط مستقل بازیابی معتبر نیستند.");
        }

        _instanceConnectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={recoveryDatabaseName};Trusted_Connection=True;TrustServerCertificate=True";
        StorageRootPath = recoveryStorageRoot;
        _recoverySecurityKey = recoverySecurityKey;
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _instanceConnectionString);
    }
}
