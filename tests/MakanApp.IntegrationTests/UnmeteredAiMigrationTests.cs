using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class UnmeteredAiMigrationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnmeteredMigrationFreshAndUpgradePreserveHistoryAndEnforceAccounting(bool upgrade)
    {
        const string before = "20261114183321_ExtendProductNotificationCoverage";
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = "(localdb)\\MSSQLLocalDB", InitialCatalog = "MakanApp_UnmeteredMigration_" + Guid.NewGuid().ToString("N"),
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false
        };
        Assert.Matches("^MakanApp_UnmeteredMigration_[a-f0-9]{32}$", connection.InitialCatalog);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            Assert.False(await db.Database.CanConnectAsync()); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(upgrade ? before : null);
            var now = DateTime.UtcNow; var user = User.Create(now); db.Users.Add(user); await db.SaveChangesAsync();
            var historical = Guid.NewGuid(); var historicalOperation = Guid.NewGuid(); var hash = new byte[32];
            var periodEnd = now.AddDays(1);
            // ستون تازه در حالت Upgrade هنوز وجود ندارد؛ fixture دقیقاً schema قبلی را به کار می‌گیرد.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [commerce].[UsageReservations]
                    ([Id],[OwnerType],[OwnerId],[ConsumerUserId],[ContextType],[ContextId],[QuotaKey],
                     [PeriodStartsAtUtc],[PeriodEndsAtUtc],[Amount],[OperationId],[PayloadHash],[IsTrial],[Status],
                     [CreatedAtUtc],[FinalizedAtUtc],[FinalizationReason])
                VALUES ({historical},1,{user.Id},{user.Id},1,{user.Id},5,{now},{periodEnd},1,{historicalOperation},{hash},1,2,{now},{now},'historical_success');
                """);
            var oldVersion = await VersionAsync(db, historical);
            await migrator.MigrateAsync(); await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var old = await db.UsageReservations.AsNoTracking().SingleAsync(r => r.Id == historical);
            Assert.False(old.IsUnmetered); Assert.True(old.IsTrial); Assert.Equal(UsageAccountingMode.FreeTrial, old.AccountingMode);
            Assert.Equal(oldVersion, old.RowVersion); Assert.Equal(historicalOperation, old.OperationId); Assert.Null(old.SubscriptionId);
            var record = UsageReservation.CreateUnmetered(user.Id, CommerceContextType.Personal, user.Id, Guid.NewGuid(), hash, now);
            db.UsageReservations.Add(record); db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(record, now, "reserved")); await db.SaveChangesAsync();
            record.Finalize(UsageReservationStatus.Unknown, "timeout", now); await db.SaveChangesAsync();
            record.Finalize(UsageReservationStatus.Consumed, "recovered", now.AddDays(2));
            db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(record, now.AddDays(2), "recovered")); await db.SaveChangesAsync();
            Assert.True(record.IsUnmetered); Assert.Null(record.SubscriptionId); Assert.False(record.IsTrial);
            var inconsistent = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [commerce].[UsageReservations] SET [IsTrial]=1 WHERE [Id]={record.Id}"));
            Assert.Equal(547, inconsistent.Number);
            var ledgerConflict = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [commerce].[UsageLedger] SET [QuotaKey]=4 WHERE [ReservationId]={record.Id}"));
            Assert.Equal(547, ledgerConflict.Number);
            var duplicate = UsageReservation.CreateUnmetered(user.Id, CommerceContextType.Personal, user.Id, record.OperationId, hash, now);
            db.UsageReservations.Add(duplicate);
            var duplicateError = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains(Assert.IsType<SqlException>(duplicateError.InnerException).Number, new[] { 2601, 2627 }); db.ChangeTracker.Clear();
            var down = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(before)); Assert.Equal(51000, down.Number);
            Assert.True(await db.UsageReservations.AnyAsync(r => r.Id == record.Id && r.IsUnmetered));
            Assert.True(await db.UsageReservations.AnyAsync(r => r.Id == historical && !r.IsUnmetered));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            Assert.Matches("^MakanApp_UnmeteredMigration_[a-f0-9]{32}$", connection.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<byte[]> VersionAsync(MakanDbContext db, Guid id)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT [RowVersion] FROM [commerce].[UsageReservations] WHERE [Id]=@id";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = id; command.Parameters.Add(parameter);
            return (byte[])(await command.ExecuteScalarAsync())!;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
