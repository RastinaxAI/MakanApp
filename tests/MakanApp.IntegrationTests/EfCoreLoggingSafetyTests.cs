using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Api.Observability;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class EfCoreLoggingSafetyTests
{
    private const string CommandMarker = "synthetic-ef-command-private-marker";
    private const string QueryMarker = "synthetic-ef-query-private-marker";
    private const string UpdateMarker = "ef-update-private-marker";
    private const string ConnectionMarker = "MakanApp_Ef_Private_Connection_Marker";

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("Test", false)]
    [InlineData("Development", true)]
    public async Task RealSqlFailuresUseSafeReplacementWithoutLosingErrorSeverityOrDevelopmentDiagnostics(string environment, bool rawExpected)
    {
        await using var isolated = await IsolatedDatabase.CreateAsync();
        var logs = new SensitiveLogTestControl();
        using var services = CreateServices(environment, isolated.ConnectionString, isolated.Factory.StorageRootPath, logs);
        using var activity = new Activity("ef-safety-test").Start();
        activity.SetTag("makan.correlation_id", "ef-safe-correlation");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
                "THROW 51001, N'synthetic-ef-sql-message-marker', 1; /* " + CommandMarker + " */"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.SqlQueryRaw<int>(
                "SELECT CONVERT(int, N'" + QueryMarker + "') AS [Value]").ToArrayAsync());

            var now = DateTime.UtcNow;
            var personOne = Person.Create("Synthetic", "One", null, now);
            var personTwo = Person.Create("Synthetic", "Two", null, now);
            var first = User.Create(now);
            var second = User.Create(now);
            first.SetProfile(personOne.Id, UpdateMarker, UpdateMarker.ToUpperInvariant(), now);
            second.SetProfile(personTwo.Id, UpdateMarker, UpdateMarker.ToUpperInvariant(), now);
            db.AddRange(personOne, personTwo, first, second);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            // هر delegate، executor تازه دارد؛ هشدار واقعی compilation بین حالت‌های محیط cache نمی‌شود.
            var firstWithoutOrdering = EF.CompileAsyncQuery((MakanDbContext context) => context.Users.FirstOrDefault());
            _ = await firstWithoutOrdering(db);
        }

        var unavailable = new SqlConnectionStringBuilder
        {
            DataSource = "tcp:127.0.0.1,1",
            InitialCatalog = ConnectionMarker,
            IntegratedSecurity = true,
            ConnectTimeout = 1,
            Encrypt = true,
            TrustServerCertificate = false
        };
        using (var disconnectedServices = CreateServices(environment, unavailable.ConnectionString, isolated.Factory.StorageRootPath, logs))
        await using (var scope = disconnectedServices.CreateAsyncScope())
            await Assert.ThrowsAsync<SqlException>(() => scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.OpenConnectionAsync());

        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        loggerFactory.CreateLogger("MakanApp.SafeOperations").LogCritical("synthetic-unrelated-critical-visible");
        loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore.Infrastructure").LogWarning("synthetic-unrelated-ef-visible");
        var combined = string.Join('\n', logs.Snapshot()) + (rawExpected
            ? string.Join('\n', logs.Entries().Select(entry => entry.ExceptionText))
            : JsonSerializer.Serialize(logs.Entries()));
        foreach (var marker in new[] { CommandMarker, QueryMarker, UpdateMarker, ConnectionMarker, "synthetic-ef-sql-message-marker" })
            Assert.Equal(rawExpected, combined.Contains(marker, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("synthetic-unrelated-critical-visible", combined);
        Assert.Contains("synthetic-unrelated-ef-visible", combined);

        var safe = logs.Entries().Where(entry => entry.EventId.Name == "SafeEfCoreDiagnostic").ToArray();
        if (rawExpected)
        {
            Assert.Empty(safe);
            return;
        }

        Assert.NotEmpty(safe);
        foreach (var category in new[] { "Database.Command", "Database.Connection", "Query", "Update" })
            Assert.Contains(safe, entry => Equals(entry.Properties["Category"], "Microsoft.EntityFrameworkCore." + category));
        Assert.Contains(safe, entry => entry.Level == LogLevel.Warning && Equals(entry.Properties["HasException"], false));
        Assert.All(safe, entry =>
        {
            Assert.Null(entry.ExceptionText);
            Assert.InRange(entry.Level, LogLevel.Warning, LogLevel.Critical);
            if (Equals(entry.Properties["HasException"], true)) Assert.InRange(entry.Level, LogLevel.Error, LogLevel.Critical);
            Assert.Contains(Assert.IsType<string>(entry.Properties["Category"]), EfCoreLoggingSafety.Categories);
            Assert.InRange(Assert.IsType<int>(entry.Properties["DatabaseEventId"]), 0, 99999);
            Assert.Equal("ef-safe-correlation", entry.Properties["CorrelationId"]);
            Assert.Equal(activity.TraceId.ToString(), entry.Properties["TraceId"]);
        });
        Assert.DoesNotContain(logs.Entries(), entry => EfCoreLoggingSafety.Categories.Contains(entry.Category));
    }

    private static ServiceProvider CreateServices(string environment, string connectionString, string storageRoot, SensitiveLogTestControl logs)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MakanDatabase"] = connectionString,
            ["Identity:SecurityKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Storage:RootPath"] = storageRoot,
            ["Sms:Provider"] = "Unavailable"
        }).Build();
        var registrations = new ServiceCollection();
        registrations.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(new RecordingTestLoggerProvider(logs));
            foreach (var category in EfCoreLoggingSafety.Categories)
                logging.AddFilter<RecordingTestLoggerProvider>(category, LogLevel.Trace);
        });
        ProductionLoggingSafety.Configure(registrations, new TestEnvironment { EnvironmentName = environment });
        registrations.AddSingleton<TimeProvider>(TimeProvider.System);
        registrations.AddInfrastructure(configuration, environment);
        return registrations.BuildServiceProvider();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "MakanApp.EfLogging.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class IsolatedDatabase(MakanAppWebApplicationFactory factory, string connectionString) : IAsyncDisposable
    {
        public MakanAppWebApplicationFactory Factory => factory;
        public string ConnectionString => connectionString;
        public static async Task<IsolatedDatabase> CreateAsync()
        {
            var id = Guid.NewGuid().ToString("N");
            var factory = new MakanAppWebApplicationFactory("MakanApp_Recovery_EfLogging_" + id,
                Path.Combine(Path.GetTempPath(), "MakanApp-StorageTests", "EfLogging-" + id),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            try
            {
                await ((IAsyncLifetime)factory).InitializeAsync();
                await using var scope = factory.Services.CreateAsyncScope();
                var connectionString = scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString()!;
                return new(factory, connectionString);
            }
            catch
            {
                await ((IAsyncLifetime)factory).DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() => await ((IAsyncLifetime)factory).DisposeAsync();
    }
}
