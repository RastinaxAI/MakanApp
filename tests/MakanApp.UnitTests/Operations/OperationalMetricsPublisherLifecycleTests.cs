using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using MakanApp.Application.Platform;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Operations;

public sealed class OperationalMetricsPublisherLifecycleTests
{
    [Fact]
    public async Task StopDuringPollingCompletesWithoutASecondSnapshotOrFalseFailure()
    {
        var store = new ControlledSnapshotStore(StoreBehavior.Success);
        await using var fixture = new PublisherFixture(store);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await fixture.Publisher.StartAsync(deadline.Token);
        await store.QueryEntered.Task.WaitAsync(deadline.Token);
        await fixture.Publisher.StopAsync(deadline.Token);

        Assert.True(fixture.Publisher.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(1, store.JobReads);
        Assert.Equal(1, store.NotificationReads);
        Assert.Equal(1, store.DisposedScopes);
        Assert.Empty(fixture.Logs.Entries);
        Assert.DoesNotContain(fixture.Measurements.Values, value => value.Name == "makan.worker.failures");
        fixture.Measurements.Collect();
        Assert.Equal(1, Assert.Single(fixture.Measurements.Values,
            value => value.Name == "makan.operational.snapshot.available").Value);
    }

    [Theory]
    [InlineData(StoreBehavior.BlockThenCancel)]
    [InlineData(StoreBehavior.BlockThenNonCancellationFailure)]
    public async Task StopDuringActiveRefreshPreservesCancellationAndDoesNotMarkPreviousSnapshotFailed(StoreBehavior behavior)
    {
        var store = new ControlledSnapshotStore(behavior);
        await using var fixture = new PublisherFixture(store);
        fixture.Telemetry.UpdateSnapshot(new PlatformJobOperations([], null), Notifications());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await fixture.Publisher.StartAsync(deadline.Token);
        await store.QueryEntered.Task.WaitAsync(deadline.Token);
        await fixture.Publisher.StopAsync(deadline.Token);

        Assert.True(fixture.Publisher.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(1, store.JobReads);
        Assert.Equal(0, store.NotificationReads);
        Assert.Equal(1, store.DisposedScopes);
        Assert.Empty(fixture.Logs.Entries);
        Assert.DoesNotContain(fixture.Measurements.Values, value => value.Name == "makan.worker.failures");
        fixture.Measurements.Collect();
        Assert.Equal(1, Assert.Single(fixture.Measurements.Values,
            value => value.Name == "makan.operational.snapshot.available").Value);
    }

    [Fact]
    public async Task FailedRefreshRemainsVisibleAndBoundedWithoutLeakingExceptionOrBlockingShutdown()
    {
        var store = new ControlledSnapshotStore(StoreBehavior.Failure);
        await using var fixture = new PublisherFixture(store);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await fixture.Publisher.StartAsync(deadline.Token);
        await fixture.Logs.FirstWarning.Task.WaitAsync(deadline.Token);
        await fixture.Publisher.StopAsync(deadline.Token);

        Assert.True(fixture.Publisher.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(1, store.JobReads);
        Assert.Equal(0, store.NotificationReads);
        Assert.Equal(1, store.DisposedScopes);
        var failure = Assert.Single(fixture.Measurements.Values, value => value.Name == "makan.worker.failures");
        Assert.Equal(1, failure.Value);
        Assert.Equal("OperationalMetrics", failure.Tags["worker"]);
        Assert.Equal("Snapshot", failure.Tags["stage"]);
        Assert.NotEmpty(fixture.Logs.Entries);
        Assert.All(fixture.Logs.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(ControlledSnapshotStore.SensitiveMarker, entry.Message, StringComparison.Ordinal);
        });
        fixture.Measurements.Collect();
        Assert.Equal(0, Assert.Single(fixture.Measurements.Values,
            value => value.Name == "makan.operational.snapshot.available").Value);
    }

    public enum StoreBehavior { Success, Failure, BlockThenCancel, BlockThenNonCancellationFailure }

    private static PlatformNotificationOperations Notifications() => new(0, 0, 0, 0, null, "Unavailable", "Unavailable", []);

    private sealed class PublisherFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        public WorkerTelemetry Telemetry { get; }
        public LifecycleLogger Logs { get; } = new();
        public MeasurementCapture Measurements { get; }
        public OperationalMetricsPublisher Publisher { get; }

        public PublisherFixture(ControlledSnapshotStore store)
        {
            var registrations = new ServiceCollection();
            registrations.AddMetrics();
            registrations.AddSingleton(TimeProvider.System);
            registrations.AddSingleton<WorkerTelemetry>();
            registrations.AddScoped<IPlatformCommerceOperationsStore>(_ => store);
            _services = registrations.BuildServiceProvider();
            Telemetry = _services.GetRequiredService<WorkerTelemetry>();
            Measurements = new MeasurementCapture(_services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName));
            Publisher = new OperationalMetricsPublisher(_services.GetRequiredService<IServiceScopeFactory>(),
                Telemetry, new OperationalMetricsOptions { SnapshotIntervalSeconds = 5 }, TimeProvider.System, Logs);
        }

        public async ValueTask DisposeAsync()
        {
            // حتی شکست assertion نیز worker یا scope در حال اجرا برای تست بعدی باقی نمی‌گذارد.
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await Publisher.StopAsync(shutdown.Token); }
            finally
            {
                Publisher.Dispose();
                Measurements.Dispose();
                await _services.DisposeAsync();
            }
        }
    }

    private sealed record Measurement(string Name, long Value, IReadOnlyDictionary<string, object?> Tags);
    private sealed class MeasurementCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public ConcurrentQueue<Measurement> Values { get; } = new();
        public MeasurementCapture(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, meter)) listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Values.Enqueue(new Measurement(instrument.Name, value,
                    tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
            _listener.Start();
        }
        public void Collect() => _listener.RecordObservableInstruments();
        public void Dispose() => _listener.Dispose();
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
    private sealed class LifecycleLogger : ILogger<OperationalMetricsPublisher>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public TaskCompletionSource FirstWarning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(new(logLevel, formatter(state, exception), exception));
            if (logLevel >= LogLevel.Warning) FirstWarning.TrySetResult();
        }
    }

    private sealed class ControlledSnapshotStore(StoreBehavior behavior) : IPlatformCommerceOperationsStore, IDisposable
    {
        public const string SensitiveMarker = "synthetic-private-sql-provider-lifecycle-marker";
        private int _jobReads;
        private int _notificationReads;
        private int _disposedScopes;
        public int JobReads => Volatile.Read(ref _jobReads);
        public int NotificationReads => Volatile.Read(ref _notificationReads);
        public int DisposedScopes => Volatile.Read(ref _disposedScopes);
        public TaskCompletionSource QueryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<PlatformJobOperations> GetJobOperationsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _jobReads);
            QueryEntered.TrySetResult();
            if (behavior == StoreBehavior.Failure) throw new IOException(SensitiveMarker);
            if (behavior is StoreBehavior.BlockThenCancel or StoreBehavior.BlockThenNonCancellationFailure)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    if (behavior == StoreBehavior.BlockThenNonCancellationFailure) throw new IOException(SensitiveMarker);
                    throw;
                }
            }
            return new PlatformJobOperations([], null);
        }

        public Task<PlatformNotificationOperations> GetNotificationOperationsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _notificationReads);
            return Task.FromResult(Notifications());
        }

        public void Dispose() => Interlocked.Increment(ref _disposedScopes);
        public Task<PlatformPlanPage> GetPlansAsync(PlatformPlanQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformPlanDetail?> GetPlanAsync(Guid planId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformPlanDetail> CreatePlanAsync(PlatformActorContext actor, CreatePlatformPlanCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformPlanVersionItem> CreatePlanVersionAsync(PlatformActorContext actor, Guid planId, CreatePlatformPlanVersionCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformPlanVersionItem> PublishPlanVersionAsync(PlatformActorContext actor, Guid planVersionId, byte[] expectedVersion, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformSubscriptionPage> GetSubscriptionsAsync(PlatformSubscriptionQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformSubscriptionOverrideResult> OverrideSubscriptionAsync(PlatformActorContext actor, Guid organizationId, OverridePlatformSubscriptionCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformOrderPage> GetOrdersAsync(PlatformOrderQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformOrderItem?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformSystemHealth> GetSystemHealthAsync(DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
