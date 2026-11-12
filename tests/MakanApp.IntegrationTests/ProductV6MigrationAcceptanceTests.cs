using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductV6MigrationAcceptanceTests(MakanAppWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductV6FreshAndBaselineUpgradePreserveIdentityAndLegacyContactsWithoutGuessedBusinessData(bool baseline)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var original = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(original.Database.GetConnectionString()) {
            InitialCatalog = "MakanApp_ProductV6Acceptance_" + Guid.NewGuid().ToString("N"), Pooling = false
        };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^MakanApp_ProductV6Acceptance_[a-f0-9]{32}$", connection.InitialCatalog);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(baseline ? "20261110081503_AddZibalPaymentIntegration" : null);
            var now = DateTime.UtcNow; var owner = User.Create(now); var peer = User.Create(now);
            await LegacyAcademicMigrationFixture.InsertAsync(db, owner); await LegacyAcademicMigrationFixture.InsertAsync(db, peer);
            var contact = UserContact.Create(owner.Id, peer.Id, "نام قدیمی حفظ‌شده", now); db.UserContacts.Add(contact); await db.SaveChangesAsync();
            var version = contact.RowVersion.ToArray();
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            var saved = await db.UserContacts.AsNoTracking().SingleAsync(c => c.Id == contact.Id);
            Assert.Equal("نام قدیمی حفظ‌شده", saved.SavedName); Assert.Equal(version, saved.RowVersion);
            Assert.Null((await db.Users.SingleAsync(u => u.Id == owner.Id)).Bio);
            Assert.Equal(2, await db.Users.CountAsync()); Assert.Empty(await db.AddressBookEntries.ToArrayAsync());
            Assert.Empty(await db.SchoolApplications.ToArrayAsync()); Assert.Empty(await db.SupportTickets.ToArrayAsync());
            Assert.Empty(await db.AccountDeletionRequests.ToArrayAsync()); Assert.Empty(await db.Subscriptions.ToArrayAsync());
            Assert.Empty(await db.PhoneChangeRequests.ToArrayAsync());
            var session = UserSession.Create(owner.Id, new byte[32], now, now.AddHours(1)); db.UserSessions.Add(session); await db.SaveChangesAsync();
            var mismatch = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].[AccountDeletionRequests]
                ([Id],[UserId],[RequestedBySessionId],[ClientOperationId],[RequestedAtUtc],[PolicyStatus])
                VALUES ({Guid.NewGuid()},{peer.Id},{session.Id},{Guid.NewGuid()},{now},N'PRODUCT_LEGAL_DECISION_REQUIRED')
                """));
            Assert.Equal(547, mismatch.Number); Assert.Empty(await db.AccountDeletionRequests.AsNoTracking().ToArrayAsync());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
            Assert.Matches("^MakanApp_ProductV6Acceptance_[a-f0-9]{32}$", connection.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
