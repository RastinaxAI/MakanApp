using System.Globalization;

namespace MakanApp.Api.Configuration;

public static class HostShutdownConfiguration
{
    public const string ConfigurationKey = "Hosting:ShutdownTimeoutSeconds";

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var configuredTimeout = ReadExplicitTimeout(configuration);
        services.AddOptions<HostOptions>().ValidateOnStart();
        services.PostConfigure<HostOptions>(options =>
        {
            // در نبود کلید جدید، override استاندارد Generic Host و default نسخهٔ ۹ حفظ می‌شوند.
            if (configuredTimeout.HasValue) options.ShutdownTimeout = configuredTimeout.Value;
            if (options.ShutdownTimeout < TimeSpan.FromSeconds(5) || options.ShutdownTimeout > TimeSpan.FromSeconds(90))
                throw Invalid();
        });
    }

    public static void ValidateConfiguration(IConfiguration configuration) => _ = ReadExplicitTimeout(configuration);

    private static TimeSpan? ReadExplicitTimeout(IConfiguration configuration)
    {
        var raw = configuration[ConfigurationKey];
        if (raw is null) return null;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds is < 5 or > 90)
            throw Invalid();
        return TimeSpan.FromSeconds(seconds);
    }

    private static InvalidOperationException Invalid() =>
        new("Hosting:ShutdownTimeoutSeconds باید عدد صحیح بین ۵ و ۹۰ ثانیه باشد؛ بودجهٔ نهایی Host نیز باید در همین محدوده قرار گیرد.");
}
