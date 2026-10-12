using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class ReportExportOutputStore(
    MakanDbContext dbContext,
    IFileStorage fileStorage,
    StorageOptions storageOptions,
    TimeProvider timeProvider) : IReportExportOutputStore
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

    public async Task<Guid> SaveAsync(
        ReportExportJob job,
        GeneratedReportFile generated,
        long maximumSizeBytes,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var file = await dbContext.FileAssets.SingleOrDefaultAsync(item => item.Id == job.Id, cancellationToken);
        if (file is null)
        {
            file = FileAsset.CreatePending(
                job.Id,
                job.OrganizationId,
                job.RequestedByUserId,
                generated.DownloadFileName,
                FileUploadPolicy.CreateStorageKey(job.Id, nowUtc),
                generated.ContentType,
                nowUtc,
                nowUtc.Add(storageOptions.UnattachedLifetime));
            dbContext.FileAssets.Add(file);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (file.Status == FileAssetStatus.Ready)
        {
            return file.Id;
        }
        else if (file.Status != FileAssetStatus.Pending)
        {
            throw new StorageException(
                StorageErrorCodes.FileStorageFailed,
                "وضعیت فایل خروجی گزارش قابل بازیابی نیست.");
        }

        await fileStorage.DeleteAsync(file.StorageKey, cancellationToken);
        generated.Content.Position = 0;
        var stored = await fileStorage.SaveAsync(
            file.StorageKey,
            generated.Content,
            maximumSizeBytes,
            cancellationToken);
        file.MarkReady(stored.SizeBytes, stored.Sha256Hash, timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
        return file.Id;
    }
}
