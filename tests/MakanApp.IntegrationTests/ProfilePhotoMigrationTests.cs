using MakanApp.Domain.Identity;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ProfilePhotoMigrationTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task ProfilePhotoMigrationIsNullablePreservesExistingDataAndEnforcesForeignKeyAndUniqueReference()
    {
        const string baseline = "20261028102653_AddSelfDeclaredProfileDateOfBirth";
        const string migration = "20261028164838_AddUserProfilePhoto";
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
        {
            InitialCatalog = "MakanApp_ProfilePhoto_Migration_" + Guid.NewGuid().ToString("N")
        };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MakanApp_ProfilePhoto_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(baseline);
            var now = DateTime.UtcNow; var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
            var person = Person.Create("نام قبلی", "پروفایل", null, now); person.SetDateOfBirth(new DateOnly(1990, 2, 3), now);
            db.Persons.Add(person); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].[Users] ([Id],[PersonId],[CreatedAtUtc],[AccountStatus],[CommunicationAgeCategory],[CommunicationAgeVerificationStatus])
                VALUES ({firstId},{person.Id},{now},1,0,0),({secondId},NULL,{now},1,0,0)
                """);
            var file = FileAsset.CreatePending(Guid.NewGuid(), null, firstId, "legacy.png", Guid.NewGuid().ToString("N"), "image/png", now, now.AddHours(1));
            file.MarkReady(100, new string('a', 64), now); db.FileAssets.Add(file); await db.SaveChangesAsync();
            await migrator.MigrateAsync(); await migrator.MigrateAsync();
            Assert.Contains(migration, await db.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            Assert.All(await db.Users.ToArrayAsync(), user => Assert.Null(user.ProfilePhotoFileAssetId));
            Assert.Equal(new DateOnly(1990, 2, 3), (await db.Persons.SingleAsync()).DateOfBirth);
            Assert.Equal(file.Sha256Hash, (await db.FileAssets.AsNoTracking().SingleAsync()).Sha256Hash);
            var sql = migrator.GenerateScript(baseline, migration);
            Assert.Contains("[ProfilePhotoFileAssetId] uniqueidentifier NULL", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("DateOfBirth", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
            var user = await db.Users.SingleAsync(u => u.Id == firstId); user.SetProfilePhoto(file.Id, now); await db.SaveChangesAsync();
            var missing = Guid.NewGuid();
            var invalidFk = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [identity].[Users] SET [ProfilePhotoFileAssetId]={missing} WHERE [Id]={secondId}"));
            Assert.Equal(547, invalidFk.Number);
            var duplicate = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [identity].[Users] SET [ProfilePhotoFileAssetId]={file.Id} WHERE [Id]={secondId}"));
            Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
            var delete = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [storage].[FileAssets] WHERE [Id]={file.Id}"));
            Assert.Equal(547, delete.Number);
            Assert.Equal(file.Id, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == firstId)).ProfilePhotoFileAssetId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
