using System.Net;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MakanApp.Api.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, string environmentName)
    {
        var isProduction = environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);
        if (!isProduction &&
            !environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        HostShutdownConfiguration.ValidateConfiguration(configuration);
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
        // استثناها فقط با تأیید صریح مالک در کانفیگ Production فعال می‌شوند؛ پیش‌فرض امن می‌ماند.
        var allowInsecureHttp = isProduction && configuration.GetValue("Hosting:AllowInsecureHttp", false);
        var allowUntrustedCertificate = isProduction &&
            configuration.GetValue("Database:AllowUntrustedServerCertificate", false);
        if (!sql.Encrypt || (sql.TrustServerCertificate && !allowUntrustedCertificate))
        {
            throw Invalid(
                "اتصال SQL باید Encrypt=True داشته باشد؛ TrustServerCertificate=True فقط با استثنای صریح Database:AllowUntrustedServerCertificate در Production مجاز است.");
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

        // wildcard صریح طبق تصمیم مالک مجاز است؛ نبود مقدار همچنان خطای پیکربندی است.
        _ = Require(configuration, "AllowedHosts");

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (!configuration.GetValue("Cors:AllowAnyOrigin", false) &&
            (origins.Length == 0 || origins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 !(allowInsecureHttp && uri.Scheme == Uri.UriSchemeHttp)) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                origin.Contains('*', StringComparison.Ordinal))))
        {
            throw Invalid("Cors:AllowedOrigins باید originهای صریح HTTPS داشته باشد؛ HTTP فقط با استثنای Hosting:AllowInsecureHttp در Production مجاز است.");
        }

        if (!configuration.GetValue("Hosting:RequireHttps", false) && !allowInsecureHttp)
        {
            throw Invalid("Hosting:RequireHttps باید true باشد؛ غیرفعال‌سازی فقط با استثنای صریح Hosting:AllowInsecureHttp در Production مجاز است.");
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
