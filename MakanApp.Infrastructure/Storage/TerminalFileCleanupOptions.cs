using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace MakanApp.Infrastructure.Storage;

public sealed class TerminalFileCleanupOptions
{
    public const string SectionName = "Storage:TerminalCleanup";
    public const int MaximumBatchSize = 100;
    // این سقف فقط از overflow زمان جلوگیری می‌کند؛ دورهٔ نگه‌داری حقوقی یا پیشنهادی نیست.
    public static readonly int MaximumGraceHours = (int)(DateTime.MaxValue.Ticks / TimeSpan.TicksPerHour);

    public bool Enabled { get; init; }
    public int? GracePeriodHours { get; init; }
    public int BatchSize { get; init; } = 25;
    public int IntervalSeconds { get; init; } = 60;

    public static TerminalFileCleanupOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = false;
        var rawEnabled = configuration[$"{SectionName}:Enabled"];
        if (rawEnabled is not null && !bool.TryParse(rawEnabled, out enabled))
            throw Invalid("Enabled");
        var options = new TerminalFileCleanupOptions
        {
            Enabled = enabled,
            GracePeriodHours = ReadInteger(configuration, "GracePeriodHours"),
            BatchSize = ReadInteger(configuration, "BatchSize") ?? 25,
            IntervalSeconds = ReadInteger(configuration, "IntervalSeconds") ?? 60
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (GracePeriodHours is { } grace && (grace <= 0 || grace > MaximumGraceHours)) throw Invalid("GracePeriodHours");
        if (Enabled && !GracePeriodHours.HasValue) throw Invalid("GracePeriodHours");
        if (BatchSize is < 1 or > MaximumBatchSize) throw Invalid("BatchSize");
        if (IntervalSeconds is < 5 or > 3600) throw Invalid("IntervalSeconds");
    }

    private static int? ReadInteger(IConfiguration configuration, string name)
    {
        var raw = configuration[$"{SectionName}:{name}"];
        if (raw is null) return null;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw Invalid(name);
        return value;
    }

    private static InvalidOperationException Invalid(string name) =>
        new($"تنظیم {SectionName}:{name} معتبر نیست؛ پاک‌سازی بدون تنظیم صریح و معتبر مجاز نیست.");
}
