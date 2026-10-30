using System.Collections.Concurrent;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Application.Realtime;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountRealtimePublisherCrashAfterDeliveryRetriesSameEventOutsideTransactionWithoutDuplicateEffect()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        await FlushReplayOutboxAsync();
        var boundary = await AccountChangesAsync(client);
        await using var live = CreateHubConnection(owner.AccessToken);
        var notifications = new ConcurrentQueue<RealtimeInvalidation>();
        var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retryReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid eventId = Guid.Empty;
        live.On<RealtimeInvalidation>("ClientInvalidated", item =>
        {
            if (item.EventId != eventId) return;
            notifications.Enqueue(item); firstReceived.TrySetResult();
            if (notifications.Count >= 2) retryReceived.TrySetResult();
        });
        await live.StartAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            eventId = AddOwnAccessEvent(scope.ServiceProvider, owner.UserId);
            await scope.ServiceProvider.GetRequiredService<MakanDbContext>().SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var crashing = new CallbackInvalidationNotifier(async (id, payload, ct) =>
            {
                Assert.Null(db.Database.CurrentTransaction);
                await scope.ServiceProvider.GetRequiredService<IRealtimeInvalidationNotifier>().NotifyAsync(id, payload, ct);
                throw new InvalidOperationException("خرابی آزمایشی پس از تحویل و قبل از completion");
            });
            Assert.Equal(1, await ReplayProcessor(scope.ServiceProvider, crashing).DispatchBatchAsync(default));
            var pending = await db.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == eventId);
            Assert.Null(pending.ProcessedAtUtc); Assert.NotNull(pending.NextAttemptAtUtc);
        }
        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(3));
        try
        {
            await FlushReplayOutboxAsync();
            await retryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Single(notifications.Select(x => x.EventId).Distinct());
            Assert.True((await DrainAccountAsync(client, boundary.NextCursor)).ContainsKey(eventId));
            await using var scope = factory.Services.CreateAsyncScope();
            var rows = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OutboxMessages.Where(x => x.Id == eventId).ToArrayAsync();
            Assert.NotNull(Assert.Single(rows).ProcessedAtUtc);
            Assert.Equal(2, rows[0].AttemptCount);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task AccountRealtimeExpiredPublisherCannotCompleteRecoveredClaim()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        await FlushReplayOutboxAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var id = AddOwnAccessEvent(scope.ServiceProvider, owner.UserId);
        await db.SaveChangesAsync();
        var moved = false;
        var expired = new CallbackInvalidationNotifier((_, _, _) =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            if (!moved) { moved = true; factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(31)); }
            return Task.CompletedTask;
        });
        try
        {
            // همان loop، claim منقضی را از token تازه بازیابی می‌کند؛ completion قدیمی پذیرفته نمی‌شود.
            Assert.Equal(2, await ReplayProcessor(scope.ServiceProvider, expired).DispatchBatchAsync(default));
            var row = await db.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.NotNull(row.ProcessedAtUtc); Assert.Equal(2, row.AttemptCount);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    private OutboxProcessor ReplayProcessor(IServiceProvider services, IRealtimeInvalidationNotifier notifier) => new(
        services.GetRequiredService<MakanDbContext>(), services.GetRequiredService<INotificationDeliveryCoordinator>(),
        services.GetRequiredService<ISubscriptionActivationHandler>(), factory.Clock, services.GetRequiredService<WorkerTelemetry>(),
        services.GetRequiredService<ILogger<OutboxProcessor>>(), notifier);

    private sealed class CallbackInvalidationNotifier(Func<Guid, string, CancellationToken, Task> action) : IRealtimeInvalidationNotifier
    {
        public Task NotifyAsync(Guid eventId, string payload, CancellationToken ct) => action(eventId, payload, ct);
    }
}
