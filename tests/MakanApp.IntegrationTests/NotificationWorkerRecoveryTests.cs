using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.IntegrationTests;

[Collection(WorkerRecoverySqlCollection.Name)]
public sealed class NotificationWorkerRecoveryTests(WorkerRecoverySqlFixture sql) : IClassFixture<WorkerRecoverySqlFixture>, IAsyncLifetime
{
    private readonly RecoveryClock _clock = new();

    public async Task InitializeAsync()
    {
        await using var db = sql.CreateDb();
        // فقط fixture دارای نام GUID و DB مستقل؛ باقی‌ماندهٔ تست قبلی حذف نمی‌شود.
        await db.NotificationDeliveryAttempts.Where(row => row.Status != NotificationDeliveryStatus.Delivered).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.Status, NotificationDeliveryStatus.Failed)
            .SetProperty(row => row.FailureReason, NotificationDeliveryFailureReason.ProviderUnavailable)
            .SetProperty(row => row.ClaimToken, (Guid?)null).SetProperty(row => row.ClaimedUntilUtc, (DateTime?)null)
            .SetProperty(row => row.NextAttemptAtUtc, (DateTime?)null));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IndependentWorkersCannotClaimSameLiveDeliveryAndDoNotPreclaimFollowingRows()
    {
        var ids = await SeedAsync(3);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DeliveryProviderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new CallbackProvider((_, _) => { entered.TrySetResult(); return release.Task; });
        await using var dbA = sql.CreateDb();
        var dispatching = Processor(dbA, first, batchSize: 2).DispatchBatchAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var rows = await ReadAsync(ids);
            Assert.Equal(1, rows.Count(row => row.Status == NotificationDeliveryStatus.Processing));
            Assert.Equal(2, rows.Count(row => row.Status == NotificationDeliveryStatus.Pending && row.AttemptCount == 0));
            await using var dbB = sql.CreateDb();
            var other = new CallbackProvider();
            Assert.Equal(2, await Processor(dbB, other, batchSize: 2).DispatchBatchAsync(CancellationToken.None));
            Assert.DoesNotContain(ids[0], other.Attempts);
            release.TrySetResult(DeliveryProviderResult.Delivered("first-live-owner"));
            Assert.Equal(1, await dispatching.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(3, first.Attempts.Count + other.Attempts.Count);
            Assert.All(await ReadAsync(ids), row => { Assert.Equal(NotificationDeliveryStatus.Delivered, row.Status); Assert.Equal(1, row.AttemptCount); });
        }
        finally
        {
            release.TrySetResult(DeliveryProviderResult.Delivered("test-cleanup"));
            await Record.ExceptionAsync(() => dispatching.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOwnerCannotOverwriteSuccessorDeliveryEvenWhenItsProviderAccepted(bool lateFailure)
    {
        var id = Assert.Single(await SeedAsync(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DeliveryProviderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new CallbackProvider((_, _) => { entered.TrySetResult(); return release.Task; });
        await using var dbA = sql.CreateDb();
        var dispatching = Processor(dbA, old).DispatchBatchAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _clock.Advance(TimeSpan.FromSeconds(31));
            await using (var dbB = sql.CreateDb())
                Assert.Equal(1, await Processor(dbB, new CallbackProvider((_, _) => Task.FromResult(DeliveryProviderResult.Delivered("successor-receipt"))))
                    .DispatchBatchAsync(CancellationToken.None));
            var before = Assert.Single(await ReadAsync([id]));
            release.TrySetResult(lateFailure ? DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderRejected) : DeliveryProviderResult.Delivered("stale-receipt"));
            Assert.Equal(1, await dispatching.WaitAsync(TimeSpan.FromSeconds(5)));
            var after = Assert.Single(await ReadAsync([id]));
            Assert.Equal(NotificationDeliveryStatus.Delivered, after.Status); Assert.Equal(2, after.AttemptCount);
            Assert.Equal("successor-receipt", after.ProviderMessageId); Assert.Equal(before.RowVersion, after.RowVersion);
            Assert.Equal(before.DeliveredAtUtc, after.DeliveredAtUtc); Assert.Null(after.FailureReason);
        }
        finally
        {
            release.TrySetResult(DeliveryProviderResult.Delivered("test-cleanup"));
            await Record.ExceptionAsync(() => dispatching.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public async Task ExpiredOwnerCannotRenewAfterAuthorizationEvenWithoutSuccessor()
    {
        var id = Assert.Single(await SeedAsync(1));
        var provider = new CallbackProvider();
        var preference = new CallbackPreference(() => _clock.Advance(TimeSpan.FromSeconds(31)));
        await using (var db = sql.CreateDb())
            Assert.Equal(1, await Processor(db, provider, preference: preference).DispatchBatchAsync(CancellationToken.None));
        Assert.Empty(provider.Attempts);
        var abandoned = Assert.Single(await ReadAsync([id]));
        Assert.Equal(NotificationDeliveryStatus.Processing, abandoned.Status); Assert.Equal(1, abandoned.AttemptCount);
        Assert.True(abandoned.ClaimedUntilUtc <= _clock.Now); Assert.Null(abandoned.FailureReason);
        await using var fresh = sql.CreateDb();
        Assert.Equal(1, await Processor(fresh, provider).DispatchBatchAsync(CancellationToken.None));
        Assert.Equal(2, Assert.Single(await ReadAsync([id])).AttemptCount); Assert.Single(provider.Attempts);
    }

    [Fact]
    public async Task CompletionChecksFreshClockAfterRealSqlLockWaitWithoutSuccessor()
    {
        var id = Assert.Single(await SeedAsync(1));
        await using var blocker = new SqlConnection(sql.ConnectionString);
        await blocker.OpenAsync();
        SqlTransaction? transaction = null;
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new CallbackProvider(async (_, ct) =>
        {
            transaction = (SqlTransaction)await blocker.BeginTransactionAsync(ct);
            await using var command = blocker.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "SELECT [Id] FROM [notification].[NotificationDeliveryAttempts] WITH (XLOCK,HOLDLOCK,ROWLOCK) WHERE [Id]=@id";
            command.Parameters.AddWithValue("@id", id); await command.ExecuteScalarAsync(ct);
            locked.TrySetResult(); return DeliveryProviderResult.Delivered("accepted-before-lease-expired");
        });
        await using var db = sql.CreateDb();
        var dispatching = Processor(db, provider).DispatchBatchAsync(CancellationToken.None);
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await WaitForBlockedSqlAsync(blocker, dispatching));
            _clock.Advance(TimeSpan.FromSeconds(31));
            await transaction!.RollbackAsync(); await transaction.DisposeAsync(); transaction = null;
            Assert.Equal(1, await dispatching.WaitAsync(TimeSpan.FromSeconds(5)));
            var row = Assert.Single(await ReadAsync([id]));
            Assert.Equal(NotificationDeliveryStatus.Processing, row.Status); Assert.Equal(1, row.AttemptCount);
            Assert.Null(row.DeliveredAtUtc); Assert.Null(row.ProviderMessageId); Assert.Null(row.FailureReason);
            await using var successor = sql.CreateDb();
            Assert.Equal(1, await Processor(successor, new CallbackProvider()).DispatchBatchAsync(CancellationToken.None));
            Assert.Equal(2, Assert.Single(await ReadAsync([id])).AttemptCount);
        }
        finally
        {
            if (transaction is not null) { await transaction.RollbackAsync(); await transaction.DisposeAsync(); }
            await Record.ExceptionAsync(() => dispatching.WaitAsync(TimeSpan.FromSeconds(6)));
        }
    }

    [Fact]
    public async Task ExhaustedAndPermanentPoisonConsumeBoundedSlotsWithoutBlockingHealthyRows()
    {
        var ids = await SeedAsync(5, exhaustedFirst: true);
        var provider = new CallbackProvider((delivery, _) => Task.FromResult(delivery.DeliveryAttemptId == ids[1]
            ? DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderRejected) : DeliveryProviderResult.Delivered()));
        await using (var first = sql.CreateDb()) Assert.Equal(3, await Processor(first, provider, batchSize: 3).DispatchBatchAsync(CancellationToken.None));
        var rows = await ReadAsync(ids);
        Assert.Equal(NotificationDeliveryStatus.Failed, rows.Single(row => row.Id == ids[0]).Status);
        Assert.Equal(3, rows.Single(row => row.Id == ids[0]).AttemptCount);
        Assert.Equal(NotificationDeliveryStatus.Failed, rows.Single(row => row.Id == ids[1]).Status);
        Assert.Equal(NotificationDeliveryStatus.Delivered, rows.Single(row => row.Id == ids[2]).Status);
        Assert.All(rows.Where(row => row.Id == ids[3] || row.Id == ids[4]), row => { Assert.Equal(NotificationDeliveryStatus.Pending, row.Status); Assert.Equal(0, row.AttemptCount); });
        Assert.Equal(2, provider.Attempts.Count);
        await using (var second = sql.CreateDb()) Assert.Equal(2, await Processor(second, provider, batchSize: 3).DispatchBatchAsync(CancellationToken.None));
        Assert.Equal(4, provider.Attempts.Count);
        Assert.Equal(3, (await ReadAsync(ids)).Count(row => row.Status == NotificationDeliveryStatus.Delivered));
    }

    [Fact]
    public async Task TransientSqlDeliveryRetriesFollowBackoffAndTerminateAtConfiguredCap()
    {
        var id = Assert.Single(await SeedAsync(1));
        var provider = new CallbackProvider((_, _) => Task.FromResult(DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderUnavailable)));
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using (var db = sql.CreateDb()) Assert.Equal(1, await Processor(db, provider).DispatchBatchAsync(CancellationToken.None));
            var row = Assert.Single(await ReadAsync([id])); Assert.Equal(attempt, row.AttemptCount);
            if (attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(attempt == 1 ? 5 : 10);
                Assert.Equal(NotificationDeliveryStatus.Deferred, row.Status); Assert.Equal(_clock.Now.Add(delay), row.NextAttemptAtUtc);
                _clock.Advance(delay - TimeSpan.FromMilliseconds(1));
                await using (var early = sql.CreateDb()) Assert.Equal(0, await Processor(early, provider).DispatchBatchAsync(CancellationToken.None));
                _clock.Advance(TimeSpan.FromMilliseconds(1));
            }
            else { Assert.Equal(NotificationDeliveryStatus.Failed, row.Status); Assert.Null(row.NextAttemptAtUtc); }
        }
        await using var terminal = sql.CreateDb(); Assert.Equal(0, await Processor(terminal, provider).DispatchBatchAsync(CancellationToken.None));
        Assert.Equal(3, provider.Attempts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IgnoredProviderCancellationIsBoundedAndLateFaultCannotRewriteRecoveredDelivery(bool callerCancellation)
    {
        var id = Assert.Single(await SeedAsync(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<DeliveryProviderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new CallbackProvider((_, _) => { entered.TrySetResult(); return late.Task; });
        using var caller = new CancellationTokenSource();
        await using (var db = sql.CreateDb())
        {
            var dispatching = Processor(db, provider, providerTimeout: callerCancellation ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(100))
                .DispatchBatchAsync(caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (callerCancellation)
            {
                caller.Cancel();
                var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatching.WaitAsync(TimeSpan.FromSeconds(3)));
                Assert.Equal(caller.Token, cancelled.CancellationToken);
            }
            else Assert.Equal(1, await dispatching.WaitAsync(TimeSpan.FromSeconds(3)));
        }
        var before = Assert.Single(await ReadAsync([id]));
        Assert.Equal(callerCancellation ? NotificationDeliveryStatus.Processing : NotificationDeliveryStatus.Deferred, before.Status);
        Assert.Equal(callerCancellation ? null : NotificationDeliveryFailureReason.ProviderTimeout, before.FailureReason);
        late.TrySetException(new IOException("synthetic-late-provider-marker"));
        _clock.Advance(TimeSpan.FromSeconds(31));
        await using (var recovered = sql.CreateDb()) Assert.Equal(1, await Processor(recovered, new CallbackProvider()).DispatchBatchAsync(CancellationToken.None));
        var delivered = Assert.Single(await ReadAsync([id])); Assert.Equal(NotificationDeliveryStatus.Delivered, delivered.Status);
        Assert.Equal(2, delivered.AttemptCount); Assert.Null(delivered.FailureReason); Assert.Null(delivered.ClaimToken);
    }

    [Fact]
    public async Task RevokedOrganizationMembershipSuppressesQueuedDeliveryUsingCurrentSqlPreferenceScope()
    {
        var id = Assert.Single(await SeedAsync(1, organizationScope: true));
        await using (var revoke = sql.CreateDb())
        {
            var notificationId = await revoke.NotificationDeliveryAttempts.Where(row => row.Id == id).Select(row => row.NotificationId).SingleAsync();
            var notification = await revoke.Notifications.SingleAsync(row => row.Id == notificationId);
            var membership = await revoke.Memberships.SingleAsync(row => row.UserId == notification.RecipientUserId && row.OrganizationId == notification.OrganizationId);
            membership.End(_clock.Now); await revoke.SaveChangesAsync();
        }
        var provider = new CallbackProvider();
        await using (var db = sql.CreateDb())
            Assert.Equal(1, await Processor(db, provider, preference: new NotificationPreferenceResolver(new EfNotificationPreferenceStore(db)))
                .DispatchBatchAsync(CancellationToken.None));
        Assert.Empty(provider.Attempts);
        var suppressed = Assert.Single(await ReadAsync([id])); Assert.Equal(NotificationDeliveryStatus.Suppressed, suppressed.Status);
        Assert.Equal(NotificationDeliveryFailureReason.OrganizationAccessRevoked, suppressed.FailureReason);
    }

    private NotificationDeliveryProcessor Processor(MakanDbContext db, CallbackProvider provider, int batchSize = 1,
        INotificationPreferenceResolver? preference = null, TimeSpan? providerTimeout = null) =>
        new(db, provider, new UnavailableNotificationSmsProvider(), preference ?? new CallbackPreference(), new UnusedPrivacyResolver(),
            new NotificationDeliveryOptions { BatchSize = batchSize, ProviderTimeout = providerTimeout ?? TimeSpan.FromSeconds(10) },
            _clock, sql.Telemetry, NullLogger<NotificationDeliveryProcessor>.Instance);

    private async Task<Guid[]> SeedAsync(int count, bool exhaustedFirst = false, bool organizationScope = false)
    {
        await using var db = sql.CreateDb(); var user = User.Create(_clock.Now);
        var session = UserSession.Create(user.Id, RandomNumberGenerator.GetBytes(32), _clock.Now, _clock.Now.AddHours(1));
        var device = PushDeviceRegistration.Create(user.Id, session.Id, PushDevicePlatform.Android, $"notification-worker-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), _clock.Now);
        db.AddRange(user, session, device);
        Guid? organizationId = null;
        if (organizationScope)
        {
            var organization = Organization.Create("سازمان تست بازیابی", _clock.Now); organizationId = organization.Id;
            db.AddRange(organization, Membership.CreateActive(user.Id, organization.Id, _clock.Now));
        }
        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var createdAtUtc = _clock.Now.AddMinutes(-5).AddTicks(index);
            var notification = DomainNotification.Create(Guid.NewGuid(), user.Id, organizationId, NotificationCategory.Educational,
                NotificationType.AssignmentPublished, "اعلان تست", "بازیابی پایدار", NotificationResourceType.Assignment, Guid.NewGuid(), createdAtUtc);
            var attempt = NotificationDeliveryAttempt.Schedule(notification.Id, NotificationDeliveryChannel.Push, device.Id.ToString("N"), createdAtUtc, createdAtUtc);
            if (exhaustedFirst && index == 0)
                for (var abandoned = 0; abandoned < 3; abandoned++)
                    attempt.Claim(Guid.NewGuid(), createdAtUtc.AddSeconds(abandoned * 2), createdAtUtc.AddSeconds(abandoned * 2 + 1));
            db.AddRange(notification, attempt); ids.Add(attempt.Id);
        }
        await db.SaveChangesAsync(); return ids.ToArray();
    }

    private async Task<NotificationDeliveryAttempt[]> ReadAsync(Guid[] ids)
    {
        await using var db = sql.CreateDb();
        return await db.NotificationDeliveryAttempts.AsNoTracking().Where(row => ids.Contains(row.Id)).ToArrayAsync();
    }

    private async Task<bool> WaitForBlockedSqlAsync(SqlConnection blocker, Task processing)
    {
        var settings = new SqlConnectionStringBuilder(sql.ConnectionString);
        Assert.StartsWith("MakanApp_Operations_Workers_", settings.InitialCatalog, StringComparison.Ordinal);
        await using var observer = new SqlConnection(settings.ConnectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await observer.OpenAsync(timeout.Token);
        var started = Stopwatch.GetTimestamp();
        while (!processing.IsCompleted && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2))
        {
            await using var command = observer.CreateCommand(); command.CommandTimeout = 1;
            command.CommandText = "SELECT COUNT_BIG(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@blocker AND wait_type LIKE N'LCK_M_%'";
            command.Parameters.AddWithValue("@blocker", blocker.ServerProcessId);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0) return true;
            await Task.Delay(10, timeout.Token);
        }
        return false;
    }

    private sealed class RecoveryClock : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public DateTime Now => GetUtcNow().UtcDateTime;
        public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }
        public void Advance(TimeSpan delta) { lock (_gate) _now = _now.Add(delta); }
    }

    private sealed class CallbackProvider(Func<PushNotificationDelivery, CancellationToken, Task<DeliveryProviderResult>>? callback = null) : IPushNotificationProvider
    {
        public ConcurrentQueue<Guid> Attempts { get; } = new();
        public Task<DeliveryProviderResult> SendAsync(PushNotificationDelivery delivery, CancellationToken ct)
        { Attempts.Enqueue(delivery.DeliveryAttemptId); return callback?.Invoke(delivery, ct) ?? Task.FromResult(DeliveryProviderResult.Delivered("recovered-receipt")); }
    }

    private sealed class CallbackPreference(Action? onResolve = null) : INotificationPreferenceResolver
    {
        public Task<EffectiveNotificationPreference> ResolveAsync(Guid userId, Guid? organizationId, NotificationPreferenceCategory category, CancellationToken ct)
        {
            onResolve?.Invoke();
            return Task.FromResult(new EffectiveNotificationPreference(userId, organizationId, category, true, true, true, false,
                false, false, null, null, null, false, null, null));
        }
    }

    private sealed class UnusedPrivacyResolver : IMessagingPrivacyResolver
    {
        public Task<MessagingPrivacyContext> ResolveAsync(Guid requesterId, Guid targetId, CancellationToken ct) =>
            throw new InvalidOperationException("دادهٔ تست Direct نیست و نباید به مجوز ساختگی متکی باشد.");
    }
}
