using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MakanApp.Api.Hosting;

public sealed class CoreReadinessHealthCheck(
    MakanDbContext dbContext,
    IStorageReadinessProbe storageReadinessProbe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
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
}
