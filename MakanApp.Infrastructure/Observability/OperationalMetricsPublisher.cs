using System.Diagnostics;
using MakanApp.Application.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Observability;

public sealed class OperationalMetricsPublisher(
    IServiceScopeFactory scopeFactory,
    WorkerTelemetry telemetry,
    OperationalMetricsOptions options,
    TimeProvider timeProvider,
    ILogger<OperationalMetricsPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(options.SnapshotIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task<bool> PublishOnceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = telemetry.BeginBatch(WorkerTelemetry.OperationalMetrics);
        var started = Stopwatch.GetTimestamp();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(OperationalMetricsOptions.PublishBudgetSeconds));
        var observedQueues = 0;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IPlatformCommerceOperationsStore>();
            var jobs = await store.GetJobOperationsAsync(budget.Token);
            var notifications = await store.GetNotificationOperationsAsync(timeProvider.GetUtcNow().UtcDateTime, budget.Token);
            telemetry.UpdateSnapshot(jobs, notifications);
            observedQueues = jobs.Queues.Count + 1;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            telemetry.MarkSnapshotUnavailable();
            telemetry.RecordFailure(WorkerTelemetry.OperationalMetrics, "Snapshot");
            logger.LogWarning("به‌روزرسانی snapshot عملیاتی ناموفق بود؛ آخرین داده با وضعیت stale حفظ شد.");
            return false;
        }
        finally
        {
            telemetry.RecordBatch(WorkerTelemetry.OperationalMetrics, observedQueues, started);
        }
    }
}
