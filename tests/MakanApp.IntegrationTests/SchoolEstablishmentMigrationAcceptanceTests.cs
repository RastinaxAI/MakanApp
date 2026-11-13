using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class SchoolEstablishmentMigrationAcceptanceTests(MakanAppWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolEstablishmentMigrationIsAdditiveAndPreservesLegacyDraftWithoutGuessedHistoryOrTerms(bool baseline)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString()) {
            InitialCatalog = "MakanApp_SchoolAcceptance_" + Guid.NewGuid().ToString("N"), Pooling = false
        };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^MakanApp_SchoolAcceptance_[a-f0-9]{32}$", connection.InitialCatalog);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(baseline ? "20261112165826_AddVerifiedPhoneChangeRequests" : null);
            var now = DateTime.UtcNow; var owner = User.Create(now); db.Users.Add(owner);
            var app = SchoolApplication.Create(owner.Id, SchoolApplicationKind.LegalOrganization, new("پیش‌نویس قبلی", null, null, null, null, null, null), now);
            db.SchoolApplications.Add(app); await db.SaveChangesAsync(); var version = app.RowVersion.ToArray();
            var delta = migrator.GenerateScript("20261112165826_AddVerifiedPhoneChangeRequests", "20261113065124_CompleteSchoolEstablishmentWorkflow",
                MigrationsSqlGenerationOptions.Idempotent);
            await ExecuteScriptAsync(db, delta); await ExecuteScriptAsync(db, delta);
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            var saved = await db.SchoolApplications.SingleAsync(a => a.Id == app.Id);
            Assert.Equal(version, saved.RowVersion); Assert.Null(saved.StudentCapacity); Assert.Null(saved.TeacherCapacity);
            Assert.Equal(SchoolApplicationStatus.Draft, saved.Status); Assert.Null(saved.SubmittedPolicyVersion);
            Assert.Empty(await db.SchoolApplicationHistories.ToArrayAsync()); Assert.Empty(await db.SchoolEstablishmentOfferings.ToArrayAsync());
            Assert.Empty(await db.Orders.ToArrayAsync()); Assert.Empty(await db.PlatformProvisioningRequests.ToArrayAsync());
            var invalid = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[SchoolApplications] SET [TeacherCapacity]=-1 WHERE [Id]={app.Id}"));
            Assert.Equal(547, invalid.Number);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
            Assert.Matches("^MakanApp_SchoolAcceptance_[a-f0-9]{32}$", connection.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task ExecuteScriptAsync(MakanDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = batch; command.CommandTimeout = 120; await command.ExecuteNonQueryAsync();
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
