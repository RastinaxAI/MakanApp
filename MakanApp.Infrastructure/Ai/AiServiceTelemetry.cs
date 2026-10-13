using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MakanApp.Infrastructure.Ai;

internal static class AiServiceTelemetry
{
    private static readonly Meter Meter = new("MakanApp.AiService", "1.0.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("makan.ai.client.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "makan.ai.client.duration",
        "ms");

    public static void Record(string outcome, string errorCode, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "operation", "chat" },
            { "outcome", outcome },
            { "error.code", errorCode }
        };
        Requests.Add(1, tags);
        Duration.Record(duration.TotalMilliseconds, tags);
    }
}
