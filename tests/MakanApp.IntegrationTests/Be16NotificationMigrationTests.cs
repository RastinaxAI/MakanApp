using MakanApp.Domain.Identity;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class Be16NotificationMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Be16MigrationFreshAndUpgradePreserveReceiptsEnforceTypesAndRefuseLossyDowngrade(bool upgrade)
    {
        const string previous = "20261114140323_AddAccountDeletionLifecycle";
        var connection = new SqlConnectionStringBuilder {
            DataSource = "(localdb)\\MSSQLLocalDB", InitialCatalog = "MakanApp_NotificationMigration_" + Guid.NewGuid().ToString("N"),
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false
        };
        Assert.Matches("^MakanApp_NotificationMigration_[a-f0-9]{32}$", connection.InitialCatalog);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            Assert.False(await db.Database.CanConnectAsync()); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(upgrade ? previous : null);
            var now = DateTime.UtcNow; var user = User.Create(now); db.Users.Add(user); await db.SaveChangesAsync();
            var old = DomainNotification.Create(Guid.NewGuid(), user.Id, null, NotificationCategory.Educational, NotificationType.AssignmentPublished,
                "رسید قدیمی", "متن عمومی قدیمی", NotificationResourceType.Assignment, Guid.NewGuid(), now);
            old.MarkRead(now); db.Notifications.Add(old); await db.SaveChangesAsync(); var version = old.RowVersion.ToArray();
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            var preserved = await db.Notifications.SingleAsync(n => n.Id == old.Id);
            Assert.Equal(version, preserved.RowVersion); Assert.Equal(old.ReadAtUtc, preserved.ReadAtUtc); Assert.Equal(old.SourceEventId, preserved.SourceEventId);
            var pending = new NotificationIntent(Guid.NewGuid(), null, NotificationCategory.AccountSystem, NotificationType.PersonalClassStateChanged,
                NotificationResourceType.PersonalClass, Guid.NewGuid(), now, [new(user.Id, "رویداد جدید", "بدنهٔ عمومی")]);
            var envelope = Assert.Single(NotificationOutboxContract.Create(pending));
            new EfOutbox(db).EnqueueRange([envelope]); await db.SaveChangesAsync();
            var pendingDowngrade = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(previous)); Assert.Equal(51000, pendingDowngrade.Number);
            Assert.True(await db.OutboxMessages.AnyAsync(o => o.MessageType == "notification.personal-class-state-changed.v1" && o.ProcessedAtUtc == null));
            // فقط envelope همین آزمون حذف می‌شود تا guard مربوط به receipt نیز مستقل ثابت شود.
            Assert.Equal(1, await db.OutboxMessages.Where(o => o.Id == envelope.MessageId).ExecuteDeleteAsync());
            var fresh = DomainNotification.Create(Guid.NewGuid(), user.Id, null, NotificationCategory.AccountSystem, NotificationType.PersonalClassStateChanged,
                "رسید جدید", "متن عمومی جدید", NotificationResourceType.PersonalClass, Guid.NewGuid(), now);
            db.Notifications.Add(fresh); await db.SaveChangesAsync();
            var badType = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [notification].[Notifications] SET [Type]=21 WHERE [Id]={fresh.Id}"));
            Assert.Equal(547, badType.Number);
            var badResource = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [notification].[Notifications] SET [ResourceType]=19 WHERE [Id]={fresh.Id}"));
            Assert.Equal(547, badResource.Number);
            var downgrade = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(previous)); Assert.Equal(51000, downgrade.Number);
            Assert.True(await db.Notifications.AnyAsync(n => n.Id == fresh.Id)); Assert.True(await db.Notifications.AnyAsync(n => n.Id == old.Id));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            Assert.Matches("^MakanApp_NotificationMigration_[a-f0-9]{32}$", connection.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
