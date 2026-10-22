using System.Collections.Concurrent;
using MakanApp.Application.Notification;
using MakanApp.Application.Reports.Exports;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Reports.Exports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Operations;

public sealed class WorkerDispatcherLogSafetyTests
{
    private const string SensitiveMarker = "private-otp-jwt-sql-storage-provider-exception-marker";

    [Theory]
    [InlineData(typeof(OutboxDispatcher))]
    [InlineData(typeof(MessagingRealtimeOutboxDispatcher))]
    [InlineData(typeof(NotificationDeliveryDispatcher))]
    [InlineData(typeof(ReportExportDispatcher))]
    [InlineData(typeof(ScheduledMessageDispatcher))]
    [InlineData(typeof(MessagingEphemeralDispatcher))]
    public async Task WorkerFailureRemainsVisibleWithoutRawExceptionAndShutdownCompletes(Type dispatcherType)
    {
        using var logs = new CapturingLoggerProvider();
        var registrations = new ServiceCollection();
        registrations.AddLogging(builder => builder.ClearProviders().AddProvider(logs));
        registrations.AddMetrics();
        registrations.AddSingleton(TimeProvider.System);
        registrations.AddSingleton<WorkerTelemetry>();
        registrations.AddSingleton<MessagingOutboxWakeSignal>();
        registrations.AddSingleton<IReportExportQueueSignal, ReportExportQueueSignal>();
        registrations.AddSingleton(new ReportExportOptions());
        registrations.AddSingleton(new NotificationDeliveryOptions());
        await using var services = registrations.BuildServiceProvider();
        using var dispatcher = (BackgroundService)ActivatorUtilities.CreateInstance(
            services,
            dispatcherType,
            new FailingScopeFactory());
        try
        {
            await dispatcher.StartAsync(CancellationToken.None);
            await logs.FirstFailure.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await dispatcher.StopAsync(shutdown.Token);
        }

        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(SensitiveMarker, entry.Message, StringComparison.Ordinal);
        });
        Assert.Contains(logs.Entries, entry => entry.Level >= LogLevel.Warning && entry.Message.Contains("Code=", StringComparison.Ordinal));
        Assert.True(dispatcher.ExecuteTask!.IsCompleted);
    }

    private sealed class FailingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException(SensitiveMarker);
    }

    private sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLog> Entries { get; } = new();
        public TaskCompletionSource FirstFailure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);
        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                owner.Entries.Enqueue(new(logLevel, formatter(state, exception), exception));
                if (logLevel >= LogLevel.Warning) owner.FirstFailure.TrySetResult();
            }
        }
    }
}
