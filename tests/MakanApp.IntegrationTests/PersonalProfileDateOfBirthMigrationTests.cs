using System.Text.RegularExpressions;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PersonalProfileDateOfBirthMigrationTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task ProfileDateMigrationPreservesExistingPersonAndAgeEvidenceAndIsIdempotent()
    {
        const string baseline = "20261024184613_RemovePlatformMfa";
        const string migration = "20261028102653_AddSelfDeclaredProfileDateOfBirth";
        await using var scope = factory.Services.CreateAsyncScope();
        var root = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(root.Database.GetConnectionString())
        {
            InitialCatalog = "MakanApp_ProfileDob_Migration_" + Guid.NewGuid().ToString("N")
        };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MakanApp_ProfileDob_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(baseline);
            var now = DateTime.UtcNow;
            var personId = Guid.NewGuid();
            // مدل جدید ستون DOB دارد؛ دادهٔ نسخهٔ قبل با فهرست ستون‌های همان نسخه وارد می‌شود.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].[Persons] ([Id],[FirstName],[LastName],[DisplayName],[CreatedAtUtc])
                VALUES ({personId},N'نام قبلی',N'نام خانوادگی',NULL,{now})
                """);
            var user = User.Create(now);
            user.SetProfile(personId, "existing.profile", "EXISTING.PROFILE", now);
            // دادهٔ نسخهٔ قبلی با ستون‌های همان نسخه درج می‌شود؛ مدل جاری ممکن است ستون جدیدتری داشته باشد.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].[Users] ([Id],[PersonId],[Username],[NormalizedUsername],[CreatedAtUtc],[ProfileCompletedAtUtc],[AccountStatus],[CommunicationAgeCategory],[CommunicationAgeVerificationStatus])
                VALUES ({user.Id},{personId},N'existing.profile',N'EXISTING.PROFILE',{now},{now},1,0,0)
                """);
            var evidence = CommunicationAgeVerification.Create(user.Id, new DateOnly(1990, 1, 1),
                CommunicationAgeCategory.Unknown, false, CommunicationAgeVerificationSource.PlatformVerification,
                user.Id, null, "شاهد قبلی مستقل از تاریخ تولد خوداظهاری", now);
            db.CommunicationAgeVerifications.Add(evidence);
            await db.SaveChangesAsync();

            var sql = migrator.GenerateScript(baseline, migration, MigrationsSqlGenerationOptions.Idempotent);
            Assert.Contains("[DateOfBirth] date NULL", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("VerifiedBirthDate", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
            await db.Database.OpenConnectionAsync();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                    .Where(batch => !string.IsNullOrWhiteSpace(batch)))
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = batch;
                    await command.ExecuteNonQueryAsync();
                }
            }
            await db.Database.CloseConnectionAsync();
            db.ChangeTracker.Clear();
            Assert.Contains(migration, await db.Database.GetAppliedMigrationsAsync());
            // بررسی DOB مستقل از این است که Migrationهای بعدی چه زمانی به پروژه اضافه می‌شوند.
            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var person = await db.Persons.SingleAsync(p => p.Id == personId);
            Assert.Equal("نام قبلی", person.FirstName);
            Assert.Null(person.DateOfBirth);
            Assert.True((await db.Users.SingleAsync(u => u.Id == user.Id)).IsProfileComplete);
            Assert.Equal(new DateOnly(1990, 1, 1), (await db.CommunicationAgeVerifications.SingleAsync()).VerifiedBirthDate);
            person.SetDateOfBirth(new DateOnly(2005, 8, 21), now);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(new DateOnly(2005, 8, 21), (await db.Persons.SingleAsync()).DateOfBirth);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
