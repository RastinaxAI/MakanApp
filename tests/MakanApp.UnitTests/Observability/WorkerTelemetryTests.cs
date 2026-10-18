using System.Diagnostics;
using System.Diagnostics.Metrics;
using MakanApp.Application.Platform;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Observability;

public sealed class WorkerTelemetryTests
{
    [Theory]
    [InlineData("Claimed")]
    [InlineData("Completed")]
    [InlineData("RetryScheduled")]
    [InlineData("Failed")]
    [InlineData("LeaseRecovered")]
    [InlineData("Deferred")]
    [InlineData("Suppressed")]
    [InlineData("LeaseLost")]
    public void ConfirmedOutcomesAreDistinctFromBatchCountsAndHaveBoundedLabels(string outcome)
    {
        using var services = BuildServices(new TestClock());
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        telemetry.RecordOutcome(WorkerTelemetry.NotificationDelivery, outcome, 3);
        var value = Assert.Single(capture.Values);
        Assert.Equal("makan.worker.outcomes", value.Name);
        Assert.Equal(3, value.Value);
        Assert.Equal(WorkerTelemetry.NotificationDelivery, value.Tags["worker"]);
        Assert.Equal(outcome, value.Tags["outcome"]);
        Assert.Equal(2, value.Tags.Count);
    }

    [Fact]
    public void OutcomesDoNotLeakUntrustedLabelsOrRecordNonPositiveCounts()
    {
        using var services = BuildServices(new TestClock());
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        telemetry.RecordOutcome("private-user-marker", "private-content-marker", 0);
        telemetry.RecordOutcome(WorkerTelemetry.IntegrationOutbox, "Completed", -1);
        Assert.Empty(capture.Values);
        telemetry.RecordOutcome("private-user-marker", "private-content-marker");
        var value = Assert.Single(capture.Values);
        Assert.Equal("Other", value.Tags["worker"]);
        Assert.Equal("Other", value.Tags["outcome"]);
        Assert.DoesNotContain(capture.Values.SelectMany(item => item.Tags), tag => tag.Value.Contains("private-", StringComparison.Ordinal));
    }

    [Fact]
    public void BatchAndFailureMeasurementsUseOnlyFixedWorkerAndStageLabels()
    {
        var clock = new TestClock();
        using var services = BuildServices(clock);
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        telemetry.RecordBatch(WorkerTelemetry.IntegrationOutbox, 3, Stopwatch.GetTimestamp());
        telemetry.RecordFailure("private-user-marker", "private-message-marker");
        Assert.Equal(1, Assert.Single(capture.Values, item => item.Name == "makan.worker.batches").Value);
        Assert.Equal(3, Assert.Single(capture.Values, item => item.Name == "makan.worker.items").Value);
        Assert.True(Assert.Single(capture.Values, item => item.Name == "makan.worker.duration").Value >= 0);
        var failure = Assert.Single(capture.Values, item => item.Name == "makan.worker.failures");
        Assert.Equal("Other", failure.Tags["worker"]);
        Assert.Equal("Other", failure.Tags["stage"]);
        Assert.All(capture.Values.SelectMany(item => item.Tags), tag =>
        {
            Assert.Contains(tag.Key, new[] { "worker", "stage" });
            Assert.DoesNotContain("private-", tag.Value, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void BatchActivityUsesTheSameBoundedWorkerContract()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkerTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using var services = BuildServices(new TestClock());
        using var activity = services.GetRequiredService<WorkerTelemetry>().BeginBatch("private-resource-marker");
        Assert.NotNull(activity);
        Assert.Equal("makan.worker.batch", activity.OperationName);
        Assert.Equal("Other", activity.GetTagItem("worker"));
    }

    [Fact]
    public void GaugesBeforeFirstSnapshotDoNotInventEmptyQueueSuccess()
    {
        using var services = BuildServices(new TestClock());
        _ = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        capture.Collect();
        Assert.Equal(0, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.available").Value);
        Assert.DoesNotContain(capture.Values, item => item.Name.StartsWith("makan.queue.", StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Values, item => item.Name == "makan.operational.snapshot.age");
    }

    [Fact]
    public void FailedRefreshKeepsLastValuesAndExposesIncreasingSnapshotAge()
    {
        var clock = new TestClock();
        using var services = BuildServices(clock);
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        telemetry.UpdateSnapshot(Jobs(clock), Notifications());
        clock.Advance(TimeSpan.FromSeconds(120));
        telemetry.MarkSnapshotUnavailable();
        capture.Collect();
        Assert.Equal(0, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.available").Value);
        Assert.Equal(120, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.age").Value);
        Assert.Equal(3, Assert.Single(capture.Values, item => item.Name == "makan.queue.items" &&
            item.Tags["queue"] == WorkerTelemetry.IntegrationOutbox && item.Tags["state"] == "Pending").Value);
        Assert.Equal(140, Assert.Single(capture.Values, item => item.Name == "makan.queue.oldest_age" &&
            item.Tags["queue"] == WorkerTelemetry.IntegrationOutbox).Value);
        Assert.All(capture.Values.SelectMany(item => item.Tags), tag => Assert.Contains(tag.Key, new[] { "queue", "state" }));
    }

    [Fact]
    public void UnknownQueueNamesAreBoundedAndCallbacksNeedNoDatabaseOrScope()
    {
        var clock = new TestClock();
        using var services = BuildServices(clock);
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        telemetry.UpdateSnapshot(new PlatformJobOperations([], null)
        {
            Queues = [new("private-conversation-marker", 2, 0, 0, null, null)]
        }, Notifications());
        capture.Collect();
        Assert.Contains(capture.Values, item => item.Name == "makan.queue.items" && item.Tags["queue"] == "Other");
        Assert.All(capture.Values.SelectMany(item => item.Tags), tag => Assert.DoesNotContain("private-", tag.Value, StringComparison.Ordinal));
        Assert.Equal(1, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.available").Value);
    }

    [Fact]
    public async Task PublisherFailureMarksCacheStaleAndNeverLogsTheSourceException()
    {
        var clock = new TestClock();
        using var services = BuildServices(clock, failStore: true);
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        telemetry.UpdateSnapshot(Jobs(clock), Notifications());
        clock.Advance(TimeSpan.FromSeconds(20));
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        var logger = new RecordingLogger();
        using var publisher = new OperationalMetricsPublisher(services.GetRequiredService<IServiceScopeFactory>(), telemetry,
            new OperationalMetricsOptions(), clock, logger);
        Assert.False(await publisher.PublishOnceAsync(CancellationToken.None));
        capture.Collect();
        Assert.Equal(0, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.available").Value);
        Assert.Equal(20, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.age").Value);
        Assert.Equal(1, Assert.Single(capture.Values, item => item.Name == "makan.worker.failures").Value);
        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message => Assert.DoesNotContain("sql-secret-marker", message, StringComparison.Ordinal));
        Assert.Null(logger.Exception);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsAWorkerFailure()
    {
        var clock = new TestClock();
        using var services = BuildServices(clock, failStore: true);
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        using var publisher = new OperationalMetricsPublisher(services.GetRequiredService<IServiceScopeFactory>(), telemetry,
            new OperationalMetricsOptions(), clock, new RecordingLogger());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishOnceAsync(cancellation.Token));
        Assert.DoesNotContain(capture.Values, item => item.Name == "makan.worker.failures");
    }

    [Fact]
    public async Task ProviderNonCancellationExceptionAfterCallerCancellationStillPropagatesCancellation()
    {
        var clock = new TestClock();
        using var cancellation = new CancellationTokenSource();
        using var services = BuildServices(clock, failStore: true, beforeStoreFailure: cancellation.Cancel);
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        telemetry.UpdateSnapshot(Jobs(clock), Notifications());
        using var capture = new MetricCapture(services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
        var logger = new RecordingLogger();
        using var publisher = new OperationalMetricsPublisher(services.GetRequiredService<IServiceScopeFactory>(), telemetry,
            new OperationalMetricsOptions(), clock, logger);
        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishOnceAsync(cancellation.Token));
        capture.Collect();
        Assert.DoesNotContain(capture.Values, item => item.Name == "makan.worker.failures");
        Assert.Empty(logger.Messages);
        Assert.Equal(1, Assert.Single(capture.Values, item => item.Name == "makan.operational.snapshot.available").Value);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("30", 30)]
    [InlineData("3600", 3600)]
    public void SnapshotIntervalAcceptsOnlyTheConfiguredOperationalBounds(string value, int expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [OperationalMetricsOptions.ConfigurationKey] = value }).Build();
        Assert.Equal(expected, OperationalMetricsOptions.FromConfiguration(configuration).SnapshotIntervalSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("-1")]
    [InlineData("3601")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("private-config-marker")]
    public void InvalidSnapshotIntervalFailsWithNoConfiguredValueDisclosure(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [OperationalMetricsOptions.ConfigurationKey] = value }).Build();
        var exception = Assert.Throws<InvalidOperationException>(() => OperationalMetricsOptions.FromConfiguration(configuration));
        Assert.DoesNotContain("private-config-marker", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotIntervalDefaultsToThirtySeconds() =>
        Assert.Equal(30, OperationalMetricsOptions.FromConfiguration(new ConfigurationBuilder().Build()).SnapshotIntervalSeconds);

    private static ServiceProvider BuildServices(TestClock clock, bool failStore = false, Action? beforeStoreFailure = null)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<WorkerTelemetry>();
        if (failStore) services.AddScoped<IPlatformCommerceOperationsStore>(_ =>
        {
            beforeStoreFailure?.Invoke();
            throw new InvalidOperationException("sql-secret-marker");
        });
        return services.BuildServiceProvider();
    }

    private static PlatformJobOperations Jobs(TestClock clock) => new([], null)
    {
        Queues = [new(WorkerTelemetry.IntegrationOutbox, 3, 1, 2, clock.GetUtcNow().UtcDateTime.AddSeconds(-20), 20)]
    };

    private static PlatformNotificationOperations Notifications() => new(1, 2, 3, 4, 15, "Unavailable", "Unavailable", []);

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    private sealed class RecordingLogger : ILogger<OperationalMetricsPublisher>
    {
        public List<string> Messages { get; } = [];
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Messages.Add(formatter(state, exception)); Exception = exception; }
    }

    private sealed record Observation(string Name, double Value, Dictionary<string, string> Tags);
    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<Observation> Values { get; } = [];
        public MetricCapture(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            { if (ReferenceEquals(instrument.Meter, meter)) listener.EnableMeasurementEvents(instrument); };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Values.Add(new Observation(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value?.ToString() ?? ""))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Values.Add(new Observation(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value?.ToString() ?? ""))));
            _listener.Start();
        }
        public void Collect() => _listener.RecordObservableInstruments();
        public void Dispose() => _listener.Dispose();
    }
}
