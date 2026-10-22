using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkerRecoverySqlCollection
{
    public const string Name = "MakanApp isolated worker recovery SQL";
}

[Collection(WorkerRecoverySqlCollection.Name)]
public sealed partial class OperationsWorkerRecoveryTests(WorkerRecoverySqlFixture sql) : IClassFixture<WorkerRecoverySqlFixture>, IAsyncLifetime
{
    private readonly RecoveryClock _clock = new();

    public async Task InitializeAsync()
    {
        await using var db = sql.CreateDb();
        // فقط باقی‌ماندهٔ تست قبلی در DB اختصاصی همین کلاس نهایی می‌شود؛ هیچ داده‌ای حذف نمی‌شود.
        await db.OutboxMessages.Where(row => row.ProcessedAtUtc == null).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.AttemptCount, OutboxProcessor.MaximumAttempts)
            .SetProperty(row => row.ClaimToken, (Guid?)null).SetProperty(row => row.ClaimedUntilUtc, (DateTime?)null)
            .SetProperty(row => row.NextAttemptAtUtc, (DateTime?)null));
        await db.MessagingRealtimeOutboxMessages.Where(row => row.DispatchedAtUtc == null).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.AttemptCount, MessagingRealtimeOutboxProcessor.MaximumAttempts)
            .SetProperty(row => row.ClaimedUntilUtc, (DateTime?)null).SetProperty(row => row.NextAttemptAtUtc, (DateTime?)null));
        await db.NotificationDeliveryAttempts.Where(row => row.Status != NotificationDeliveryStatus.Delivered).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.Status, NotificationDeliveryStatus.Failed)
            .SetProperty(row => row.FailureReason, NotificationDeliveryFailureReason.ProviderUnavailable)
            .SetProperty(row => row.ClaimToken, (Guid?)null).SetProperty(row => row.ClaimedUntilUtc, (DateTime?)null)
            .SetProperty(row => row.NextAttemptAtUtc, (DateTime?)null));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepairUsesScopedReadCommittedAfterCompletedSerializableTransactionOnSameConnection(bool realtime)
    {
        var id = Assert.Single(realtime ? await SeedRealtimeAsync(1) : await SeedOutboxAsync(1));
        await using var db = sql.CreateDb();
        await db.Database.OpenConnectionAsync();
        await using (var previous = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            await previous.CommitAsync();
        var previousIsolation = await db.Database.SqlQuery<int>(
            $"SELECT CONVERT(int, transaction_isolation_level) AS [Value] FROM sys.dm_exec_sessions WHERE session_id = @@SPID").SingleAsync();
        Assert.Equal(4, previousIsolation);
        if (realtime)
        {
            await new MessagingRealtimeOutboxProcessor(db, new CallbackNotifier(), _clock, sql.Telemetry,
                NullLogger<MessagingRealtimeOutboxProcessor>.Instance).DispatchBatchAsync(CancellationToken.None);
            Assert.NotNull((await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id)).DispatchedAtUtc);
        }
        else
        {
            await new OutboxProcessor(db, new CallbackCoordinator(), new NoopSubscriptionActivation(), _clock, sql.Telemetry,
                NullLogger<OutboxProcessor>.Instance).DispatchBatchAsync(CancellationToken.None);
            Assert.NotNull((await db.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id)).ProcessedAtUtc);
        }
    }

    [Fact]
    public async Task OutboxRcsiConcurrentWorkersClaimOneAtATimeWithoutExpiringFollowingWork()
    {
        var ids = await SeedOutboxAsync(2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new CallbackCoordinator(async (_, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); });
        var first = RunOutboxAsync(coordinator);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            _clock.Advance(TimeSpan.FromSeconds(40));
            await RunOutboxAsync(new CallbackCoordinator()).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10)); }
        var rows = await ReadOutboxAsync(ids);
        Assert.All(rows, row => { Assert.True(row.ProcessedAtUtc.HasValue); Assert.Equal(1, row.AttemptCount); });
        await using var db = sql.CreateDb();
        Assert.Equal(2, await db.Notifications.CountAsync(row => ids.Contains(row.SourceEventId)));
    }

    [Fact]
    public async Task OutboxPoisonLastAttemptBecomesTerminalAndFollowingHealthyWorkProgresses()
    {
        var healthy = Assert.Single(await SeedOutboxAsync(1));
        var poison = OutboxMessage.Create(Guid.NewGuid(), NotificationOutboxContract.AssignmentPublishedMessageType, "{malformed", _clock.Now.AddMinutes(-1));
        for (var index = 0; index < OutboxProcessor.MaximumAttempts - 1; index++)
            poison.Claim(Guid.NewGuid(), _clock.Now.AddSeconds(-20 + index), _clock.Now.AddSeconds(-19 + index));
        await using (var db = sql.CreateDb()) { db.OutboxMessages.Add(poison); await db.SaveChangesAsync(); }
        await RunOutboxAsync(new CallbackCoordinator());
        var rows = await ReadOutboxAsync([healthy, poison.Id]);
        var failed = Assert.Single(rows, row => row.Id == poison.Id);
        Assert.Null(failed.ProcessedAtUtc); Assert.Null(failed.ClaimToken); Assert.Null(failed.NextAttemptAtUtc);
        Assert.Equal(OutboxProcessor.MaximumAttempts, failed.AttemptCount);
        Assert.Equal(OutboxProcessor.AttemptsExhaustedErrorCode, failed.LastErrorCode);
        Assert.NotNull(Assert.Single(rows, row => row.Id == healthy).ProcessedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExhaustedCrashRepairIsBoundedAndDoesNotReterminalizeSameRows(bool realtime)
    {
        var ids = realtime ? await SeedRealtimeAsync(61, exhausted: true) : await SeedOutboxAsync(61, exhausted: true);
        async Task DispatchAsync()
        {
            if (realtime) await RunRealtimeAsync(new CallbackNotifier());
            else await RunOutboxAsync(new CallbackCoordinator());
        }
        async Task<(int Cleared, string[] Versions)> SnapshotAsync()
        {
            await using var db = sql.CreateDb();
            if (realtime)
            {
                var rows = await db.MessagingRealtimeOutboxMessages.AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToArrayAsync();
                Assert.All(rows, row => { Assert.Null(row.DispatchedAtUtc); Assert.Equal(10, row.AttemptCount); });
                return (rows.Count(row => row.ClaimedUntilUtc == null), rows.Select(row => Convert.ToHexString(row.RowVersion)).ToArray());
            }
            var messages = await db.OutboxMessages.AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToArrayAsync();
            Assert.All(messages, row => { Assert.Null(row.ProcessedAtUtc); Assert.Equal(10, row.AttemptCount); });
            return (messages.Count(row => row.ClaimedUntilUtc == null), messages.Select(row => Convert.ToHexString(row.RowVersion)).ToArray());
        }
        await DispatchAsync(); Assert.Equal(50, (await SnapshotAsync()).Cleared);
        await DispatchAsync(); var terminal = await SnapshotAsync(); Assert.Equal(61, terminal.Cleared);
        await DispatchAsync(); Assert.Equal(terminal.Versions, (await SnapshotAsync()).Versions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutboxCancellationRollsBackBusinessEffectAndNewScopeRecoversDurableClaim(bool nonCancellationException)
    {
        var id = Assert.Single(await SeedOutboxAsync(1));
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = RunOutboxAsync(new CallbackCoordinator(async (_, ct) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) when (nonCancellationException)
            { throw new IOException("خطای کنترل‌شدهٔ I/O پس از درخواست لغو."); }
        }), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); caller.Cancel();
        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(caller.Token, cancelled.CancellationToken);
        await using (var db = sql.CreateDb()) Assert.Equal(0, await db.Notifications.CountAsync(row => row.SourceEventId == id));
        var abandoned = Assert.Single(await ReadOutboxAsync([id])); Assert.NotNull(abandoned.ClaimToken); Assert.Null(abandoned.ProcessedAtUtc);
        Assert.Equal(1, abandoned.AttemptCount); Assert.Null(abandoned.NextAttemptAtUtc); Assert.Null(abandoned.LastErrorCode);
        _clock.Advance(TimeSpan.FromSeconds(31)); await RunOutboxAsync(new CallbackCoordinator());
        var recovered = Assert.Single(await ReadOutboxAsync([id])); Assert.Equal(2, recovered.AttemptCount); Assert.NotNull(recovered.ProcessedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealtimeNewWorkerOwnsReceiptWhenOldNotifierCompletesOrFailsLate(bool oldFails)
    {
        var ids = await SeedRealtimeAsync(2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var notifier = new CallbackNotifier(async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) != 1) return;
            entered.TrySetResult(); await release.Task.WaitAsync(ct);
            if (oldFails) throw new InvalidOperationException("کنترل خطای notifier قدیمی در تست.");
        });
        var old = RunRealtimeAsync(notifier);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); _clock.Advance(TimeSpan.FromSeconds(31));
            await RunRealtimeAsync(notifier).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { release.TrySetResult(); await old.WaitAsync(TimeSpan.FromSeconds(10)); }
        await using var db = sql.CreateDb();
        var rows = await db.MessagingRealtimeOutboxMessages.AsNoTracking().Where(row => ids.Contains(row.Id)).ToArrayAsync();
        Assert.All(rows, row => { Assert.NotNull(row.DispatchedAtUtc); Assert.Null(row.ClaimedUntilUtc); Assert.Null(row.NextAttemptAtUtc); });
        Assert.Equal(2, Assert.Single(rows, row => row.Id == ids[0]).AttemptCount);
        Assert.Equal(1, Assert.Single(rows, row => row.Id == ids[1]).AttemptCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealtimeWaitIsBoundedEvenWhenNotifierIgnoresCancellation(bool cancelCaller)
    {
        var ids = await SeedRealtimeAsync(2);
        var id = ids[0];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        var work = RunRealtimeAsync(new CallbackNotifier((_, _) => { entered.TrySetResult(); return late.Task; }), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancelCaller)
        {
            caller.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else await work.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(late.Task.IsCompleted);
        await using (var db = sql.CreateDb())
        {
            var abandoned = await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id);
            Assert.Null(abandoned.DispatchedAtUtc); Assert.NotNull(abandoned.ClaimedUntilUtc); Assert.Null(abandoned.NextAttemptAtUtc);
            var following = await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == ids[1]);
            Assert.Equal(0, following.AttemptCount); Assert.Null(following.ClaimedUntilUtc);
        }
        late.SetException(new InvalidOperationException("خطای دیررس کنترل‌شده بدون انتشار جزئیات."));
        _clock.Advance(TimeSpan.FromSeconds(31)); await RunRealtimeAsync(new CallbackNotifier());
        await using var verification = sql.CreateDb();
        var recovered = await verification.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id);
        Assert.Equal(2, recovered.AttemptCount); Assert.NotNull(recovered.DispatchedAtUtc);
        Assert.Equal(1, (await verification.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == ids[1])).AttemptCount);
    }

    [Fact]
    public async Task ProviderAcceptanceBeforeSqlFailureCanRetrySameLogicalDeliveryWithoutDuplicateNotification()
    {
        var attemptId = await SeedDeliveryAsync();
        var failure = new CompletionInterceptor(waitForCancellation: false);
        var provider = new CallbackPushProvider(() => failure.Armed = true);
        await using (var db = sql.CreateDb(failure))
            await Assert.ThrowsAnyAsync<Exception>(() => DeliveryProcessor(db, provider).DispatchBatchAsync(CancellationToken.None));
        Assert.True(failure.Entered.Task.IsCompletedSuccessfully);
        Assert.Single(provider.Deliveries);
        await using (var db = sql.CreateDb()) Assert.Equal(NotificationDeliveryStatus.Processing,
            (await db.NotificationDeliveryAttempts.AsNoTracking().SingleAsync(row => row.Id == attemptId)).Status);
        _clock.Advance(TimeSpan.FromSeconds(31));
        await using (var db = sql.CreateDb()) await DeliveryProcessor(db, provider).DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(new[] { attemptId, attemptId }, provider.Deliveries.ToArray());
        await using var verification = sql.CreateDb();
        var attempt = await verification.NotificationDeliveryAttempts.AsNoTracking().SingleAsync(row => row.Id == attemptId);
        Assert.Equal(NotificationDeliveryStatus.Delivered, attempt.Status); Assert.Equal(2, attempt.AttemptCount);
        Assert.Equal(1, await verification.Notifications.CountAsync(row => row.Id == attempt.NotificationId));
    }

    [Fact]
    public async Task ProviderCancellationWithIOExceptionDoesNotPersistFalseFailure()
    {
        var attemptId = await SeedDeliveryAsync();
        using var caller = new CancellationTokenSource();
        var provider = new CallbackPushProvider(() =>
        {
            caller.Cancel();
            throw new IOException("خطای کنترل‌شدهٔ provider پس از لغو کاربر.");
        });
        await using (var db = sql.CreateDb())
        {
            var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                DeliveryProcessor(db, provider).DispatchBatchAsync(caller.Token));
            Assert.Equal(caller.Token, cancelled.CancellationToken);
        }
        await using var verification = sql.CreateDb();
        var attempt = await verification.NotificationDeliveryAttempts.AsNoTracking().SingleAsync(row => row.Id == attemptId);
        Assert.Equal(NotificationDeliveryStatus.Processing, attempt.Status); Assert.Equal(1, attempt.AttemptCount);
        Assert.NotNull(attempt.ClaimToken); Assert.NotNull(attempt.ClaimedUntilUtc);
        Assert.Null(attempt.NextAttemptAtUtc); Assert.Null(attempt.FailureReason); Assert.Null(attempt.DeliveredAtUtc);
    }

    [Fact]
    public async Task AcceptedProviderResultStillPersistsWhenCallerCancelsBeforeReturn()
    {
        var attemptId = await SeedDeliveryAsync();
        using var caller = new CancellationTokenSource();
        var provider = new CallbackPushProvider(caller.Cancel);
        await using (var db = sql.CreateDb())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                DeliveryProcessor(db, provider).DispatchBatchAsync(caller.Token));
        await using var verification = sql.CreateDb();
        var attempt = await verification.NotificationDeliveryAttempts.AsNoTracking().SingleAsync(row => row.Id == attemptId);
        Assert.Equal(NotificationDeliveryStatus.Delivered, attempt.Status); Assert.Equal(1, attempt.AttemptCount);
        Assert.NotNull(attempt.DeliveredAtUtc); Assert.Null(attempt.FailureReason);
        Assert.Null(attempt.ClaimToken); Assert.Null(attempt.NextAttemptAtUtc);
        Assert.Equal(attemptId, Assert.Single(provider.Deliveries));
    }

    [Fact]
    public async Task DeliveryCompletionSqlUsesFiniteIndependentBudgetAndLeavesRecoverableClaim()
    {
        var attemptId = await SeedDeliveryAsync();
        var failure = new CompletionInterceptor(waitForCancellation: true);
        var provider = new CallbackPushProvider(() => failure.Armed = true);
        await using (var db = sql.CreateDb(failure))
        {
            var work = DeliveryProcessor(db, provider).DispatchBatchAsync(CancellationToken.None);
            await failure.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(failure.CompletionToken.CanBeCanceled);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(WorkerCompletionBudget.Timeout + TimeSpan.FromSeconds(5)));
            Assert.True(failure.CompletionToken.IsCancellationRequested);
        }
        await using var verification = sql.CreateDb();
        var attempt = await verification.NotificationDeliveryAttempts.AsNoTracking().SingleAsync(row => row.Id == attemptId);
        Assert.Equal(NotificationDeliveryStatus.Processing, attempt.Status); Assert.NotNull(attempt.ClaimToken);
    }

    private async Task<int> RunOutboxAsync(CallbackCoordinator coordinator, CancellationToken ct = default)
    {
        await using var db = sql.CreateDb();
        return await new OutboxProcessor(db, coordinator, new NoopSubscriptionActivation(), _clock, sql.Telemetry,
            NullLogger<OutboxProcessor>.Instance).DispatchBatchAsync(ct);
    }

    private async Task<int> RunRealtimeAsync(CallbackNotifier notifier, CancellationToken ct = default)
    {
        await using var db = sql.CreateDb();
        return await new MessagingRealtimeOutboxProcessor(db, notifier, _clock, sql.Telemetry,
            NullLogger<MessagingRealtimeOutboxProcessor>.Instance).DispatchBatchAsync(ct);
    }

    private NotificationDeliveryProcessor DeliveryProcessor(MakanDbContext db, CallbackPushProvider provider) =>
        new(db, provider, new UnavailableNotificationSmsProvider(), new AlwaysEnabledPreference(), new UnusedPrivacyResolver(),
            new NotificationDeliveryOptions(), _clock, sql.Telemetry, NullLogger<NotificationDeliveryProcessor>.Instance);

    private async Task<Guid[]> SeedOutboxAsync(int count, bool exhausted = false)
    {
        await using var db = sql.CreateDb();
        var user = User.Create(_clock.Now); db.Users.Add(user);
        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var id = Guid.NewGuid();
            var occurredAtUtc = (exhausted ? _clock.Now.AddMinutes(-1) : _clock.Now).AddTicks(index);
            var envelope = Assert.Single(NotificationOutboxContract.Create(new NotificationIntent(id, null,
                NotificationCategory.Educational, NotificationType.AssignmentPublished, NotificationResourceType.Assignment,
                Guid.NewGuid(), occurredAtUtc, [new(user.Id, "اعلان تست", "بازیابی worker", true)])));
            var row = OutboxMessage.Create(id, envelope.MessageType, envelope.Payload, envelope.OccurredAtUtc);
            if (exhausted) for (var attempt = 0; attempt < 10; attempt++)
                row.Claim(Guid.NewGuid(), _clock.Now.AddSeconds(-20 + attempt), _clock.Now.AddSeconds(-19 + attempt));
            db.OutboxMessages.Add(row); ids.Add(id);
        }
        await db.SaveChangesAsync(); return ids.ToArray();
    }

    private async Task<IReadOnlyCollection<OutboxMessage>> ReadOutboxAsync(Guid[] ids)
    {
        await using var db = sql.CreateDb();
        return await db.OutboxMessages.AsNoTracking().Where(row => ids.Contains(row.Id)).ToArrayAsync();
    }

    private async Task<Guid[]> SeedRealtimeAsync(int count, bool exhausted = false)
    {
        await using var db = sql.CreateDb();
        var user = User.Create(_clock.Now); var conversation = Conversation.CreateSaved(user.Id, _clock.Now);
        db.Users.Add(user); db.Conversations.Add(conversation); var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var change = MessagingChangeEvent.Create(conversation.Id, index + 1, MessagingChangeType.ConversationChanged,
                conversation.Id, null, (exhausted ? _clock.Now.AddMinutes(-1) : _clock.Now).AddTicks(index));
            var row = MessagingRealtimeOutboxMessage.Create(change, change.OccurredAtUtc);
            if (exhausted) for (var attempt = 0; attempt < 10; attempt++)
                row.Claim(_clock.Now.AddSeconds(-20 + attempt), _clock.Now.AddSeconds(-19 + attempt));
            db.MessagingChangeEvents.Add(change); db.MessagingRealtimeOutboxMessages.Add(row); ids.Add(row.Id);
        }
        await db.SaveChangesAsync(); return ids.ToArray();
    }

    private async Task<Guid> SeedDeliveryAsync()
    {
        await using var db = sql.CreateDb(); var user = User.Create(_clock.Now);
        var session = UserSession.Create(user.Id, RandomNumberGenerator.GetBytes(32), _clock.Now, _clock.Now.AddHours(1));
        var device = PushDeviceRegistration.Create(user.Id, session.Id, PushDevicePlatform.Android,
            $"worker-recovery-test-{Guid.NewGuid():N}", RandomNumberGenerator.GetBytes(32), _clock.Now);
        var notification = DomainNotification.Create(Guid.NewGuid(), user.Id, null, NotificationCategory.Educational,
            NotificationType.AssignmentPublished, "اعلان تست", "بازیابی ثبت نتیجه", NotificationResourceType.Assignment, Guid.NewGuid(), _clock.Now);
        var attempt = NotificationDeliveryAttempt.Schedule(notification.Id, NotificationDeliveryChannel.Push, device.Id.ToString("N"), _clock.Now, _clock.Now);
        db.AddRange(user, session, device, notification, attempt); await db.SaveChangesAsync(); return attempt.Id;
    }

    private sealed class RecoveryClock : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public DateTime Now => GetUtcNow().UtcDateTime;
        public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }
        public void Advance(TimeSpan amount) { lock (_gate) _now = _now.Add(amount); }
    }

    private sealed class CallbackCoordinator(Func<DomainNotification, CancellationToken, Task>? callback = null) : INotificationDeliveryCoordinator
    {
        public Task ScheduleAsync(DomainNotification notification, bool suppressExternalDelivery, CancellationToken ct) =>
            callback?.Invoke(notification, ct) ?? Task.CompletedTask;
    }

    private sealed class CallbackNotifier(Func<MessagingRealtimeNotification, CancellationToken, Task>? callback = null) : IMessagingRealtimeNotifier
    {
        public Task NotifyConversationChangedAsync(MessagingRealtimeNotification notification, Guid? audienceUserId, CancellationToken ct) =>
            callback?.Invoke(notification, ct) ?? Task.CompletedTask;
    }

    private sealed class NoopSubscriptionActivation : ISubscriptionActivationHandler
    {
        public Task HandleAsync(SubscriptionActivationRequested payload, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CallbackPushProvider(Action accepted) : IPushNotificationProvider
    {
        public ConcurrentQueue<Guid> Deliveries { get; } = new();
        public Task<DeliveryProviderResult> SendAsync(PushNotificationDelivery delivery, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Deliveries.Enqueue(delivery.DeliveryAttemptId); accepted();
            return Task.FromResult(DeliveryProviderResult.Delivered("controlled-test-acceptance"));
        }
    }

    private sealed class AlwaysEnabledPreference : INotificationPreferenceResolver
    {
        public Task<EffectiveNotificationPreference> ResolveAsync(Guid userId, Guid? organizationId, NotificationPreferenceCategory category, CancellationToken ct) =>
            Task.FromResult(new EffectiveNotificationPreference(userId, organizationId, category, true, true, true, false,
                false, false, null, null, null, false, null, null));
    }

    private sealed class UnusedPrivacyResolver : IMessagingPrivacyResolver
    {
        public Task<MessagingPrivacyContext> ResolveAsync(Guid requesterId, Guid targetId, CancellationToken ct) =>
            throw new InvalidOperationException("دادهٔ تست، پیام خصوصی نیست و نباید privacy را دور بزند.");
    }

    private sealed class CompletionInterceptor(bool waitForCancellation) : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public CancellationToken CompletionToken { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("UPDATE [notification].[NotificationDeliveryAttempts]", StringComparison.Ordinal))
            {
                CompletionToken = cancellationToken; Entered.TrySetResult();
                if (waitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("شکست کنترل‌شدهٔ ثبت نتیجه پس از پذیرش provider تست.");
            }
            return result;
        }
    }
}

public sealed class WorkerRecoverySqlFixture : IAsyncLifetime
{
    private const string DatabasePrefix = "MakanApp_Operations_Workers_";
    private readonly string _databaseName = $"{DatabasePrefix}{Guid.NewGuid():N}";
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    public string ConnectionString => new SqlConnectionStringBuilder
    {
        DataSource = "(localdb)\\MSSQLLocalDB", InitialCatalog = _databaseName,
        IntegratedSecurity = true, TrustServerCertificate = true, MultipleActiveResultSets = true, Pooling = false
    }.ConnectionString;
    public WorkerTelemetry Telemetry { get; }

    public WorkerRecoverySqlFixture() => Telemetry = new WorkerTelemetry(_services.GetRequiredService<IMeterFactory>(), TimeProvider.System);

    public MakanDbContext CreateDb(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(ConnectionString);
        if (interceptors.Length > 0) options.AddInterceptors(interceptors);
        return new MakanDbContext(options.Options);
    }

    public async Task InitializeAsync()
    {
        await using (var db = CreateDb()) await db.Database.MigrateAsync();
        // نام فقط GUID سروری همین fixture است؛ هیچ connection string برنامه یا DB عادی مصرف نمی‌شود.
        if (!_databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(_databaseName[DatabasePrefix.Length..], "N", out _)) throw new InvalidOperationException("مقصد تست ایزوله معتبر نیست.");
        var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(master.ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{_databaseName}] SET READ_COMMITTED_SNAPSHOT ON";
        await command.ExecuteNonQueryAsync();
        await using var check = CreateDb();
        var actual = await check.Database.SqlQuery<int>($"SELECT CONVERT(int, is_read_committed_snapshot_on) AS [Value] FROM sys.databases WHERE name = DB_NAME()").SingleAsync();
        Assert.Equal(1, actual);
    }

    public async Task DisposeAsync()
    {
        Telemetry.Dispose(); await _services.DisposeAsync();
        // DB یکتای تست برای بررسی شواهد باقی می‌ماند؛ DROP یا پاک‌سازی دادهٔ کاربر انجام نمی‌شود.
    }
}
