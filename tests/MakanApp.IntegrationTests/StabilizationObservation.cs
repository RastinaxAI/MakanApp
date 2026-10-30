using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MakanApp.IntegrationTests;

// ابزار شاهد آزمون؛ فقط Method/Route ماسک‌شده، Status، correlation و زمان را نگه می‌دارد.
internal sealed class StabilizationObservation(string scenario)
{
    private readonly ConcurrentQueue<object> _requests = new();
    private readonly ConcurrentDictionary<string, DateTime> _stages = new();
    private readonly ConcurrentDictionary<string, double> _elapsed = new();
    private readonly long _started = Stopwatch.GetTimestamp();

    internal DelegatingHandler Handler() => new Observer(this);
    internal void Mark(string stage)
    {
        _stages.TryAdd(stage, DateTime.UtcNow);
        _elapsed.TryAdd(stage, Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
    }

    internal void Save(DateTime acceptedAtUtc, DateTime? publishStartedAtUtc)
    {
        var directory = Environment.GetEnvironmentVariable("MAKANAPP_TEST_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MakanApp.sln"))) current = current.Parent;
        var target = Path.GetFullPath(directory);
        if (current is null || !target.StartsWith(Path.Combine(current.FullName, ".artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("شاهد آزمون فقط در artifact خصوصی مخزن نوشته می‌شود.");
        Directory.CreateDirectory(target);
        var value = new
        {
            scenario,
            httpRequestCount = _requests.Count,
            httpRequests = _requests.ToArray(),
            stages = _stages,
            stageElapsedMs = _elapsed,
            serverAcceptedAtUtc = acceptedAtUtc,
            serverClock = "ControlledTestClock",
            commitAtUtc = (DateTime?)null,
            commitTiming = "Exact SQL commit was not instrumented; HTTP receipt is an observed upper bound.",
            publishedAtUtc = publishStartedAtUtc,
            publicationTiming = "Notifier invocation after committed outbox claim; same machine clock.",
            renderEquivalent = "Authorized inbox projection verified after live invalidation; no browser render claim.",
            signalRTransportRequestCountIncluded = false,
            actualUiRenderTested = false,
            secretCaptured = false
        };
        File.WriteAllText(Path.Combine(target, scenario + "-" + Guid.NewGuid().ToString("N") + ".json"),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class Observer(StabilizationObservation owner) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var started = Stopwatch.GetTimestamp();
            var response = await base.SendAsync(request, ct);
            string? correlation = response.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.SingleOrDefault() : null;
            owner._requests.Enqueue(new
            {
                method = request.Method.Method,
                route = Regex.Replace(request.RequestUri!.AbsolutePath, @"[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}", "{id}"),
                status = (int)response.StatusCode,
                elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                correlationId = correlation
            });
            return response;
        }
    }
}
