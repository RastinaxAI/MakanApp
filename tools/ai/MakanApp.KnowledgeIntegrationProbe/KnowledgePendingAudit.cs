using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MakanApp.KnowledgeIntegrationProbe;

internal static class KnowledgePendingAudit
{
    public static async Task<int> RunAsync(string repository)
    {
        var root = Path.GetFullPath(repository);
        if (!File.Exists(Path.Combine(root, "MakanApp.sln"))) return 2;
        foreach (var environment in new[] { "Production", "Test" })
        {
            try
            {
                var config = new ConfigurationBuilder().SetBasePath(Path.Combine(root, "MakanApp.Api"))
                    .AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environment}.json").Build();
                var settings = new SqlConnectionStringBuilder(config.GetConnectionString("MakanDatabase"));
                if (settings.DataSource is not ("." or "localhost" or "(local)" or "127.0.0.1") ||
                    settings.InitialCatalog is not ("MakanApp" or "MakanApp_Test"))
                {
                    Write(new { environment, status = "BLOCKED_BY_ACCESS", reason = "LOCAL_APPROVED_DATABASE_REQUIRED", databaseChanged = false });
                    continue;
                }
                settings.ConnectTimeout = 5;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await using var connection = new SqlConnection(settings.ConnectionString);
                await connection.OpenAsync(timeout.Token);
                await using var command = connection.CreateCommand();
                command.CommandTimeout = 10;
                // فقط Aggregate خواندنی؛ هیچ Mutation، payload، شناسهٔ کاربر یا credential خروجی ندارد.
                command.CommandText = """
                    SELECT [Status],COUNT_BIG(*),MIN([CreatedAtUtc]) FROM [commerce].[UsageReservations]
                    WHERE [QuotaKey]=@quota GROUP BY [Status] ORDER BY [Status];
                    SELECT COUNT_BIG(*) FROM [platform].[OutboxMessages]
                    WHERE [ProcessedAtUtc] IS NULL AND [MessageType] LIKE 'knowledge.%';
                    """;
                command.Parameters.Add("@quota", SqlDbType.Int).Value = 5;
                await using var reader = await command.ExecuteReaderAsync(timeout.Token);
                var rows = new List<object>();
                while (await reader.ReadAsync(timeout.Token))
                    rows.Add(new { status = reader.GetInt32(0) switch { 1 => "Reserved", 2 => "Consumed", 3 => "Released", 4 => "Unknown", _ => "Invalid" },
                        count = reader.GetInt64(1), oldestCreatedAtUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2).ToString("O") });
                await reader.NextResultAsync(timeout.Token);
                long? pending = await reader.ReadAsync(timeout.Token) ? reader.GetInt64(0) : null;
                Write(new { environment, database = settings.InitialCatalog, scope = "LOCAL_ONLY_NOT_BACKEND_SERVER", status = "READ_ONLY_AUDIT_COMPLETED",
                    reservations = rows, knowledgeOutboxPending = pending, remoteAiQueueVerified = false,
                    stuckThreshold = "NOT_DEFINED_FOR_USAGE_RESERVATIONS", retryOrCleanupExecuted = false, databaseChanged = false });
            }
            catch (SqlException error)
            { Write(new { environment, status = error.Number == 18456 ? "BLOCKED_BY_ACCESS" : "BLOCKED_BY_ENVIRONMENT", sqlErrorNumber = error.Number, databaseChanged = false }); }
            catch (Exception error)
            { Write(new { environment, status = "BLOCKED_BY_ENVIRONMENT", failureType = error.GetType().Name, databaseChanged = false }); }
        }
        return 0;
    }
    private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
}
