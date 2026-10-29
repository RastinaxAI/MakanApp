using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
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
public sealed class CommercialSubscriptionMigrationTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task MigrationPreservesIndefiniteLegacyContractsAndLedgerAndRejectsDestructiveRollback()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
            { InitialCatalog = "MakanApp_Commercial_Migration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MakanApp_Commercial_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261028202239_CompleteSchoolManagement");
            var now = DateTime.UtcNow; var organization = Organization.Create("سازمان قدیمی آزمون", now); var user = User.Create(now);
            db.Organizations.Add(organization); db.Users.Add(user); await db.SaveChangesAsync();
            var plan = Guid.NewGuid(); var version = Guid.NewGuid(); var subscription = Guid.NewGuid(); var ledger = Guid.NewGuid(); var operation = Guid.NewGuid();
            var code = "legacy-" + plan.ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [commerce].[Plans] ([Id],[Code],[DisplayName],[CreatedAtUtc]) VALUES ({plan},{code},N'پلن تاریخی',{now});
                INSERT INTO [commerce].[PlanVersions] ([Id],[PlanId],[VersionNumber],[Status],[CreatedAtUtc],[PublishedAtUtc])
                    VALUES ({version},{plan},1,2,{now},{now});
                INSERT INTO [commerce].[Subscriptions] ([Id],[OrganizationId],[PlanVersionId],[Status],[StartsAtUtc],[EndsAtUtc],[CreatedAtUtc],[ActivatedAtUtc])
                    VALUES ({subscription},{organization.Id},{version},2,{now},NULL,{now},{now});
                INSERT INTO [commerce].[UsageLedger] ([Id],[OrganizationId],[SubscriptionId],[QuotaKey],[Amount],[SourceOperationId],[OccurredAtUtc])
                    VALUES ({ledger},{organization.Id},{subscription},4,5,{operation},{now});
                """);
            var script = migrator.GenerateScript("20261028202239_CompleteSchoolManagement", "20261029111645_AddCommercialOwnershipPeriodsAndPersonalClasses",
                MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            try
            {
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = batch; command.CommandTimeout = 120; await command.ExecuteNonQueryAsync();
                }
            }
            finally { await db.Database.CloseConnectionAsync(); }
            await migrator.MigrateAsync(); await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var saved = await db.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == subscription);
            Assert.Equal(organization.Id, saved.OwnerId); Assert.Equal(SubscriptionOwnerType.Organization, saved.OwnerType);
            Assert.False(saved.CommercialControlEnabled); Assert.Null(saved.EndsAtUtc); Assert.Null(saved.BillingAnchorUtc);
            Assert.Equal(version, saved.InitialPlanVersionId);
            var entry = await db.UsageLedgerEntries.AsNoTracking().SingleAsync(e => e.Id == ledger);
            Assert.Equal(5, entry.Amount); Assert.Equal(operation, entry.SourceOperationId);
            Assert.Equal(UsageReservationStatus.Consumed, entry.Status); Assert.Equal(organization.Id, entry.OwnerId);
            Assert.Null(entry.ConsumerUserId); Assert.Null(entry.PeriodEndsAtUtc);
            Assert.Null((await db.PlanVersions.SingleAsync(v => v.Id == version)).Calendar);
            var other = Organization.Create("سازمان غیرمجاز آزمون", now); db.Organizations.Add(other); await db.SaveChangesAsync();
            db.UsageLedgerEntries.Add(UsageLedgerEntry.Create(other.Id, subscription, QuotaKey.ReportExports, 1, Guid.NewGuid(), now));
            var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(547, Assert.IsType<SqlException>(rejected.InnerException).Number);
            db.ChangeTracker.Clear();
            db.PersonalClasses.Add(PersonalClass.Create(user.Id, "نمونهٔ محفوظ", Guid.NewGuid(), now)); await db.SaveChangesAsync();
            var rollback = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync("20261028202239_CompleteSchoolManagement"));
            Assert.Equal(51000, rollback.Number);
            Assert.Equal(1, await db.PersonalClasses.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
