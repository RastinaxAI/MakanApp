using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class EfReportExportJobStore(MakanDbContext dbContext, TimeProvider timeProvider) : IReportExportJobStore
{
    public const int MaximumRepairBatchSize = 100;

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
            UPDATE TOP ({MaximumRepairBatchSize}) [reports].[ReportExportJobs] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
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
                FROM [reports].[ReportExportJobs] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
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

        var claimedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        job.Claim(claimedAtUtc, claimedAtUtc.Add(lease));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveClaimChangesAsync(
        ReportExportJob job,
        byte[] expectedClaimRowVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var current = await ReportExportClaimLock.GetAsync(dbContext, job.Id, cancellationToken);
        if (!ReportExportClaimLock.IsCurrent(current, expectedClaimRowVersion, timeProvider.GetUtcNow().UtcDateTime))
        {
            await transaction.CommitAsync(cancellationToken);
            dbContext.Entry(job).State = EntityState.Detached;
            return false;
        }

        dbContext.Entry(job).Property(item => item.RowVersion).OriginalValue = expectedClaimRowVersion;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}

// خواندن مستقل از ChangeTracker لازم است؛ job محلی ممکن است قبلاً Completed شده باشد.
internal static class ReportExportClaimLock
{
    internal static Task<ReportExportJob?> GetAsync(MakanDbContext dbContext, Guid jobId, CancellationToken cancellationToken) =>
        dbContext.ReportExportJobs.FromSqlInterpolated($"""
            SELECT * FROM [reports].[ReportExportJobs] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = {jobId}
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    internal static bool IsCurrent(ReportExportJob? current, byte[] expectedClaimRowVersion, DateTime nowUtc) =>
        current is { Status: ReportExportJobStatus.Processing, LeaseExpiresAtUtc: not null } &&
        current.LeaseExpiresAtUtc > nowUtc && expectedClaimRowVersion.Length > 0 &&
        current.RowVersion.AsSpan().SequenceEqual(expectedClaimRowVersion);
}
