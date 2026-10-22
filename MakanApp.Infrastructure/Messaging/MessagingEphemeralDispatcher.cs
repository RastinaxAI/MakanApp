using System.Diagnostics;
using MakanApp.Application.Messaging;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingEphemeralDispatcher(IServiceScopeFactory scopes, WorkerTelemetry telemetry, ILogger<MessagingEphemeralDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.MessagingEphemeral))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<MessagingEphemeralService>().FlushAsync(stoppingToken);
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.MessagingEphemeral, "Loop");
                    logger.LogWarning("انتشار وضعیت گذرا ناموفق بود؛ اعتبار آن فقط تا پایان TTL است. Code={FailureCode}", "MESSAGING_EPHEMERAL_LOOP_FAILED");
                }
                finally
                {
                    // تعداد hint خروجی در قرارداد Flush وجود ندارد؛ عدد ساختگی برای آن ثبت نمی‌شود.
                    telemetry.RecordBatch(WorkerTelemetry.MessagingEphemeral, 0, started);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
