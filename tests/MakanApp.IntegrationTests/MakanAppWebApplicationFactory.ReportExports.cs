using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Reports.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<int> ProcessAllReportExportsAsync(int maximumJobs = 20)
    {
        var processed = 0;
        while (processed < maximumJobs)
        {
            await using var scope = Services.CreateAsyncScope();
            var didProcess = await scope.ServiceProvider
                .GetRequiredService<ReportExportProcessor>()
                .ProcessNextAsync(CancellationToken.None);
            if (!didProcess)
            {
                break;
            }

            processed++;
        }

        return processed;
    }

    public async Task<ReportExportJob> GetReportExportJobAsync(Guid jobId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.ReportExportJobs.AsNoTracking().SingleAsync(job => job.Id == jobId);
    }

    public async Task<(int Claims, IReadOnlyCollection<Guid> ClaimedJobIds)> ClaimReportExportsConcurrentlyAsync()
    {
        async Task<ReportExportJob?> ClaimAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IReportExportJobStore>();
            return await store.TryClaimNextAsync(
                Clock.GetUtcNow().UtcDateTime,
                TimeSpan.FromMinutes(2),
                3,
                CancellationToken.None);
        }

        var jobs = await Task.WhenAll(ClaimAsync(), ClaimAsync());
        var claimed = jobs.Where(job => job is not null).Select(job => job!.Id).ToArray();
        return (claimed.Length, claimed);
    }

    public async Task<int> CountReportOutputFilesAsync(Guid jobId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.FileAssets.CountAsync(file => file.Id == jobId);
    }
}
