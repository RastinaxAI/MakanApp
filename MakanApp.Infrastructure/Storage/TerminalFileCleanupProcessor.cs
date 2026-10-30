using System.Data;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Storage;

public sealed record TerminalFileCleanupBatchResult(int Examined, int Deleted, int AlreadyMissing, int Skipped, int Failed);

public sealed class TerminalFileCleanupProcessor(
    MakanDbContext dbContext,
    IFileStorage fileStorage,
    TerminalFileCleanupOptions options,
    TerminalFileCleanupCursor cursor,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<TerminalFileCleanupProcessor> logger)
{
    public static readonly TimeSpan PerAssetTimeout = TimeSpan.FromSeconds(10);

    public async Task<TerminalFileCleanupBatchResult> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        if (!options.Enabled) return new(0, 0, 0, 0, 0);
        if (fileStorage is not LocalFileStorage localStorage)
            throw new InvalidOperationException("پاک‌سازی terminal فقط با مخزن محلی پیکربندی‌شده مجاز است.");

        var examined = 0;
        var deleted = 0;
        var missing = 0;
        var skipped = 0;
        var failed = 0;
        await cursor.Gate.WaitAsync(cancellationToken);
        try
        {
            var cutoff = CutoffUtc();
            var status = cursor.NextStatus;
            cursor.NextStatus = status == 3 ? 4 : 3;
            var previous = cursor.Positions.GetValueOrDefault(status);
            var candidates = previous is null
                ? dbContext.FileAssets.FromSqlInterpolated($"""
                    SELECT TOP ({options.BatchSize}) * FROM [storage].[FileAssets]
                    WHERE [Status] = {status} AND [CreatedAtUtc] <= {cutoff}
                    ORDER BY [CreatedAtUtc], [Id]
                    """)
                : dbContext.FileAssets.FromSqlInterpolated($"""
                    SELECT TOP ({options.BatchSize}) * FROM [storage].[FileAssets]
                    WHERE [Status] = {status} AND [CreatedAtUtc] <= {cutoff}
                      AND ([CreatedAtUtc] > {previous.CreatedAtUtc}
                           OR ([CreatedAtUtc] = {previous.CreatedAtUtc} AND [Id] > {previous.Id}))
                    ORDER BY [CreatedAtUtc], [Id]
                    """);
            var rows = await candidates.AsNoTracking().ToArrayAsync(cancellationToken);
            if (rows.Length == 0) cursor.Positions.Remove(status);
            foreach (var candidate in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;
                try
                {
                    using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    budget.CancelAfter(PerAssetTimeout);
                    var outcome = await ProcessAssetAsync(localStorage, candidate.Id, budget.Token);
                    if (outcome == "Deleted") deleted++;
                    else if (outcome == "AlreadyMissing") missing++;
                    else skipped++;
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                catch (Exception)
                {
                    failed++;
                    telemetry.RecordFailure(WorkerTelemetry.FileCleanup, "Cleanup");
                    logger.LogWarning("پاک‌سازی فایل terminal ناموفق بود. Code={FailureCode}", "TERMINAL_FILE_CLEANUP_FAILED");
                }
                finally
                {
                    cursor.Positions[status] = new(status, candidate.CreatedAtUtc, candidate.Id);
                }
            }
            return new(examined, deleted, missing, skipped, failed);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        finally
        {
            cursor.Gate.Release();
            telemetry.RecordCleanup("Examined", examined);
            telemetry.RecordCleanup("Deleted", deleted);
            telemetry.RecordCleanup("AlreadyMissing", missing);
            telemetry.RecordCleanup("Skipped", skipped);
            telemetry.RecordCleanup("Failed", failed);
        }
    }

    private async Task<string> ProcessAssetAsync(LocalFileStorage storage, Guid fileAssetId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var file = await dbContext.FileAssets.FromSqlInterpolated($"""
            SELECT * FROM [storage].[FileAssets] WITH (XLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = {fileAssetId}
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var terminalAt = file?.Status == FileAssetStatus.Deleted ? file.DeletedAtUtc : file?.RejectedAtUtc;
        if (file is null || file.Status is not (FileAssetStatus.Deleted or FileAssetStatus.Rejected) ||
            file.RetainedAtUtc.HasValue || !terminalAt.HasValue || terminalAt.Value > CutoffUtc() ||
            await IsReferencedAsync(file.Id, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return "Skipped";
        }

        // فقط syscall کوچک محلی زیر این قفل مجاز است؛ metadata و rowversion هرگز نوشته نمی‌شوند.
        var deleted = await storage.DeleteTerminalBlobAsync(file.StorageKey, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted ? "Deleted" : "AlreadyMissing";
    }

    private async Task<bool> IsReferencedAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        await dbContext.EducationalSourceVersions.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.Users.AnyAsync(user => user.ProfilePhotoFileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.ClassLessonAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.AnswerRevisionAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.SubmissionAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.MessageAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.ModerationEvidenceAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.Set<ScheduledMessageAttachment>().AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.ReportExportJobs.AnyAsync(item => item.Id == fileAssetId || item.OutputFileAssetId == fileAssetId, cancellationToken);

    private DateTime CutoffUtc()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var graceTicks = (long)options.GracePeriodHours!.Value * TimeSpan.TicksPerHour;
        return new DateTime(Math.Max(0, now.Ticks - graceTicks), DateTimeKind.Utc);
    }
}
