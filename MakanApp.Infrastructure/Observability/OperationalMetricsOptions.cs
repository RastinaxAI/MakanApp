using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace MakanApp.Infrastructure.Observability;

public sealed class OperationalMetricsOptions
{
    public const string ConfigurationKey = "Observability:OperationalMetrics:SnapshotIntervalSeconds";
    public const int PublishBudgetSeconds = 10;
    public int SnapshotIntervalSeconds { get; init; } = 30;

    public void Validate()
    {
        if (SnapshotIntervalSeconds is < 5 or > 3600)
            throw new InvalidOperationException("Observability:OperationalMetrics:SnapshotIntervalSeconds باید بین ۵ و ۳۶۰۰ ثانیه باشد.");
    }

    public static OperationalMetricsOptions FromConfiguration(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        var seconds = 30;
        if (value is not null && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            throw new InvalidOperationException("Observability:OperationalMetrics:SnapshotIntervalSeconds باید عدد صحیح معتبر باشد.");
        var options = new OperationalMetricsOptions { SnapshotIntervalSeconds = seconds };
        options.Validate();
        return options;
    }
}
