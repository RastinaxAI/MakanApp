using System.Net;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MakanApp.Api.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, string environmentName)
    {
        if (!environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase))
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
                "ConnectionStrings:MakanDatabase must use Encrypt=True and TrustServerCertificate=False in Production.");
        }

        if (Encoding.UTF8.GetByteCount(Require(configuration, "Identity:SecurityKey")) < 32)
        {
            throw Invalid("Identity:SecurityKey must contain at least 32 UTF-8 bytes in Production.");
        }

        var storageRoot = Environment.ExpandEnvironmentVariables(Require(configuration, "Storage:RootPath"));
        if (!Path.IsPathFullyQualified(storageRoot))
        {
            throw Invalid("Storage:RootPath must be an absolute persistent path in Production.");
        }

        var allowedHosts = Require(configuration, "AllowedHosts");
        if (allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(host => host == "*"))
        {
            throw Invalid("AllowedHosts cannot contain '*' in Production.");
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
            throw Invalid("Cors:AllowedOrigins must contain only explicit HTTPS origins in Production.");
        }

        if (!configuration.GetValue("Hosting:RequireHttps", false))
        {
            throw Invalid("Hosting:RequireHttps must be true in Production.");
        }

        if (!configuration.GetValue("ReverseProxy:Enabled", false))
        {
            throw Invalid("ReverseProxy:Enabled must be true for the IIS Production target.");
        }

        var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        if (knownProxies.Length == 0 || knownProxies.Any(value => !IPAddress.TryParse(value, out _)))
        {
            throw Invalid("ReverseProxy:KnownProxies must contain explicit IP addresses in Production.");
        }
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid($"{key} must be configured in Production.")
            : value;
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
