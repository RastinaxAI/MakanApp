using System.Diagnostics;
using System.Diagnostics.Metrics;
using MakanApp.Application.Platform;

namespace MakanApp.Infrastructure.Observability;

public sealed class WorkerTelemetry : IDisposable
{
    public const string MeterName = "MakanApp.Workers";
    public const string ActivitySourceName = "MakanApp.Workers";
    public const string IntegrationOutbox = nameof(IntegrationOutbox);
    public const string MessagingRealtimeOutbox = nameof(MessagingRealtimeOutbox);
    public const string NotificationDelivery = nameof(NotificationDelivery);
    public const string ScheduledMessage = nameof(ScheduledMessage);
    public const string ReportExport = nameof(ReportExport);
    public const string MessagingEphemeral = nameof(MessagingEphemeral);
    public const string OperationalMetrics = nameof(OperationalMetrics);
    public const string FileCleanup = nameof(FileCleanup);
    public const string Other = nameof(Other);

    private readonly TimeProvider _timeProvider;
    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Counter<long> _batches;
    private readonly Counter<long> _items;
    private readonly Counter<long> _failures;
    private readonly Counter<long> _cleanupResults;
    private readonly Histogram<double> _duration;
    private Snapshot _snapshot = new(false, null, []);

    public WorkerTelemetry(IMeterFactory meterFactory, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        var meter = meterFactory.Create(MeterName);
        _batches = meter.CreateCounter<long>("makan.worker.batches");
        _items = meter.CreateCounter<long>("makan.worker.items");
        _failures = meter.CreateCounter<long>("makan.worker.failures");
        _cleanupResults = meter.CreateCounter<long>("makan.file_cleanup.results");
        _duration = meter.CreateHistogram<double>("makan.worker.duration", "ms");
        meter.CreateObservableGauge<long>("makan.queue.items", ObserveQueueItems, "{item}");
        meter.CreateObservableGauge<double>("makan.queue.oldest_age", ObserveQueueAges, "s");
        meter.CreateObservableGauge<long>("makan.operational.snapshot.available", () => Volatile.Read(ref _snapshot).Available ? 1 : 0);
        meter.CreateObservableGauge<double>("makan.operational.snapshot.age", ObserveSnapshotAge, "s");
    }

    public Activity? BeginBatch(string worker)
    {
        var activity = _activitySource.StartActivity("makan.worker.batch", ActivityKind.Internal);
        activity?.SetTag("worker", NormalizeWorker(worker));
        return activity;
    }

    public void RecordBatch(string worker, int items, long startedAtTimestamp)
    {
        var tag = Tag("worker", NormalizeWorker(worker));
        _batches.Add(1, tag);
        _items.Add(Math.Max(0, items), tag);
        _duration.Record(Math.Max(0, Stopwatch.GetElapsedTime(startedAtTimestamp).TotalMilliseconds), tag);
    }

    public void RecordFailure(string worker, string stage) =>
        _failures.Add(1, Tag("worker", NormalizeWorker(worker)), Tag("stage", NormalizeStage(stage)));

    public void RecordCleanup(string outcome, int count)
    {
        if (count <= 0) return;
        var bounded = outcome is "Examined" or "Deleted" or "AlreadyMissing" or "Skipped" or "Failed" ? outcome : Other;
        _cleanupResults.Add(count, Tag("outcome", bounded));
    }

    public void UpdateSnapshot(PlatformJobOperations jobs, PlatformNotificationOperations notifications)
    {
        var now = _timeProvider.GetUtcNow();
        var queues = jobs.Queues.Select(queue => new QueueReading(NormalizeWorker(queue.JobType),
                Math.Max(0, queue.PendingCount), Math.Max(0, queue.ProcessingCount), Math.Max(0, queue.FailedCount), 0, false,
                queue.OldestPendingAtUtc.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(queue.OldestPendingAtUtc.Value, DateTimeKind.Utc))
                    : null))
            .Append(new QueueReading(NotificationDelivery, Math.Max(0, notifications.PendingCount),
                Math.Max(0, notifications.ProcessingCount), Math.Max(0, notifications.FailedCount),
                Math.Max(0, notifications.DeferredCount), true,
                notifications.OldestQueuedAgeSeconds is { } age && double.IsFinite(age)
                    ? now.AddSeconds(-Math.Max(0, age)) : null))
            .GroupBy(queue => queue.Name, StringComparer.Ordinal)
            .Select(group => new QueueReading(group.Key, group.Sum(queue => queue.Pending), group.Sum(queue => queue.Processing),
                group.Sum(queue => queue.Failed), group.Sum(queue => queue.Deferred), group.Any(queue => queue.HasDeferred),
                group.Select(queue => queue.OldestAtUtc).Min()))
            .ToArray();
        Volatile.Write(ref _snapshot, new Snapshot(true, now, queues));
    }

    public void MarkSnapshotUnavailable()
    {
        Snapshot previous;
        do { previous = Volatile.Read(ref _snapshot); }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, previous with { Available = false }, previous), previous));
    }

    private IEnumerable<Measurement<long>> ObserveQueueItems()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        foreach (var queue in snapshot.Queues)
        {
            yield return new Measurement<long>(queue.Pending, Tag("queue", queue.Name), Tag("state", "Pending"));
            yield return new Measurement<long>(queue.Processing, Tag("queue", queue.Name), Tag("state", "Processing"));
            yield return new Measurement<long>(queue.Failed, Tag("queue", queue.Name), Tag("state", "Failed"));
            if (queue.HasDeferred)
                yield return new Measurement<long>(queue.Deferred, Tag("queue", queue.Name), Tag("state", "Deferred"));
        }
    }

    private IEnumerable<Measurement<double>> ObserveQueueAges()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        var now = _timeProvider.GetUtcNow();
        foreach (var queue in snapshot.Queues)
            yield return new Measurement<double>(queue.OldestAtUtc.HasValue ? Math.Max(0, (now - queue.OldestAtUtc.Value).TotalSeconds) : 0,
                Tag("queue", queue.Name));
    }

    private IEnumerable<Measurement<double>> ObserveSnapshotAge()
    {
        var observedAt = Volatile.Read(ref _snapshot).ObservedAtUtc;
        if (observedAt.HasValue)
            yield return new Measurement<double>(Math.Max(0, (_timeProvider.GetUtcNow() - observedAt.Value).TotalSeconds));
    }

    private static string NormalizeWorker(string? worker) => worker switch
    {
        IntegrationOutbox or MessagingRealtimeOutbox or NotificationDelivery or ScheduledMessage or
            ReportExport or MessagingEphemeral or OperationalMetrics or FileCleanup => worker,
        _ => Other
    };

    private static string NormalizeStage(string? stage) => stage switch
    {
        "Batch" or "Claim" or "Process" or "Dispatch" or "Send" or "Notify" or "Cleanup" or "Snapshot" or "Loop" => stage,
        _ => Other
    };

    private static KeyValuePair<string, object?> Tag(string name, string value) => new(name, value);
    public void Dispose() => _activitySource.Dispose();

    private sealed record Snapshot(bool Available, DateTimeOffset? ObservedAtUtc, QueueReading[] Queues);
    private sealed record QueueReading(string Name, long Pending, long Processing, long Failed, long Deferred, bool HasDeferred, DateTimeOffset? OldestAtUtc);
}
