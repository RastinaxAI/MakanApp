using Microsoft.Extensions.Configuration;

namespace MakanApp.Infrastructure.Identity;

public sealed class OtpAuthenticationOptions
{
    public const string SectionName = "Otp";
    public bool Enabled { get; set; } = true;
    public bool AllowMockAuthentication { get; set; }

    public static OtpAuthenticationOptions Load(IConfiguration configuration, string environmentName)
    {
        var options = configuration.GetSection(SectionName).Get<OtpAuthenticationOptions>() ?? new();
        if (!IsSafe(options, environmentName))
            throw new InvalidOperationException(
                "Mock Authentication فقط در Development/Test/Testing و با Otp:AllowMockAuthentication=true مجاز است؛ در Production/Staging باید Otp:Enabled=true باشد.");

        var legacyMode = configuration["Identity:Otp:Mode"];
        if (legacyMode is not null && !legacyMode.Trim().Equals("Real", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Identity:Otp:Mode=DevelopmentBypass دیگر قابل فعال‌سازی نیست؛ فقط Otp:Enabled و محافظ محیط تست را تنظیم کنید.");
        foreach (var key in new[] { "DevelopmentBypassEnabled", "Identity:Otp:DevelopmentBypassEnabled", "Otp:DevelopmentBypassEnabled" })
        {
            if (configuration.GetValue(key, false))
                throw new InvalidOperationException("DevelopmentBypassEnabled با قرارداد واحد Otp:Enabled سازگار نیست؛ تنظیم قدیمی را حذف کنید.");
        }
        return options;
    }

    internal static bool IsSafe(OtpAuthenticationOptions options, string environmentName) =>
        IsMockEnvironment(environmentName)
            ? options.Enabled || options.AllowMockAuthentication
            : options.Enabled && !options.AllowMockAuthentication;

    private static bool IsMockEnvironment(string environmentName) =>
        new[] { "Development", "Testing", "Test" }.Contains(environmentName, StringComparer.OrdinalIgnoreCase);
}
