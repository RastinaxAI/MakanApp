using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ZibalPaymentMigrationAcceptanceTests(MakanAppWebApplicationFactory factory)
{
    private const string Before = "20261104224922_AddSchoolMembershipManagementOperations";
    private const string Target = "20261110081503_AddZibalPaymentIntegration";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UpPreservesHistoricalPaymentAndDeltaSqlReplaysWithoutInventedData(bool applyDeltaFirst)
    {
        await using var owned = await OwnedDatabase.CreateAsync(factory);
        var db = owned.Context;
        await db.GetService<IMigrator>().MigrateAsync(Before);
        var legacy = await SeedHistoricalPaymentAsync(db);
        var version = await ScalarAsync(db, $"SELECT [RowVersion] FROM [commerce].[Orders] WHERE [Id]='{legacy.OrderId}'");
        var export = await ExportAsync(Before);
        if (applyDeltaFirst) await ExecuteAsync(db, export.Sql);
        else await db.GetService<IMigrator>().MigrateAsync(Target);
        await AssertHistoryAndSchemaAsync(db, legacy);
        await ExecuteAsync(db, export.Sql);
        await ExecuteAsync(db, export.Sql);
        await AssertHistoryAndSchemaAsync(db, legacy);
        Assert.Equal((byte[])version, (byte[])await ScalarAsync(db, $"SELECT [RowVersion] FROM [commerce].[Orders] WHERE [Id]='{legacy.OrderId}'"));
        SaveEvidence(export, applyDeltaFirst ? "DELTA_UP_AND_REPLAY" : "EF_UP_AND_DELTA_REPLAY");
    }

    [Fact]
    public async Task FullIdempotentSqlCreatesFreshSchemaAndSecondRunPreservesPaidData()
    {
        await using var owned = await OwnedDatabase.CreateAsync(factory);
        var export = await ExportAsync("0");
        await ExecuteAsync(owned.Context, export.Sql);
        var legacy = await SeedHistoricalPaymentAsync(owned.Context);
        var version = (byte[])await ScalarAsync(owned.Context, $"SELECT [RowVersion] FROM [commerce].[PaymentAttempts] WHERE [Id]='{legacy.AttemptId}'");
        await ExecuteAsync(owned.Context, export.Sql);
        await AssertHistoryAndSchemaAsync(owned.Context, legacy);
        Assert.Equal(version, (byte[])await ScalarAsync(owned.Context, $"SELECT [RowVersion] FROM [commerce].[PaymentAttempts] WHERE [Id]='{legacy.AttemptId}'"));
        SaveEvidence(export, "FULL_SQL_FRESH_AND_REPLAY");
    }

    private static async Task<LegacyPayment> SeedHistoricalPaymentAsync(MakanDbContext db)
    {
        var now = DateTime.UtcNow.AddHours(-1);
        var user = User.Create(now); var organization = Organization.Create("پذیرش مالی تاریخی", now);
        var plan = Plan.Create("migration." + Guid.NewGuid().ToString("N"), "پلن تاریخی", now);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now);
        version.SetMonthlyTerms(BillingCalendar.Gregorian, "migration-acceptance", null, null);
        version.SetEntitlement(EntitlementKey.Messaging, false); version.SetPrice(1000, CurrencyCode.Irr); version.Publish(now);
        db.Users.Add(user); db.Organizations.Add(organization); db.Plans.Add(plan); db.PlanVersions.Add(version);
        await db.SaveChangesAsync();
        var orderId = Guid.NewGuid(); var attemptId = Guid.NewGuid(); var paymentId = Guid.NewGuid();
        var operation = Guid.NewGuid(); var reference = "legacy-" + paymentId.ToString("N"); var hash = new byte[32];
        // فقط fixture قدیمی ساخته می‌شود؛ قبل از Up ستون‌های تازه اصلاً وجود ندارند.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [commerce].[Orders]
                ([Id],[PurchaserUserId],[OrganizationId],[PlanVersionId],[Status],[Amount],[Currency],
                 [ClientOperationId],[RequestPayloadHash],[CreatedAtUtc],[PaidAtUtc],[CancelledAtUtc],
                 [HasCommercialTerms],[Purpose],[BillingPeriodIndex])
            VALUES ({orderId},{user.Id},{organization.Id},{version.Id},2,1000,'IRR',{operation},{hash},{now},{now},NULL,0,1,0);
            INSERT INTO [commerce].[PaymentAttempts]
                ([Id],[OrderId],[Provider],[Status],[Amount],[Currency],[ClientOperationId],[RequestPayloadHash],
                 [ProviderReference],[CreatedAtUtc],[InitiatedAtUtc],[VerifiedAtUtc],[CompletedAtUtc])
            VALUES ({attemptId},{orderId},'LegacyFixture',4,1000,'IRR',{operation},{hash},{reference},{now},{now},{now},{now});
            INSERT INTO [commerce].[Payments]
                ([Id],[OrderId],[PaymentAttemptId],[Provider],[ProviderReference],[Amount],[Currency],[PaidAtUtc])
            VALUES ({paymentId},{orderId},{attemptId},'LegacyFixture',{reference},1000,'IRR',{now});
            """);
        db.ChangeTracker.Clear();
        return new(orderId, attemptId, paymentId, reference, now);
    }

    private static async Task AssertHistoryAndSchemaAsync(MakanDbContext db, LegacyPayment legacy)
    {
        db.ChangeTracker.Clear();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == legacy.OrderId);
        var attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(a => a.Id == legacy.AttemptId);
        var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == legacy.PaymentId);
        Assert.Equal(OrderStatus.Paid, order.Status); Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(1000m, order.Amount); Assert.Equal(1000m, payment.Amount);
        Assert.Equal(legacy.Reference, payment.ProviderReference); Assert.Equal(legacy.PaidAt, payment.PaidAtUtc);
        Assert.Null(order.ProductTitleSnapshot); Assert.Null(payment.SettlementReference); Assert.Null(payment.ProviderPaidAtUtc);
        Assert.Null(attempt.SettlementReference); Assert.Null(attempt.ProviderPaidAtUtc); Assert.Null(attempt.CallbackReceivedAtUtc);
        Assert.Equal(0, await db.PaymentAttemptEvents.CountAsync());
        Assert.Equal(1, await db.Orders.CountAsync()); Assert.Equal(1, await db.Payments.CountAsync()); Assert.Equal(1, await db.PaymentAttempts.CountAsync());
        Assert.Equal(65, (await db.Database.GetAppliedMigrationsAsync()).Count()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(2, Convert.ToInt32(await ScalarAsync(db, """
            SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
            WHERE SCHEMA_NAME(t.schema_id)='commerce' AND t.name IN ('Orders','PaymentAttempts') AND c.name='RowVersion' AND c.system_type_id=189
            """)));
        Assert.True(Convert.ToInt32(await ScalarAsync(db, "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('commerce.Payments') AND is_unique=1")) >= 3);
        Assert.Equal(1, Convert.ToInt32(await ScalarAsync(db, "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('commerce.PaymentAttemptEvents') AND is_disabled=0 AND delete_referential_action=0")));
        var invalid = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(db,
            $"INSERT INTO commerce.PaymentAttemptEvents (Id,PaymentAttemptId,Type,OccurredAtUtc) VALUES (NEWID(),'{legacy.AttemptId}',99,SYSUTCDATETIME())"));
        Assert.Equal(547, invalid.Number);
        var badParent = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(db,
            "INSERT INTO commerce.PaymentAttemptEvents (Id,PaymentAttemptId,Type,OccurredAtUtc) VALUES (NEWID(),NEWID(),1,SYSUTCDATETIME())"));
        Assert.Equal(547, badParent.Number);
    }

    private static async Task<ExportedSql> ExportAsync(string from)
    {
        var root = FindRoot(); var folder = Path.Combine(root, ".artifacts", "zibal-migration-acceptance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "migration-safe.sql");
        var start = new ProcessStartInfo("powershell") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "tools/production/export-migration-sql.ps1"),
            "-OutputPath", path, "-FromMigration", from, "-ToMigration", Target, "-Configuration",
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "-NoBuild" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("SQL_EXPORT_START_FAILED");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new InvalidOperationException("SQL_EXPORT_DEADLINE"); }
        await Task.WhenAll(stdout, stderr);
        Assert.Equal(0, process.ExitCode);
        var sql = await File.ReadAllTextAsync(path);
        Assert.Contains(Target, sql); Assert.Contains("IF NOT EXISTS", sql); Assert.Contains("__EFMigrationsHistory", sql);
        Assert.False(Regex.IsMatch(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase));
        return new(path, sql, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("REPOSITORY_ROOT_NOT_FOUND");
    }
    private static void SaveEvidence(ExportedSql export, string kind) => File.WriteAllText(Path.ChangeExtension(export.Path, ".acceptance.json"),
        JsonSerializer.Serialize(new { Kind = kind, Migration = Target, SqlSha256 = export.Hash, Passed = true, NormalDatabasesChanged = false }));
    private static async Task<object> ScalarAsync(MakanDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        try { await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; return (await command.ExecuteScalarAsync())!; }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    private static async Task ExecuteAsync(MakanDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        try { await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; command.CommandTimeout = 180; await command.ExecuteNonQueryAsync(); }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    private sealed record LegacyPayment(Guid OrderId, Guid AttemptId, Guid PaymentId, string Reference, DateTime PaidAt);
    private sealed record ExportedSql(string Path, string Sql, string Hash);

    private sealed class OwnedDatabase(string connectionString, string master, string name) : IAsyncDisposable
    {
        public MakanDbContext Context { get; } = new(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connectionString).Options);
        public static async Task<OwnedDatabase> CreateAsync(MakanAppWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var builder = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
                { InitialCatalog = "MakanApp_ZibalAcceptance_" + Guid.NewGuid().ToString("N"), Pooling = false };
            Assert.StartsWith("(localdb)", builder.DataSource, StringComparison.OrdinalIgnoreCase);
            var name = builder.InitialCatalog;
            Assert.Matches("^MakanApp_ZibalAcceptance_[a-f0-9]{32}$", name);
            var master = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
            await using var connection = new SqlConnection(master); await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name"; command.Parameters.AddWithValue("@name", name);
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
            command.Parameters.Clear(); command.CommandText = $"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync();
            return new(builder.ConnectionString, master, name);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            Assert.Matches("^MakanApp_ZibalAcceptance_[a-f0-9]{32}$", name);
            await using var connection = new SqlConnection(master); await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];";
            await command.ExecuteNonQueryAsync();
        }
    }
}
