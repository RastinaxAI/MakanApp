using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Identity;

internal static class ProfilePhotoQuery
{
    public static IQueryable<FileAsset> ReadyImages(MakanDbContext db) =>
        db.FileAssets.AsNoTracking().Where(file => file.Status == FileAssetStatus.Ready &&
            file.OrganizationId == null && (file.ContentType == "image/jpeg" || file.ContentType == "image/png"));
}
