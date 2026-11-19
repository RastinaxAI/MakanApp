using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MakanApp.IntegrationTests;

// فقط شناسه، زمان و نوع خطا ثبت می‌شوند؛ متن پیام، SQL و credential وارد شواهد نمی‌شوند.
public sealed class RealtimeTestDiagnostics
{
    private readonly ConcurrentQueue<Entry> _entries = new();
    public void Record(Guid hostId, Guid resourceId, string stage)
    {
        _entries.Enqueue(new(hostId, resourceId, stage, DateTime.UtcNow));
        while (_entries.Count > 5000) _entries.TryDequeue(out _);
    }

    public Entry[] ForResource(Guid resourceId) => _entries.Where(e => e.ResourceId == resourceId).ToArray();
    public Entry[] RecentSqlFailures() => _entries.Where(e => e.Stage.StartsWith("Sql:", StringComparison.Ordinal)).TakeLast(10).ToArray();
    public sealed record Entry(Guid HostId, Guid ResourceId, string Stage, DateTime AtUtc);
}

public sealed class RealtimeSqlDiagnostics(RealtimeTestDiagnostics diagnostics) : DbCommandInterceptor
{
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("RealtimeOutbox", StringComparison.OrdinalIgnoreCase))
            diagnostics.Record(Guid.Empty, Guid.Empty, "Sql:" + (eventData.Exception is SqlException sql ? sql.Number.ToString() : eventData.Exception.GetType().Name));
        return Task.CompletedTask;
    }
}

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<T> WaitForRealtimeAsync<T>(Task<T> pending, Guid resourceId, TimeSpan timeout)
    {
        try { return await pending.WaitAsync(timeout); }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            string state;
            try
            {
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var scope = Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                var rows = await (from item in db.MessagingRealtimeOutboxMessages.AsNoTracking()
                    join change in db.MessagingChangeEvents.AsNoTracking() on item.ChangeEventId equals change.Id
                    where change.ResourceId == resourceId
                    select new { item.Id, item.AttemptCount, item.ClaimedUntilUtc, item.NextAttemptAtUtc, item.DispatchedAtUtc, change.ChangeType })
                    .ToArrayAsync(budget.Token);
                var workers = Services.GetServices<IHostedService>().OfType<MessagingRealtimeOutboxDispatcher>()
                    .Select(w => w.ExecuteTask?.Status.ToString()).ToArray();
                state = JsonSerializer.Serialize(new { ResourceId = resourceId,
                    RootHostId = Services.GetRequiredService<RealtimeFailureSwitch>().HostId,
                    Publications = RealtimeDiagnostics.ForResource(resourceId), Rows = rows, Workers = workers,
                    Clock = Clock.GetUtcNow(), SqlFailures = RealtimeDiagnostics.RecentSqlFailures() });
            }
            catch (Exception diagnosticFailure) { state = "DiagnosticFailure=" + diagnosticFailure.GetType().Name; }
            throw new TimeoutException("REALTIME_WAIT_FAILED " + state, exception);
        }
    }
}
