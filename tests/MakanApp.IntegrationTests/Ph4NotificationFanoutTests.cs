using System.Security.Cryptography;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.IntegrationTests;

[Collection(WorkerRecoverySqlCollection.Name)]
public sealed class Ph4NotificationFanoutTests(WorkerRecoverySqlFixture sql) : IClassFixture<WorkerRecoverySqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceFanoutUsesBoundedPagesAndTrackingWhilePreservingOuterTransaction(bool failAfterFirstBatch)
    {
        var now = DateTime.UtcNow;
        var user = User.Create(now);
        var session = UserSession.Create(user.Id, RandomNumberGenerator.GetBytes(32), now, now.AddHours(1));
        var devices = Enumerable.Range(0, EfNotificationPreferenceStore.PushDeviceBatchSize * 2 + 5)
            .Select(_ => PushDeviceRegistration.Create(user.Id, session.Id, PushDevicePlatform.Android,
                $"isolated-ph4-test-{Guid.NewGuid():N}", RandomNumberGenerator.GetBytes(32), now)).ToArray();
        await using (var seed = sql.CreateDb())
        {
            seed.AddRange(user, session); seed.PushDeviceRegistrations.AddRange(devices); await seed.SaveChangesAsync();
        }
        var notification = DomainNotification.Create(Guid.NewGuid(), user.Id, null, NotificationCategory.Educational,
            NotificationType.AssignmentPublished, "آزمون fan-out", "دادهٔ مصنوعی", NotificationResourceType.Assignment, Guid.NewGuid(), now);
        await using (var db = sql.CreateDb())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Notifications.Add(notification);
            var probe = new BatchProbe(new EfNotificationPreferenceStore(db), db, failAfterFirstBatch);
            var coordinator = new NotificationDeliveryCoordinator(new PushOnlyPreference(), probe, TimeProvider.System);
            if (failAfterFirstBatch)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ScheduleAsync(notification, false, default));
                Assert.Equal(1, probe.SavedBatches);
                await transaction.RollbackAsync();
            }
            else
            {
                await coordinator.ScheduleAsync(notification, false, default);
                await db.SaveChangesAsync(); await transaction.CommitAsync();
                Assert.Equal(3, probe.SavedBatches);
                Assert.Equal(devices.Select(device => device.Id).Order(), probe.DeviceIds.Order());
                Assert.Equal(devices.Length, probe.DeviceIds.Distinct().Count());
            }
        }
        await using var check = sql.CreateDb();
        var attempts = await check.NotificationDeliveryAttempts.AsNoTracking().Where(row => row.NotificationId == notification.Id).ToArrayAsync();
        if (failAfterFirstBatch)
        {
            Assert.Empty(attempts);
            Assert.False(await check.Notifications.AnyAsync(row => row.Id == notification.Id));
        }
        else
        {
            Assert.Equal(devices.Length, attempts.Count(row => row.Channel == NotificationDeliveryChannel.Push));
            Assert.Single(attempts, row => row.Channel == NotificationDeliveryChannel.Sms && row.Status == NotificationDeliveryStatus.Suppressed);
            Assert.Equal(attempts.Length, attempts.Select(row => (row.NotificationId, row.Channel, row.DestinationReference)).Distinct().Count());
        }
    }

    [Fact]
    public async Task ChunkPersistenceRejectsMissingOwnerTransactionBeforeAnyWrite()
    {
        await using var db = sql.CreateDb();
        var attempt = NotificationDeliveryAttempt.Schedule(Guid.NewGuid(), NotificationDeliveryChannel.Push, "test", DateTime.UtcNow, DateTime.UtcNow.AddSeconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfNotificationPreferenceStore(db).AddBatchAsync([attempt], default));
        Assert.Empty(db.ChangeTracker.Entries<NotificationDeliveryAttempt>());
    }

    private sealed class PushOnlyPreference : INotificationPreferenceResolver
    {
        public Task<EffectiveNotificationPreference> ResolveAsync(Guid userId, Guid? organizationId, NotificationPreferenceCategory category, CancellationToken ct) =>
            Task.FromResult(new EffectiveNotificationPreference(userId, organizationId, category, true, true, true, false,
                false, false, null, null, null, false, null, null));
    }

    private sealed class BatchProbe(EfNotificationPreferenceStore inner, MakanDbContext db, bool failAfterFirstBatch) : INotificationDeliveryStore
    {
        public int SavedBatches { get; private set; }
        public List<Guid> DeviceIds { get; } = [];
        public async IAsyncEnumerable<IReadOnlyList<Guid>> GetActivePushDeviceIdBatchesAsync(Guid userId, DateTime nowUtc,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var batch in inner.GetActivePushDeviceIdBatchesAsync(userId, nowUtc, ct))
            {
                Assert.InRange(batch.Count, 1, EfNotificationPreferenceStore.PushDeviceBatchSize);
                if (failAfterFirstBatch && SavedBatches == 1) throw new InvalidOperationException("شکست مصنوعی پس از ذخیرهٔ اولین صفحه.");
                DeviceIds.AddRange(batch); yield return batch;
            }
        }
        public Task<Guid?> GetVerifiedMobileCredentialIdAsync(Guid userId, CancellationToken ct) => inner.GetVerifiedMobileCredentialIdAsync(userId, ct);
        public void AddRange(IEnumerable<NotificationDeliveryAttempt> attempts) => inner.AddRange(attempts);
        public async Task AddBatchAsync(IReadOnlyCollection<NotificationDeliveryAttempt> attempts, CancellationToken ct)
        {
            Assert.InRange(attempts.Count, 1, EfNotificationPreferenceStore.PushDeviceBatchSize);
            await inner.AddBatchAsync(attempts, ct); SavedBatches++;
            Assert.Empty(db.ChangeTracker.Entries<NotificationDeliveryAttempt>());
        }
    }
}
