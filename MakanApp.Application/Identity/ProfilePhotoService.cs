using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Identity;

public sealed record SetProfilePhotoCommand(Guid FileAssetId);

public sealed record ProfilePhotoResult(Guid FileAssetId, string ContentType)
{
    public string DownloadUrl => $"/api/v1/files/{FileAssetId:D}/content";
}

public sealed class ProfilePhotoService(
    IIdentityStore identityStore,
    IFileAssetStore fileStore,
    TimeProvider clock)
{
    public async Task<ProfilePhotoResult> SetAsync(Guid userId, SetProfilePhotoCommand command, CancellationToken ct)
    {
        if (command.FileAssetId == Guid.Empty) throw NotFound();
        await using var transaction = await identityStore.BeginSerializableTransactionAsync(ct);
        var user = await GetActiveUserAsync(userId, ct);
        var file = await fileStore.GetForUpdateAsync(command.FileAssetId, ct) ?? throw NotFound();
        if (file.UploadedByUserId != userId || file.OrganizationId is not null) throw NotFound();
        if (file.Status != FileAssetStatus.Ready)
            throw new StorageException(StorageErrorCodes.FileNotReady, "عکس پروفایل باید فایل آماده باشد.");
        if (file.ContentType is not ("image/jpeg" or "image/png"))
            throw new StorageException(StorageErrorCodes.FileTypeNotAllowed, "عکس پروفایل باید JPEG یا PNG معتبر باشد.");

        if (user.ProfilePhotoFileAssetId != file.Id)
        {
            user.SetProfilePhoto(file.Id, clock.GetUtcNow().UtcDateTime);
            // تغییر rowversion فایل، حذفِ هم‌زمان با نسخهٔ قدیمی را پس از اتصال نامعتبر می‌کند.
            fileStore.TouchForBinding(file);
            await fileStore.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new ProfilePhotoResult(file.Id, file.ContentType);
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        await using var transaction = await identityStore.BeginSerializableTransactionAsync(ct);
        var user = await GetActiveUserAsync(userId, ct);
        if (user.ProfilePhotoFileAssetId is not null)
        {
            user.SetProfilePhoto(null, clock.GetUtcNow().UtcDateTime);
            await fileStore.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await identityStore.GetUserForUpdateAsync(userId, ct)
            ?? throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        if (user.AccountStatus != UserAccountStatus.Active)
            throw new IdentityException(IdentityErrorCodes.AccountSuspended, "حساب کاربری معلق است.");
        return user;
    }

    private static StorageException NotFound() => new(StorageErrorCodes.FileNotFound, "فایل قابل استفاده برای عکس پروفایل یافت نشد.");
}
