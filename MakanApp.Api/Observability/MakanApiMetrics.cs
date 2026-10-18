using System.Diagnostics.Metrics;

namespace MakanApp.Api.Observability;

public sealed class MakanApiMetrics
{
    public const string MeterName = "MakanApp.Api";

    public MakanApiMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        RequestCount = meter.CreateCounter<long>("makan.http.server.requests");
        RequestErrors = meter.CreateCounter<long>("makan.http.server.errors");
        RequestFailures = meter.CreateCounter<long>("makan.http.server.failures");
        RequestDuration = meter.CreateHistogram<double>("makan.http.server.duration", "ms");
    }

    public Counter<long> RequestCount { get; }
    public Counter<long> RequestErrors { get; }
    public Counter<long> RequestFailures { get; }
    public Histogram<double> RequestDuration { get; }
}
