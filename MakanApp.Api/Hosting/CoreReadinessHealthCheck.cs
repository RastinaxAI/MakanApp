using MakanApp.Api.Configuration;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MakanApp.Api.Hosting;

public sealed class CoreReadinessHealthCheck(
    MakanDbContext dbContext,
    IStorageReadinessProbe storageReadinessProbe,
    IConfiguration configuration,
    IHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ProductionConfigurationValidator.Validate(configuration, environment.EnvironmentName);
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("SQL Server در دسترس نیست.");
            }

            if (!await storageReadinessProbe.IsReadyAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Storage پایدار در دسترس نیست.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // exception فنی وارد HealthCheckResult و logger داخلی framework نمی‌شود.
            return HealthCheckResult.Unhealthy("یکی از وابستگی‌های اصلی در دسترس نیست.");
        }
    }
}
