using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Security.Cryptography;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OperationalMetricsPublisherTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PublisherReadsRealSqlQueueStatesAndOnlyExportsAggregateMeasurements()
    {
        await using var isolated = await CreateScopeAsync();
        var services = isolated.Factory.Services;
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        var now = isolated.Factory.Clock.GetUtcNow().UtcDateTime;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.OutboxMessages.Add(OutboxMessage.Create(Guid.NewGuid(), "synthetic.metrics",
                "{\"private\":\"metrics-payload-secret-marker\"}", now.AddMinutes(-2)));
            db.OutboxMessages.Add(OutboxMessage.Create(Guid.NewGuid(), "synthetic.metrics", "{}", now.AddMinutes(-1)));
            var exhausted = OutboxMessage.Create(Guid.NewGuid(), "synthetic.metrics", "{}", now.AddHours(-1));
            db.OutboxMessages.Add(exhausted);
            db.Entry(exhausted).Property("AttemptCount").CurrentValue = OutboxProcessor.MaximumAttempts;
            await db.SaveChangesAsync();
        }

        var measurements = new List<Reading>();
        var meter = services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName);
        using var listener = Listen(meter, measurements);
        using var publisher = new OperationalMetricsPublisher(services.GetRequiredService<IServiceScopeFactory>(), telemetry,
            services.GetRequiredService<OperationalMetricsOptions>(), isolated.Factory.Clock, NullLogger<OperationalMetricsPublisher>.Instance);
        Assert.True(await publisher.PublishOnceAsync(CancellationToken.None));
        listener.RecordObservableInstruments();
        Assert.Equal(2, Assert.Single(measurements, item => item.Name == "makan.queue.items" &&
            item.Tags.GetValueOrDefault("queue") == WorkerTelemetry.IntegrationOutbox && item.Tags.GetValueOrDefault("state") == "Pending").Value);
        Assert.Equal(1, Assert.Single(measurements, item => item.Name == "makan.queue.items" &&
            item.Tags.GetValueOrDefault("queue") == WorkerTelemetry.IntegrationOutbox && item.Tags.GetValueOrDefault("state") == "Failed").Value);
        Assert.Equal(1, Assert.Single(measurements, item => item.Name == "makan.operational.snapshot.available").Value);
        Assert.True(Assert.Single(measurements, item => item.Name == "makan.queue.oldest_age" &&
            item.Tags.GetValueOrDefault("queue") == WorkerTelemetry.IntegrationOutbox).Value >= 120);
        Assert.All(measurements.SelectMany(item => item.Tags), tag =>
        {
            Assert.Contains(tag.Key, new[] { "worker", "stage", "queue", "state" });
            Assert.DoesNotContain("secret-marker", tag.Value, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CancellationInterruptsAnInFlightSqlSnapshotWithoutReportingFalseSuccess()
    {
        await using var isolated = await CreateScopeAsync();
        var services = isolated.Factory.Services;
        var telemetry = services.GetRequiredService<WorkerTelemetry>();
        await using var blockerScope = services.CreateAsyncScope();
        var db = blockerScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var snapshotEnabled = await db.Database.SqlQueryRaw<bool>(
            "SELECT CAST([is_read_committed_snapshot_on] AS bit) AS [Value] FROM [sys].[databases] WHERE [name] = DB_NAME()")
            .SingleAsync();
        output.WriteLine("READ_COMMITTED_SNAPSHOT={0}", snapshotEnabled);
        var connection = (SqlConnection)db.Database.GetDbConnection();
        Assert.StartsWith("MakanApp_Recovery_PH3_", connection.Database, StringComparison.Ordinal);
        // قفل داده در RCSI خواندن را متوقف نمی‌کند؛ Sch-M فقط در DB مصنوعی و تا rollback نگه داشته می‌شود.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE [platform].[OutboxMessages] ADD [Ph3CancellationBarrier] int NULL");
        using var publisher = new OperationalMetricsPublisher(services.GetRequiredService<IServiceScopeFactory>(), telemetry,
            services.GetRequiredService<OperationalMetricsOptions>(), isolated.Factory.Clock, NullLogger<OperationalMetricsPublisher>.Instance);
        using var cancellation = new CancellationTokenSource();
        var publishing = publisher.PublishOnceAsync(cancellation.Token);
        try
        {
            Assert.True(await WaitForBlockedSqlReaderAsync(connection, connection.ServerProcessId, publishing),
                "انتظار واقعی SQL برای Sch-S در دیتابیس ایزوله مشاهده نشد.");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await transaction.RollbackAsync();
            }
            finally
            {
                // تکمیل task را پیش از حذف DB مشاهده می‌کنیم؛ خطای cleanup جای شکست اصلی تست را نمی‌گیرد.
                _ = await Record.ExceptionAsync(() => publishing.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
    }

    private static async Task<bool> WaitForBlockedSqlReaderAsync(SqlConnection blocker, int blockerSessionId, Task publishing)
    {
        var settings = new SqlConnectionStringBuilder(blocker.ConnectionString) { ConnectTimeout = 5, Pooling = false };
        await using var observer = new SqlConnection(settings.ConnectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await observer.OpenAsync(timeout.Token);
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5) && !publishing.IsCompleted)
        {
            await using var command = observer.CreateCommand();
            command.CommandTimeout = 1;
            command.CommandText = """
                SELECT COUNT_BIG(*) FROM [sys].[dm_exec_requests]
                WHERE [database_id] = DB_ID()
                  AND [blocking_session_id] = @blocker
                  AND [wait_type] = N'LCK_M_SCH_S'
                """;
            command.Parameters.AddWithValue("@blocker", blockerSessionId);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0) return true;
            await Task.Delay(20, timeout.Token);
        }
        return false;
    }

    private static MeterListener Listen(Meter meter, List<Reading> readings)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, target) =>
        { if (ReferenceEquals(instrument.Meter, meter)) target.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            readings.Add(new Reading(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value?.ToString() ?? ""))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            readings.Add(new Reading(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value?.ToString() ?? ""))));
        listener.Start();
        return listener;
    }

    private static async Task<MetricsScope> CreateScopeAsync()
    {
        var runId = Guid.NewGuid().ToString("N");
        var factory = new MakanAppWebApplicationFactory("MakanApp_Recovery_PH3_" + runId,
            Path.Combine(Path.GetTempPath(), "MakanApp-StorageTests", "PH3-" + runId),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        try
        {
            await ((IAsyncLifetime)factory).InitializeAsync();
            return new MetricsScope(factory);
        }
        catch
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
            throw;
        }
    }

    private sealed record Reading(string Name, double Value, Dictionary<string, string> Tags);
    private sealed class MetricsScope(MakanAppWebApplicationFactory factory) : IAsyncDisposable
    {
        public MakanAppWebApplicationFactory Factory => factory;
        public async ValueTask DisposeAsync() => await ((IAsyncLifetime)factory).DisposeAsync();
    }
}
