using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.IntegrationTests;

public sealed partial class OperationsWorkerRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutboxNormalBatchClaimsAtMostFiftyAndLeavesFollowingWorkUnclaimed(bool realtime)
    {
        var ids = realtime ? await SeedRealtimeAsync(51) : await SeedOutboxAsync(51);
        var first = realtime ? await RunRealtimeAsync(new CallbackNotifier()) : await RunOutboxAsync(new CallbackCoordinator());
        Assert.Equal(50, first);
        await using (var db = sql.CreateDb())
        {
            if (realtime)
            {
                var rows = await db.MessagingRealtimeOutboxMessages.AsNoTracking().Where(row => ids.Contains(row.Id)).ToArrayAsync();
                Assert.Equal(50, rows.Count(row => row.DispatchedAtUtc.HasValue));
                Assert.Equal(0, Assert.Single(rows, row => !row.DispatchedAtUtc.HasValue).AttemptCount);
            }
            else
            {
                var rows = await ReadOutboxAsync(ids);
                Assert.Equal(50, rows.Count(row => row.ProcessedAtUtc.HasValue));
                Assert.Equal(0, Assert.Single(rows, row => !row.ProcessedAtUtc.HasValue).AttemptCount);
            }
        }
        Assert.Equal(1, realtime ? await RunRealtimeAsync(new CallbackNotifier()) : await RunOutboxAsync(new CallbackCoordinator()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutboxSqlBackoffProgressionTerminalCeilingAndPoisonProgressAreDeterministic(bool realtime)
    {
        var ids = realtime ? await SeedRealtimeAsync(2) : await SeedOutboxAsync(2);
        string? poisonCursor = null;
        if (realtime)
        {
            await using var db = sql.CreateDb();
            var row = await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == ids[0]);
            var change = await db.MessagingChangeEvents.AsNoTracking().SingleAsync(change => change.Id == row.ChangeEventId);
            poisonCursor = MessagingChangeCursor.Encode(change.ConversationId, change.ChangeSequence);
        }
        var coordinator = new CallbackCoordinator((notification, _) => notification.SourceEventId == ids[0]
            ? Task.FromException(new IOException("synthetic-private-poison-marker")) : Task.CompletedTask);
        var notifier = new CallbackNotifier((notification, _) => notification.Cursor == poisonCursor
            ? Task.FromException(new IOException("synthetic-private-poison-marker")) : Task.CompletedTask);
        using var outcomes = new OutboxOutcomeRecorder(realtime ? WorkerTelemetry.MessagingRealtimeOutbox : WorkerTelemetry.IntegrationOutbox);
        async Task<int> DispatchAsync() => realtime ? await RunRealtimeAsync(notifier) : await RunOutboxAsync(coordinator);
        async Task<(int Attempts, DateTime? Retry, bool Done)> StateAsync()
        {
            await using var db = sql.CreateDb();
            if (realtime)
            {
                var row = await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == ids[0]);
                return (row.AttemptCount, row.NextAttemptAtUtc, row.DispatchedAtUtc.HasValue);
            }
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == ids[0]);
            return (message.AttemptCount, message.NextAttemptAtUtc, message.ProcessedAtUtc.HasValue);
        }

        Assert.Equal(2, await DispatchAsync());
        Assert.Equal((1, (DateTime?)_clock.Now.AddSeconds(2), false), await StateAsync());
        Assert.Equal(0, await DispatchAsync());
        Assert.Equal(1, (await StateAsync()).Attempts);
        for (var attempt = 2; attempt <= 10; attempt++)
        {
            var previous = await StateAsync();
            _clock.Advance(previous.Retry!.Value - _clock.Now);
            Assert.Equal(1, await DispatchAsync());
            var current = await StateAsync();
            Assert.Equal(attempt, current.Attempts);
            Assert.False(current.Done);
            Assert.Equal(attempt == 10 ? null : (DateTime?)_clock.Now.AddSeconds(realtime ? attempt * 2 : attempt * attempt * 2), current.Retry);
        }
        Assert.Equal(0, await DispatchAsync());
        Assert.Equal(11, outcomes.Count("Claimed"));
        Assert.Equal(1, outcomes.Count("Completed"));
        Assert.Equal(9, outcomes.Count("RetryScheduled"));
        Assert.Equal(1, outcomes.Count("Failed"));
        Assert.Equal(0, outcomes.Count("LeaseLost"));
    }

    [Fact]
    public async Task ExpiredGeneralOwnerDoesNotClaimRetryWasScheduledAndRecoveryRemainsSafe()
    {
        var id = Assert.Single(await SeedOutboxAsync(1));
        using var outcomes = new OutboxOutcomeRecorder(WorkerTelemetry.IntegrationOutbox);
        using var caller = new CancellationTokenSource();
        outcomes.OnOutcome = outcome => { if (outcome == "LeaseLost") caller.Cancel(); };
        var logs = new OutboxLogCapture();
        await using (var db = sql.CreateDb())
        {
            var coordinator = new CallbackCoordinator((_, _) =>
            {
                _clock.Advance(TimeSpan.FromSeconds(31));
                return Task.FromException(new IOException("synthetic-expired-owner-private-marker"));
            });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new OutboxProcessor(db, coordinator, new NoopSubscriptionActivation(), _clock, sql.Telemetry, logs)
                    .DispatchBatchAsync(caller.Token));
        }
        var pending = Assert.Single(await ReadOutboxAsync([id]));
        Assert.NotNull(pending.ClaimToken);
        Assert.Null(pending.NextAttemptAtUtc);
        Assert.Null(pending.ProcessedAtUtc);
        Assert.Empty(logs.Messages);
        Assert.Equal(1, outcomes.Count("LeaseLost"));
        Assert.Equal(0, outcomes.Count("RetryScheduled"));
        Assert.Equal(0, outcomes.Count("Failed"));
        Assert.Equal(1, await RunOutboxAsync(new CallbackCoordinator()));
        Assert.NotNull(Assert.Single(await ReadOutboxAsync([id])).ProcessedAtUtc);
        Assert.Equal(1, outcomes.Count("LeaseRecovered"));
    }

    [Fact]
    public async Task GeneralCommitAcknowledgementLossCannotDuplicateDurableEffectOrInventRetryOutcome()
    {
        var id = Assert.Single(await SeedOutboxAsync(1));
        var fault = new OutboxCommitAcknowledgementLoss();
        using var outcomes = new OutboxOutcomeRecorder(WorkerTelemetry.IntegrationOutbox);
        var logs = new OutboxLogCapture();
        await using (var db = sql.CreateDb(fault))
            Assert.Equal(1, await new OutboxProcessor(db, new CallbackCoordinator((_, _) =>
            {
                fault.Armed = true;
                return Task.CompletedTask;
            }), new NoopSubscriptionActivation(), _clock, sql.Telemetry, logs).DispatchBatchAsync(CancellationToken.None));
        Assert.True(fault.Thrown);
        Assert.NotNull(Assert.Single(await ReadOutboxAsync([id])).ProcessedAtUtc);
        Assert.Equal(0, await RunOutboxAsync(new CallbackCoordinator()));
        await using var verify = sql.CreateDb();
        Assert.Equal(1, await verify.Notifications.CountAsync(row => row.SourceEventId == id));
        Assert.Equal(0, outcomes.Count("RetryScheduled"));
        Assert.Equal(0, outcomes.Count("Completed"));
        Assert.Empty(logs.Messages);
    }

    [Fact]
    public async Task GeneralNotificationDedupUsesScalarLookupForOnlyTheCurrentRecipient()
    {
        var id = Assert.Single(await SeedOutboxAsync(1));
        await using (var seed = sql.CreateDb())
        {
            for (var index = 0; index < 60; index++)
            {
                var user = User.Create(_clock.Now);
                seed.Users.Add(user);
                seed.Notifications.Add(DomainNotification.Create(id, user.Id, null, NotificationCategory.Educational,
                    NotificationType.AssignmentPublished, "اعلان تست", "گیرندهٔ مستقل", NotificationResourceType.Assignment, Guid.NewGuid(), _clock.Now));
            }
            await seed.SaveChangesAsync();
        }
        var observer = new OutboxScalarDedupObserver();
        await using (var db = sql.CreateDb(observer))
            Assert.Equal(1, await new OutboxProcessor(db, new CallbackCoordinator(), new NoopSubscriptionActivation(), _clock,
                sql.Telemetry, NullLogger<OutboxProcessor>.Instance).DispatchBatchAsync(CancellationToken.None));
        var command = Assert.Single(observer.Commands);
        Assert.Contains("EXISTS", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[RecipientUserId]", command, StringComparison.Ordinal);
        await using var verify = sql.CreateDb();
        Assert.Equal(61, await verify.Notifications.CountAsync(row => row.SourceEventId == id));
    }

    [Fact]
    public async Task RealtimeUnexpiredClaimCannotBeOwnedByASecondIndependentProcessor()
    {
        var id = Assert.Single(await SeedRealtimeAsync(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = RunRealtimeAsync(new CallbackNotifier(async (_, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); }));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, await RunRealtimeAsync(new CallbackNotifier()));
            await using var db = sql.CreateDb();
            Assert.Equal(1, (await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id)).AttemptCount);
        }
        finally { release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealtimeCompletionAndRetryReadFreshLeaseAfterActualSqlLockWait(bool notifierFails)
    {
        var id = Assert.Single(await SeedRealtimeAsync(1));
        await using var blocker = sql.CreateDb();
        await blocker.Database.OpenConnectionAsync();
        var connection = (SqlConnection)blocker.Database.GetDbConnection();
        IDbContextTransaction? transaction = null;
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var outcomes = new OutboxOutcomeRecorder(WorkerTelemetry.MessagingRealtimeOutbox);
        outcomes.OnOutcome = outcome => { if (outcome == "LeaseLost") caller.Cancel(); };
        var processing = RunRealtimeAsync(new CallbackNotifier(async (_, _) =>
        {
            transaction = await blocker.Database.BeginTransactionAsync();
            _ = await blocker.MessagingRealtimeOutboxMessages.FromSqlInterpolated($"SELECT * FROM [messaging].[RealtimeOutbox] WITH (XLOCK,HOLDLOCK) WHERE [Id]={id}")
                .AsNoTracking().SingleAsync();
            locked.TrySetResult();
            if (notifierFails) throw new IOException("synthetic-private-notifier-marker");
        }), caller.Token);
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await WaitForBlockedOutboxCommandAsync(connection, processing));
            _clock.Advance(TimeSpan.FromSeconds(31));
            await transaction!.CommitAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            caller.Cancel();
            if (transaction is not null)
            {
                if (transaction.GetDbTransaction().Connection is not null) await transaction.RollbackAsync();
                await transaction.DisposeAsync();
            }
            _ = await Record.ExceptionAsync(() => processing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await using (var verify = sql.CreateDb())
        {
            var row = await verify.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id);
            Assert.Null(row.DispatchedAtUtc);
            Assert.Null(row.NextAttemptAtUtc);
            Assert.NotNull(row.ClaimedUntilUtc);
            Assert.Equal(1, row.AttemptCount);
        }
        Assert.Equal(1, outcomes.Count("LeaseLost"));
        Assert.Equal(0, outcomes.Count("Completed"));
        Assert.Equal(0, outcomes.Count("RetryScheduled"));
        Assert.Equal(1, await RunRealtimeAsync(new CallbackNotifier()));
        Assert.Equal(1, outcomes.Count("LeaseRecovered"));
    }

    private async Task<bool> WaitForBlockedOutboxCommandAsync(SqlConnection blocker, Task processing)
    {
        await using var observer = new SqlConnection(sql.ConnectionString);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        await observer.OpenAsync(deadline.Token);
        var started = Stopwatch.GetTimestamp();
        while (!processing.IsCompleted && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(3))
        {
            await using var command = observer.CreateCommand();
            command.CommandText = "SELECT COUNT_BIG(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@blocker AND wait_type LIKE N'LCK_M_%'";
            command.Parameters.AddWithValue("@blocker", blocker.ServerProcessId);
            command.CommandTimeout = 1;
            if ((long)(await command.ExecuteScalarAsync(deadline.Token))! > 0) return true;
            await Task.Delay(20, deadline.Token);
        }
        return false;
    }

    private sealed class OutboxCommitAcknowledgementLoss : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public bool Thrown { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Armed && !Thrown) { Thrown = true; throw new IOException("synthetic-private-commit-ack-marker"); }
            return Task.CompletedTask;
        }
    }

    private sealed class OutboxScalarDedupObserver : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [notification].[Notifications]", StringComparison.Ordinal)) Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class OutboxLogCapture : ILogger<OutboxProcessor>
    {
        public List<string> Messages { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Assert.Null(exception); Messages.Add(formatter(state, exception)); }
    }

    private sealed class OutboxOutcomeRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly Dictionary<string, long> _counts = [];
        public Action<string>? OnOutcome { get; set; }
        public OutboxOutcomeRecorder(string worker)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            { if (instrument.Meter.Name == WorkerTelemetry.MeterName && instrument.Name == "makan.worker.outcomes") listener.EnableMeasurementEvents(instrument); };
            _listener.SetMeasurementEventCallback<long>((_, count, tags, _) =>
            {
                var values = tags.ToArray();
                Assert.Equal(2, values.Length);
                Assert.All(values, tag => Assert.Contains(tag.Key, new[] { "worker", "outcome" }));
                if (!Equals(values.Single(tag => tag.Key == "worker").Value, worker)) return;
                var outcome = Assert.IsType<string>(values.Single(tag => tag.Key == "outcome").Value);
                lock (_counts) _counts[outcome] = _counts.GetValueOrDefault(outcome) + count;
                OnOutcome?.Invoke(outcome);
            });
            _listener.Start();
        }
        public long Count(string outcome) { lock (_counts) return _counts.GetValueOrDefault(outcome); }
        public void Dispose() => _listener.Dispose();
    }
}
