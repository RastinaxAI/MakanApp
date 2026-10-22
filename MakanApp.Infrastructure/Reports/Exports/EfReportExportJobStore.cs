using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class EfReportExportJobStore(
    MakanDbContext dbContext,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<EfReportExportJobStore> logger) : IReportExportJobStore
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
        var repaired = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
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
            RecordRepairedFailures(repaired);
            return null;
        }

        var recoveredLease = job.Status == ReportExportJobStatus.Processing;
        var claimedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        job.Claim(claimedAtUtc, claimedAtUtc.Add(lease));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RecordRepairedFailures(repaired);
        telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "Claimed");
        if (recoveredLease) telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "LeaseRecovered");
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
            telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "LeaseLost");
            return false;
        }

        dbContext.Entry(job).Property(item => item.RowVersion).OriginalValue = expectedClaimRowVersion;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // فقط transition دارای acknowledgement موفق شمرده می‌شود؛ خطای Commit ممکن است نتیجهٔ نامعلوم داشته باشد.
        switch (job.Status)
        {
            case ReportExportJobStatus.Completed:
                telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "Completed");
                break;
            case ReportExportJobStatus.Queued:
                telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "RetryScheduled");
                break;
            case ReportExportJobStatus.Failed:
                telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "Failed");
                logger.LogError("job خروجی گزارش به شکست نهایی رسید. Code={FailureCode}", "REPORT_EXPORT_TERMINAL_FAILED");
                break;
        }
        return true;
    }

    private void RecordRepairedFailures(int count)
    {
        if (count <= 0) return;
        telemetry.RecordOutcome(WorkerTelemetry.ReportExport, "Failed", count);
        logger.LogError("تلاش نهایی رهاشدهٔ خروجی گزارش terminal شد. Count={Count}; Code={FailureCode}",
            count, "REPORT_EXPORT_EXHAUSTED_REPAIR");
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
