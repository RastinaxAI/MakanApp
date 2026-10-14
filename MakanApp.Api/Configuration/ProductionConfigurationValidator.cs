using System.Net;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MakanApp.Api.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, string environmentName)
    {
        if (!environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
            !environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connectionString = Require(configuration, "ConnectionStrings:MakanDatabase");
        SqlConnectionStringBuilder sql;
        try
        {
            sql = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw Invalid("ConnectionStrings:MakanDatabase is not a valid SQL Server connection string.");
        }
        if (!sql.Encrypt || sql.TrustServerCertificate)
        {
            throw Invalid(
                "ConnectionStrings:MakanDatabase در Production/Staging باید Encrypt=True و TrustServerCertificate=False داشته باشد.");
        }

        if (Encoding.UTF8.GetByteCount(Require(configuration, "Identity:SecurityKey")) < 32)
        {
            throw Invalid("Identity:SecurityKey در Production/Staging باید حداقل ۳۲ بایت UTF-8 داشته باشد.");
        }

        var storageRoot = Environment.ExpandEnvironmentVariables(Require(configuration, "Storage:RootPath"));
        if (!Path.IsPathFullyQualified(storageRoot))
        {
            throw Invalid("Storage:RootPath در Production/Staging باید مسیر مطلق و پایدار باشد.");
        }

        var allowedHosts = Require(configuration, "AllowedHosts");
        if (allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(host => host == "*"))
        {
            throw Invalid("AllowedHosts در Production/Staging نمی‌تواند شامل '*' باشد.");
        }

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                origin.Contains('*', StringComparison.Ordinal)))
        {
            throw Invalid("Cors:AllowedOrigins در Production/Staging باید فقط originهای صریح HTTPS داشته باشد.");
        }

        if (!configuration.GetValue("Hosting:RequireHttps", false))
        {
            throw Invalid("Hosting:RequireHttps در Production/Staging باید true باشد.");
        }

        if (!configuration.GetValue("ReverseProxy:Enabled", false))
        {
            throw Invalid("ReverseProxy:Enabled برای مقصد IIS در Production/Staging باید true باشد.");
        }

        var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        if (knownProxies.Length == 0 || knownProxies.Any(value => !IPAddress.TryParse(value, out _)))
        {
            throw Invalid("ReverseProxy:KnownProxies در Production/Staging باید IPهای صریح داشته باشد.");
        }
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid($"تنظیم {key} در Production/Staging الزامی است.")
            : value;
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
