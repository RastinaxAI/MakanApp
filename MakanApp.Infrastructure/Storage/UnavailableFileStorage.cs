using MakanApp.Application.Storage;

namespace MakanApp.Infrastructure.Storage;

public sealed class UnavailableFileStorage : IFileStorage, IStorageReadinessProbe
{
    public Task<StoredFileResult> SaveAsync(
        string storageKey,
        Stream content,
        long maxSizeBytes,
        CancellationToken cancellationToken) => throw Unavailable();

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task<bool> IsReadyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(false);

    private static StorageException Unavailable() =>
        new(
            StorageErrorCodes.FileStorageFailed,
            "ارائه‌دهنده ذخیره‌سازی production هنوز پیکربندی نشده است.");
}
