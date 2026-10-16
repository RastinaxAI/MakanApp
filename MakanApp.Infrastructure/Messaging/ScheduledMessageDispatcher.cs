using MakanApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class ScheduledMessageDispatcher(IServiceScopeFactory scopes, ILogger<ScheduledMessageDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("پردازش پیام زمان‌بندی‌شده ناموفق بود؛ کار پایدار پس از پایان lease قابل بازیابی است."); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
