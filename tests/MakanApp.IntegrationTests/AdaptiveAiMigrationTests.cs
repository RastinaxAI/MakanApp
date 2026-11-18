using MakanApp.Application.Ai;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Intelligence;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AdaptiveAiMigrationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AdaptiveAiFreshAndUpgradePreserveUsersEnforceIntegrityAndRejectDestructiveRollback(bool upgrade)
    {
        await Isolated(async options =>
        {
            await using var db = new MakanDbContext(options); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(upgrade ? "20261118065656_AddUnmeteredAiOperations" : null);
            var owner = User.Create(DateTime.UtcNow); var other = User.Create(DateTime.UtcNow); db.Users.AddRange(owner, other); await db.SaveChangesAsync();
            await migrator.MigrateAsync(); await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.True(await db.Users.AnyAsync(x => x.Id == owner.Id));
            var binding = new byte[32]; var operation = Guid.NewGuid(); var id = Guid.NewGuid();
            var store = new EfAiConversationStore(db, TimeProvider.System);
            var opened = await store.OpenAsync(owner.Id, id, operation, binding, binding, Authorize, default);
            Assert.Equal(id, opened.ConversationId);
            await store.CompleteAsync(owner.Id, id, operation, binding, "question", "answer", [], Authorize, default);
            var history = await store.ReadHistoryAsync(owner.Id, id, null, binding, Authorize, default);
            Assert.Equal(new[] { "User", "Assistant" }, history.History.Select(x => x.Role));
            var unique = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [intelligence].[AiConversationTurns]
                    ([Id],[ConversationId],[OwnerUserId],[OperationId],[Sequence],[Role],[Text],[IsCompleted],[IntentHash],[CreatedAtUtc],[CompletedAtUtc])
                SELECT NEWID(),[ConversationId],[OwnerUserId],[OperationId],[Sequence]+2,[Role],[Text],[IsCompleted],[IntentHash],[CreatedAtUtc],[CompletedAtUtc]
                    FROM [intelligence].[AiConversationTurns] WHERE [OwnerUserId]={owner.Id} AND [Role]=1;
                """));
            Assert.Contains(unique.Number, new[] { 2601, 2627 });
            var foreign = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [intelligence].[AiConversationTurns] SET [OwnerUserId]={other.Id} WHERE [ConversationId]={id}")); Assert.Equal(547, foreign.Number);
            var role = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [intelligence].[AiConversationTurns] SET [Role]=3 WHERE [ConversationId]={id}")); Assert.Equal(547, role.Number);
            var size = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [intelligence].[AiConversationTurns] SET [Text]=REPLICATE(CAST(N'x' AS nvarchar(max)),65537) WHERE [ConversationId]={id} AND [Role]=2")); Assert.Equal(547, size.Number);
            var rollback = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync("20261118065656_AddUnmeteredAiOperations"));
            Assert.Equal(51000, rollback.Number); Assert.Equal(2, await db.AiConversationTurns.CountAsync());
        });
    }

    [Fact]
    public async Task AdaptiveAiSqlConcurrentSameOperationCreatesOnePendingPairAndCompletionIsIdempotent()
    {
        await Isolated(async options =>
        {
            Guid owner;
            await using (var db = new MakanDbContext(options))
            { await db.Database.MigrateAsync(); var user = User.Create(DateTime.UtcNow); db.Users.Add(user); await db.SaveChangesAsync(); owner = user.Id; }
            var operation = Guid.NewGuid(); var binding = new byte[32];
            var states = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
            {
                await using var db = new MakanDbContext(options);
                return await new EfAiConversationStore(db, TimeProvider.System).OpenAsync(owner, null, operation, binding, binding, Authorize, default);
            }));
            var id = Assert.Single(states.Select(x => x.ConversationId).Distinct());
            await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
            {
                await using var db = new MakanDbContext(options);
                await new EfAiConversationStore(db, TimeProvider.System).CompleteAsync(owner, id, operation, binding, "q", "a", [], Authorize, default);
            }));
            await using var verify = new MakanDbContext(options); Assert.Equal(1, await verify.AiConversations.CountAsync());
            Assert.Equal(2, await verify.AiConversationTurns.CountAsync()); Assert.All(await verify.AiConversationTurns.ToArrayAsync(), x => Assert.True(x.IsCompleted));
        });
    }

    [Fact]
    public async Task AdaptiveAiConcurrentSameClientGuidAcrossOwnersConcealsLoserWithoutOverwriting()
    {
        await Isolated(async options =>
        {
            Guid[] owners;
            await using (var db = new MakanDbContext(options))
            { await db.Database.MigrateAsync(); var users = new[] { User.Create(DateTime.UtcNow), User.Create(DateTime.UtcNow) }; db.Users.AddRange(users); await db.SaveChangesAsync(); owners = users.Select(x => x.Id).ToArray(); }
            var id = Guid.NewGuid(); var binding = new byte[32];
            var results = await Task.WhenAll(owners.Select(async owner =>
            {
                await using var db = new MakanDbContext(options);
                return await Record.ExceptionAsync(() => new EfAiConversationStore(db, TimeProvider.System).OpenAsync(owner, id, Guid.NewGuid(), binding, binding, Authorize, default));
            }));
            Assert.Single(results, x => x is null);
            Assert.Equal(AiConversationErrorCodes.NotFound, Assert.IsType<AiServiceException>(Assert.Single(results, x => x is not null)).Code);
            await using var verify = new MakanDbContext(options); Assert.Equal(1, await verify.AiConversations.CountAsync()); Assert.Equal(1, await verify.AiConversationTurns.CountAsync());
        });
    }

    private static Task Authorize(AiConversationState state, CancellationToken ct) => Task.CompletedTask;
    private static async Task Isolated(Func<DbContextOptions<MakanDbContext>, Task> test)
    {
        var connection = new SqlConnectionStringBuilder { DataSource = "(localdb)\\MSSQLLocalDB",
            InitialCatalog = "MakanApp_AdaptiveMigration_" + Guid.NewGuid().ToString("N"), IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var options = new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var cleanup = new MakanDbContext(options);
        try { Assert.False(await cleanup.Database.CanConnectAsync()); await test(options); }
        finally
        {
            Assert.Matches("^MakanApp_AdaptiveMigration_[a-f0-9]{32}$", connection.InitialCatalog);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
