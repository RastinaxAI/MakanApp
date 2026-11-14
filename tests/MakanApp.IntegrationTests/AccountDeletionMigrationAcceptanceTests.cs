using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AccountDeletionMigrationAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionMigrationFreshAndUpgradePreserveRootsAndKeepOldRequestsUnexecuted(bool upgrade)
    {
        var connection = new SqlConnectionStringBuilder {
            DataSource = "(localdb)\\MSSQLLocalDB", InitialCatalog = "MakanApp_DeletionMigration_" + Guid.NewGuid().ToString("N"),
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false
        };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^MakanApp_DeletionMigration_[a-f0-9]{32}$", connection.InitialCatalog);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            Assert.False(await db.Database.CanConnectAsync()); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(upgrade ? "20261113103326_FinalizeSchoolManagerAndPaidProvisioning" : null);
            var now = DateTime.UtcNow; var user = User.Create(now); await LegacyAcademicMigrationFixture.InsertAsync(db, user);
            var session = UserSession.Create(user.Id, new byte[32], now, now.AddHours(1)); await LegacyAcademicMigrationFixture.InsertAsync(db, session);
            var request = AccountDeletionRequest.Create(user.Id, session.Id, Guid.NewGuid(), now);
            if (upgrade)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [identity].[AccountDeletionRequests] ([Id],[UserId],[RequestedBySessionId],[ClientOperationId],[RequestedAtUtc],[PolicyStatus])
                    VALUES ({request.Id},{user.Id},{session.Id},{request.ClientOperationId},{now},N'PRODUCT_LEGAL_DECISION_REQUIRED')
                    """);
            else
            {
                db.AccountDeletionRequests.Add(request);
                db.AccountDeletionSteps.AddRange(Enum.GetValues<AccountDeletionStepKind>().Select(k => AccountDeletionStep.Create(request.Id, k)));
                await db.SaveChangesAsync();
            }
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            var migrated = await db.AccountDeletionRequests.SingleAsync();
            Assert.Equal(AccountDeletionStatus.Requested, migrated.Status); Assert.Null(migrated.ScheduledExecutionAtUtc); Assert.Null(migrated.CompletedAtUtc);
            Assert.Equal(AccountDeletionRequest.CurrentPolicyVersion, migrated.PolicyVersion);
            Assert.Equal(8, await db.AccountDeletionSteps.CountAsync()); Assert.Empty(await db.AccountDeletionKnowledgePurges.ToArrayAsync());
            Assert.Null((await db.Users.SingleAsync()).DeletionStartedAtUtc); Assert.Equal(UserAccountStatus.Active, (await db.Users.SingleAsync()).AccountStatus);
            Assert.Null((await db.UserSessions.SingleAsync()).RevokedAtUtc);
            var invalidClaim = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [identity].[AccountDeletionRequests] SET [ClaimToken]={Guid.NewGuid()} WHERE [Id]={request.Id}"));
            Assert.Equal(547, invalidClaim.Number);
            var invalidCompletion = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [identity].[AccountDeletionRequests] SET [Status]=5 WHERE [Id]={request.Id}"));
            Assert.Equal(547, invalidCompletion.Number);
            var invalidReactivation = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [identity].[Users] SET [DeletionStartedAtUtc]={now},[AccountStatus]=1 WHERE [Id]={user.Id}"));
            Assert.Equal(547, invalidReactivation.Number);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            Assert.Matches("^MakanApp_DeletionMigration_[a-f0-9]{32}$", connection.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
