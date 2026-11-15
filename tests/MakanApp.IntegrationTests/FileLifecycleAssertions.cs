using System.Net;
using System.Text.Json;
using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

internal static class FileLifecycleAssertions
{
    internal static async Task BoundCannotDeleteAsync(MakanAppWebApplicationFactory factory, HttpClient uploader, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var retainedAt = (await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == id)).RetainedAtUtc;
        // فقط در SQL ایزولهٔ تست: marker موقتاً خالی است تا خود اتصال، نه shortcut نگهداری، اثبات شود.
        if (retainedAt.HasValue)
            await db.FileAssets.Where(f => f.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(f => f.RetainedAtUtc, (DateTime?)null));
        try
        {
            var before = await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == id);
            using var response = await uploader.DeleteAsync($"/api/v1/files/{id}?expectedRowVersion={Uri.EscapeDataString(Convert.ToBase64String(before.RowVersion))}");
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(StorageErrorCodes.FileInUse, error.RootElement.GetProperty("code").GetString());
            var after = await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == id);
            Assert.NotEqual(FileAssetStatus.Deleted, after.Status);
            Assert.Equal(before.RowVersion, after.RowVersion);
            Assert.True(await factory.StoredBytesExistAsync(id));
        }
        finally
        {
            if (retainedAt.HasValue)
                await db.FileAssets.Where(f => f.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(f => f.RetainedAtUtc, retainedAt));
        }
    }
}
