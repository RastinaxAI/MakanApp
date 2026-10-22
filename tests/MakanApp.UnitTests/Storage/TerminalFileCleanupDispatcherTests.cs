using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Storage;

public sealed class TerminalFileCleanupDispatcherTests
{
    private const string SensitiveMarker = "synthetic-private-terminal-cleanup-provider-error";

    [Fact]
    public async Task DisabledWorkerDoesNotCreateScopesOrTouchProcessorAndStopsWithinBudget()
    {
        await using var fixture = new DispatcherFixture(false, FailurePoint.CreateScope);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await fixture.Dispatcher.StartAsync(deadline.Token);
        await fixture.Dispatcher.ExecuteTask!.WaitAsync(deadline.Token);
        await fixture.Dispatcher.StopAsync(deadline.Token);

        Assert.True(fixture.Dispatcher.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(0, fixture.Scopes.Created);
        Assert.Equal(0, fixture.Scopes.Resolutions);
        Assert.Equal(0, fixture.Scopes.Disposed);
        Assert.Empty(fixture.Logs.Entries);
        Assert.Empty(fixture.Measurements.Values);
    }

    [Theory]
    [InlineData(FailurePoint.CreateScope)]
    [InlineData(FailurePoint.ResolveProcessor)]
    public async Task FailureIsVisibleAndSafeWithoutHotLoopAndStopInterruptsPolling(FailurePoint failurePoint)
    {
        await using var fixture = new DispatcherFixture(true, failurePoint);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await fixture.Dispatcher.StartAsync(deadline.Token);
        await fixture.Logs.FirstWarning.Task.WaitAsync(deadline.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
        Assert.Equal(1, fixture.Scopes.Created);
        await fixture.Dispatcher.StopAsync(deadline.Token);

        Assert.True(fixture.Dispatcher.ExecuteTask!.IsCompleted);
        Assert.False(fixture.Dispatcher.ExecuteTask.IsFaulted);
        Assert.Equal(1, fixture.Scopes.Created);
        Assert.Equal(failurePoint == FailurePoint.ResolveProcessor ? 1 : 0, fixture.Scopes.Resolutions);
        Assert.Equal(failurePoint == FailurePoint.ResolveProcessor ? 1 : 0, fixture.Scopes.Disposed);
        var failure = Assert.Single(fixture.Measurements.Values,
            value => value.Name == "makan.worker.failures");
        Assert.Equal(1, failure.Value);
        Assert.Equal(WorkerTelemetry.FileCleanup, failure.Tags["worker"]);
        Assert.Equal("Loop", failure.Tags["stage"]);
        var batch = Assert.Single(fixture.Measurements.Values,
            value => value.Name == "makan.worker.batches");
        Assert.Equal(1, batch.Value);
        Assert.Equal(WorkerTelemetry.FileCleanup, batch.Tags["worker"]);
        Assert.DoesNotContain(fixture.Measurements.Values,
            value => value.Name == "makan.file_cleanup.results");
        var log = Assert.Single(fixture.Logs.Entries);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Null(log.Exception);
        Assert.Contains("TERMINAL_FILE_CLEANUP_LOOP_FAILED", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SensitiveMarker, log.Message, StringComparison.Ordinal);
        Assert.All(log.Values, value =>
            Assert.DoesNotContain(SensitiveMarker, value.Value?.ToString() ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CleanupCounterNormalizesUnknownOutcomesAndIgnoresNonPositiveCounts()
    {
        await using var fixture = new DispatcherFixture(false, FailurePoint.CreateScope);

        fixture.Telemetry.RecordCleanup(SensitiveMarker, 1);
        fixture.Telemetry.RecordCleanup("Deleted", 0);
        fixture.Telemetry.RecordCleanup("Deleted", -1);

        var measurement = Assert.Single(fixture.Measurements.Values);
        Assert.Equal("makan.file_cleanup.results", measurement.Name);
        Assert.Equal(1, measurement.Value);
        var tag = Assert.Single(measurement.Tags);
        Assert.Equal("outcome", tag.Key);
        Assert.Equal(WorkerTelemetry.Other, tag.Value);
        Assert.DoesNotContain(SensitiveMarker, tag.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Scopes.Created);
        Assert.Empty(fixture.Logs.Entries);
    }

    public enum FailurePoint { CreateScope, ResolveProcessor }

    private sealed class DispatcherFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        public ControlledScopeFactory Scopes { get; }
        public WorkerTelemetry Telemetry { get; }
        public CaptureLogger Logs { get; } = new();
        public MeasurementCapture Measurements { get; }
        public TerminalFileCleanupDispatcher Dispatcher { get; }

        public DispatcherFixture(bool enabled, FailurePoint failurePoint)
        {
            var registrations = new ServiceCollection();
            registrations.AddMetrics();
            registrations.AddSingleton(TimeProvider.System);
            registrations.AddSingleton<WorkerTelemetry>();
            _services = registrations.BuildServiceProvider();
            Telemetry = _services.GetRequiredService<WorkerTelemetry>();
            Measurements = new MeasurementCapture(_services.GetRequiredService<IMeterFactory>()
                .Create(WorkerTelemetry.MeterName));
            Scopes = new ControlledScopeFactory(failurePoint);
            var options = new TerminalFileCleanupOptions
            {
                Enabled = enabled,
                GracePeriodHours = enabled ? 1 : null,
                IntervalSeconds = 5
            };
            options.Validate();
            Dispatcher = new TerminalFileCleanupDispatcher(Scopes, options, Telemetry, Logs);
        }

        public async ValueTask DisposeAsync()
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await Dispatcher.StopAsync(shutdown.Token); }
            finally
            {
                Dispatcher.Dispose();
                Measurements.Dispose();
                await _services.DisposeAsync();
            }
        }
    }

    private sealed class ControlledScopeFactory(FailurePoint failurePoint) : IServiceScopeFactory
    {
        private int _created;
        private int _resolutions;
        private int _disposed;
        public int Created => Volatile.Read(ref _created);
        public int Resolutions => Volatile.Read(ref _resolutions);
        public int Disposed => Volatile.Read(ref _disposed);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _created);
            if (failurePoint == FailurePoint.CreateScope) throw new IOException(SensitiveMarker);
            return new ControlledScope(this);
        }

        private sealed class ControlledScope(ControlledScopeFactory owner) : IServiceScope, IServiceProvider, IAsyncDisposable
        {
            public IServiceProvider ServiceProvider => this;
            public object? GetService(Type serviceType)
            {
                Assert.Equal(typeof(TerminalFileCleanupProcessor), serviceType);
                Interlocked.Increment(ref owner._resolutions);
                throw new IOException(SensitiveMarker);
            }

            public void Dispose() => Interlocked.Increment(ref owner._disposed);
            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
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
                Values.Enqueue(new(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
            _listener.Start();
        }
        public void Dispose() => _listener.Dispose();
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception,
        IReadOnlyList<KeyValuePair<string, object?>> Values);
    private sealed class CaptureLogger : ILogger<TerminalFileCleanupDispatcher>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public TaskCompletionSource FirstWarning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> properties ? properties.ToArray() : [];
            Entries.Enqueue(new(logLevel, formatter(state, exception), exception, values));
            if (logLevel >= LogLevel.Warning) FirstWarning.TrySetResult();
        }
    }
}
