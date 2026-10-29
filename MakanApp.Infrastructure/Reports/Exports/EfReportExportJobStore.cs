using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Commerce;
using System.Data;
using System.Security.Cryptography;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed partial class EfReportExportJobStore(
    MakanDbContext dbContext,
    EfCommercialStore commerce,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<EfReportExportJobStore> logger) : IReportExportJobStore
{
    public const int MaximumRepairBatchSize = 100;

    public async Task<ReportExportJob> AddAsync(ReportExportJob job, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(job.RequestedByUserId), cancellationToken);
        await commerce.LockOwnerAsync(SubscriptionOwner.Organization(job.OrganizationId), cancellationToken);
        if (job.ClientOperationId.HasValue)
        {
            var existing = await GetByOperationAsync(job.RequestedByUserId, job.ClientOperationId.Value, cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestPayloadHash is null || job.RequestPayloadHash is null || !CryptographicOperations.FixedTimeEquals(existing.RequestPayloadHash, job.RequestPayloadHash))
                    throw new CommerceException(CommerceErrorCodes.UsageConflict, "شناسهٔ خروجی گزارش با درخواست دیگری استفاده شده است.");
                await transaction.CommitAsync(cancellationToken); return existing;
            }
        }
        if (await dbContext.Subscriptions.AnyAsync(s => s.OrganizationId == job.OrganizationId && s.CommercialControlEnabled, cancellationToken))
        {
            if (!job.ClientOperationId.HasValue || job.RequestPayloadHash is not { Length: 32 })
                throw new CommerceException(CommerceErrorCodes.UsageInvalid, "عملیات خروجی مدیریت‌شده به شناسهٔ معتبر نیاز دارد.");
            var reservation = await commerce.ReserveReportExportAsync(job.OrganizationId, job.RequestedByUserId,
                job.ClientOperationId.Value, job.RequestPayloadHash, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            if (reservation is not null) job.BindCommercialReservation(reservation.ReservationId);
        }
        dbContext.ReportExportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public Task<ReportExportJob?> GetByOperationAsync(Guid userId, Guid operationId, CancellationToken ct) => dbContext.ReportExportJobs.AsNoTracking()
        .SingleOrDefaultAsync(j => j.RequestedByUserId == userId && j.ClientOperationId == operationId, ct);

    public Task<ReportExportJob?> GetAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.ReportExportJobs.AsNoTracking().SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);

    public async Task<ReportExportJob?> TryClaimNextAsync(
        DateTime nowUtc,
        TimeSpan lease,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        await ReconcileTerminalReservationsAsync(cancellationToken);
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
            await ReconcileTerminalReservationsAsync(cancellationToken);
            return null;
        }

        var recoveredLease = job.Status == ReportExportJobStatus.Processing;
        var claimedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        job.Claim(claimedAtUtc, claimedAtUtc.Add(lease));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RecordRepairedFailures(repaired);
        await ReconcileTerminalReservationsAsync(cancellationToken);
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
        if (job.CommerceReservationId.HasValue && job.Status is ReportExportJobStatus.Completed or ReportExportJobStatus.Failed)
            await commerce.FinalizeAsync(job.RequestedByUserId, job.CommerceReservationId.Value,
                job.Status == ReportExportJobStatus.Completed ? UsageReservationStatus.Consumed : UsageReservationStatus.Released,
                job.Status == ReportExportJobStatus.Completed ? "report_completed" : "report_failed_fenced",
                timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
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

    private async Task ReconcileTerminalReservationsAsync(CancellationToken ct)
    {
        var terminal = await (from job in dbContext.ReportExportJobs.AsNoTracking()
            join reservation in dbContext.UsageReservations.AsNoTracking() on job.CommerceReservationId equals reservation.Id
            where (job.Status == ReportExportJobStatus.Completed || job.Status == ReportExportJobStatus.Failed)
                && (reservation.Status == UsageReservationStatus.Reserved || reservation.Status == UsageReservationStatus.Unknown)
            orderby job.RequestedAtUtc
            select new { reservation.Id, reservation.ConsumerUserId, job.Status }).Take(MaximumRepairBatchSize).ToArrayAsync(ct);
        foreach (var row in terminal)
            await commerce.FinalizeAsync(row.ConsumerUserId, row.Id,
                row.Status == ReportExportJobStatus.Completed ? UsageReservationStatus.Consumed : UsageReservationStatus.Released,
                row.Status == ReportExportJobStatus.Completed ? "report_completed_reconciled" : "report_failed_fenced_reconciled",
                timeProvider.GetUtcNow().UtcDateTime, ct);
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
