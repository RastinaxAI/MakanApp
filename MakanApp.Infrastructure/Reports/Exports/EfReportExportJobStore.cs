using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class EfReportExportJobStore(MakanDbContext dbContext) : IReportExportJobStore
{
    public async Task AddAsync(ReportExportJob job, CancellationToken cancellationToken)
    {
        dbContext.ReportExportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ReportExportJob?> GetAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.ReportExportJobs.AsNoTracking().SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);

    public async Task<ReportExportJob?> TryClaimNextAsync(
        DateTime nowUtc,
        TimeSpan lease,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [reports].[ReportExportJobs]
            SET [Status] = {(int)ReportExportJobStatus.Failed},
                [FailedAtUtc] = {nowUtc},
                [FailureCode] = {ReportExportErrorCodes.GenerationFailed},
                [LeaseExpiresAtUtc] = NULL,
                [NextAttemptAtUtc] = NULL
            WHERE [Status] = {(int)ReportExportJobStatus.Processing}
              AND [LeaseExpiresAtUtc] <= {nowUtc}
              AND [AttemptCount] >= {maximumAttempts}
            """, cancellationToken);

        var job = await dbContext.ReportExportJobs
            .FromSqlInterpolated($"""
                SELECT TOP (1) *
                FROM [reports].[ReportExportJobs] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [AttemptCount] < {maximumAttempts}
                  AND (([Status] = {(int)ReportExportJobStatus.Queued}
                        AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc}))
                       OR ([Status] = {(int)ReportExportJobStatus.Processing}
                           AND [LeaseExpiresAtUtc] <= {nowUtc}))
                ORDER BY [RequestedAtUtc], [Id]
                """)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.Claim(nowUtc, nowUtc.Add(lease));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
