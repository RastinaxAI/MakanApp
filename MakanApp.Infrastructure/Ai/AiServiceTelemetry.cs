using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MakanApp.Infrastructure.Ai;

internal static class AiServiceTelemetry
{
    private static readonly ActivitySource Activities = new("MakanApp.AiService", "1.0.0");
    private static readonly Meter Meter = new("MakanApp.AiService", "1.0.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("makan.ai.client.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "makan.ai.client.duration",
        "ms");
    private static readonly Counter<long> CircuitRejections = Meter.CreateCounter<long>("makan.ai.client.circuit.rejections");

    public static Activity? Start(string operation, string requestId, string? correlationId) =>
        Activities.StartActivity("ai.invoke", ActivityKind.Client)?
            .SetTag("ai.operation", operation)
            .SetTag("request.id", requestId)
            .SetTag("correlation.id", correlationId)
            .SetTag("http.request.method", "POST");

    public static void SetHttpStatus(int status)
    {
        if (Activity.Current?.Source.Name == Activities.Name)
            Activity.Current.SetTag("http.response.status_code", status);
    }

    public static void RecordCircuitRejection(string operation)
    {
        CircuitRejections.Add(1, new KeyValuePair<string, object?>("operation", operation));
        if (Activity.Current?.Source.Name == Activities.Name)
            Activity.Current.SetTag("ai.circuit.state", "open");
    }

    public static void Record(
        string operation,
        string outcome,
        string errorCode,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "outcome", outcome },
            { "error.code", errorCode }
        };
        Requests.Add(1, tags);
        Duration.Record(duration.TotalMilliseconds, tags);
        // فقط span متعلق به AI تغییر می‌کند؛ در نبود listener، parent میزبان دست‌نخورده است.
        if (Activity.Current?.Source.Name == Activities.Name)
        {
            Activity.Current.SetTag("ai.outcome", outcome);
            Activity.Current.SetTag("error.code", errorCode);
            Activity.Current.SetStatus(outcome == "success" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
    }
}
