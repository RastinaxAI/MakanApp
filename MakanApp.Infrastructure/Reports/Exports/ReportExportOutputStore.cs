using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class ReportExportOutputStore(
    MakanDbContext dbContext,
    IFileStorage fileStorage,
    StorageOptions storageOptions,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<ReportExportOutputStore> logger) : IReportExportOutputStore
{
    public async Task<Guid?> FindReusableOutputAsync(
        ReportExportJob job,
        CancellationToken cancellationToken)
    {
        var file = await dbContext.FileAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == job.Id, cancellationToken);
        if (file?.Status != FileAssetStatus.Ready)
        {
            return null;
        }

        if (!await fileStorage.ExistsAsync(file.StorageKey, cancellationToken))
        {
            throw new StorageException(
                StorageErrorCodes.FileStorageFailed,
                "فراداده خروجی گزارش وجود دارد اما محتوای فایل در مخزن پیدا نشد.");
        }

        return file.Id;
    }

    public async Task<Guid?> SaveAsync(
        ReportExportJob job,
        byte[] expectedClaimRowVersion,
        GeneratedReportFile generated,
        long maximumSizeBytes,
        CancellationToken cancellationToken)
    {
        // محتوای هر تلاش کلید مستقل دارد؛ I/O طولانی نه قفل SQL می‌گیرد نه خروجی تلاش دیگر را بازنویسی می‌کند.
        var candidateKey = FileUploadPolicy.CreateStorageKey(Guid.NewGuid(), timeProvider.GetUtcNow().UtcDateTime);
        generated.Content.Position = 0;
        StoredFileResult stored;
        try
        {
            stored = await fileStorage.SaveAsync(candidateKey, generated.Content, maximumSizeBytes, cancellationToken);
        }
        catch
        {
            await DeleteUnpublishedCandidateAsync(candidateKey);
            throw;
        }
        Guid? outputId = null;
        string? reusedKey = null;
        var published = false;
        FileAsset? publicationFile = null;
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var current = await ReportExportClaimLock.GetAsync(dbContext, job.Id, cancellationToken);
            if (ReportExportClaimLock.IsCurrent(current, expectedClaimRowVersion, timeProvider.GetUtcNow().UtcDateTime))
            {
                publicationFile = await dbContext.FileAssets.FromSqlInterpolated($"""
                    SELECT * FROM [storage].[FileAssets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE [Id] = {job.Id}
                    """).SingleOrDefaultAsync(cancellationToken);
                // انتظار برای قفل فایل نیز می‌تواند lease را منقضی کند.
                if (ReportExportClaimLock.IsCurrent(current, expectedClaimRowVersion, timeProvider.GetUtcNow().UtcDateTime))
                {
                    if (publicationFile?.Status == FileAssetStatus.Ready)
                    {
                        reusedKey = publicationFile.StorageKey;
                    }
                    else
                    {
                        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                        if (publicationFile is null)
                        {
                            publicationFile = FileAsset.CreatePending(job.Id, job.OrganizationId, job.RequestedByUserId,
                                generated.DownloadFileName, candidateKey, generated.ContentType, nowUtc,
                                nowUtc.Add(storageOptions.UnattachedLifetime));
                            dbContext.FileAssets.Add(publicationFile);
                        }
                        else
                        {
                            if (publicationFile.Status != FileAssetStatus.Pending)
                            {
                                throw new StorageException(StorageErrorCodes.FileStorageFailed,
                                    "وضعیت فایل خروجی گزارش قابل بازیابی نیست.");
                            }

                            publicationFile.ReplacePendingStorageKey(candidateKey);
                        }

                        publicationFile.MarkReady(stored.SizeBytes, stored.Sha256Hash, nowUtc);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        published = true;
                    }

                    outputId = publicationFile!.Id;
                }
            }

            // در خطای SQL یا گم‌شدن پاسخ Commit، candidate حذف نمی‌شود؛ ممکن است همین حالا canonical باشد.
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (publicationFile is not null)
            {
                // خطای انتشار نباید در SaveChanges مربوط به retryِ job دوباره metadata فایل را بنویسد.
                dbContext.Entry(publicationFile).State = EntityState.Detached;
            }
            throw;
        }

        if (!published)
        {
            await DeleteUnpublishedCandidateAsync(candidateKey);
        }

        if (reusedKey is not null && !await fileStorage.ExistsAsync(reusedKey, cancellationToken))
        {
            throw new StorageException(StorageErrorCodes.FileStorageFailed,
                "فراداده خروجی گزارش وجود دارد اما محتوای فایل در مخزن پیدا نشد.");
        }

        return outputId;
    }

    private async Task DeleteUnpublishedCandidateAsync(string candidateKey)
    {
        using var cleanup = WorkerCompletionBudget.Create();
        try
        {
            await fileStorage.DeleteAsync(candidateKey, cleanup.Token);
        }
        catch (Exception)
        {
            // حذف بهترین‌تلاش فقط برای candidate قطعاً منتشرنشده است؛ خطا نباید خروجی معتبر را خراب کند.
            telemetry.RecordFailure(WorkerTelemetry.ReportExport, "Cleanup");
            logger.LogWarning("پاک‌سازی فایل منتشرنشده گزارش ناموفق بود. Code={FailureCode}", "REPORT_EXPORT_CANDIDATE_CLEANUP_FAILED");
        }
    }
}
