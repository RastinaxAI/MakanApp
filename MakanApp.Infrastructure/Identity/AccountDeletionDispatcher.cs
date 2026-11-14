using System.Diagnostics;
using MakanApp.Application.Identity;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Identity;

public sealed class AccountDeletionDispatcher(IServiceScopeFactory scopes, AccountDeletionOptions options,
    WorkerTelemetry telemetry, ILogger<AccountDeletionDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.ExecutionEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var count = 0; var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.AccountDeletion))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    count = await scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>().DispatchBatchAsync(stoppingToken);
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.AccountDeletion, "Loop");
                    logger.LogError("چرخهٔ حذف حساب ناموفق بود. Code={FailureCode}", "ACCOUNT_DELETION_LOOP_FAILED");
                }
                finally { telemetry.RecordBatch(WorkerTelemetry.AccountDeletion, count, started); }
            }
            if (count < AccountDeletionProcessor.BatchSize) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
