using System.Diagnostics;
using MakanApp.Application.Messaging;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class ScheduledMessageDispatcher(IServiceScopeFactory scopes, WorkerTelemetry telemetry, ILogger<ScheduledMessageDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.ScheduledMessage))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    processed = await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(stoppingToken);
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.ScheduledMessage, "Loop");
                    logger.LogError("پردازش پیام زمان‌بندی‌شده ناموفق بود؛ کار پایدار پس از پایان lease قابل بازیابی است. Code={FailureCode}", "SCHEDULED_MESSAGE_LOOP_FAILED");
                }
                finally
                {
                    telemetry.RecordBatch(WorkerTelemetry.ScheduledMessage, processed, started);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
