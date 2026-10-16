using MakanApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingEphemeralDispatcher(IServiceScopeFactory scopes, ILogger<MessagingEphemeralDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MessagingEphemeralService>().FlushAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("انتشار وضعیت گذرا ناموفق بود؛ اعتبار آن فقط تا پایان TTL است."); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
