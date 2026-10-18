using System.Diagnostics;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Storage;

public sealed class TerminalFileCleanupDispatcher(
    IServiceScopeFactory scopes,
    TerminalFileCleanupOptions options,
    WorkerTelemetry telemetry,
    ILogger<TerminalFileCleanupDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var deleted = 0;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.FileCleanup))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    deleted = (await scope.ServiceProvider.GetRequiredService<TerminalFileCleanupProcessor>()
                        .ProcessBatchAsync(stoppingToken)).Deleted;
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.FileCleanup, "Loop");
                    logger.LogWarning("حلقه پاک‌سازی فایل terminal ناموفق بود. Code={FailureCode}", "TERMINAL_FILE_CLEANUP_LOOP_FAILED");
                }
                finally { telemetry.RecordBatch(WorkerTelemetry.FileCleanup, deleted, started); }
            }
            await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), stoppingToken);
        }
    }
}
