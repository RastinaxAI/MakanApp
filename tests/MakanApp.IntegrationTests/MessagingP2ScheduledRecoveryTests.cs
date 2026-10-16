using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Outbox;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2ScheduledOutboxFailureRollsBackMessageAndReceiptBeforeSafeRetry()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.AddSingleton<P2ScheduledOutboxControl>(); services.AddScoped<EfOutbox>(); services.RemoveAll<IOutbox>();
            services.AddScoped<IOutbox>(p => new P2FailingScheduledOutbox(p.GetRequiredService<EfOutbox>(), p.GetRequiredService<P2ScheduledOutboxControl>()));
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken);
        try
        {
            var task = await ScheduleP2Async(client, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "rollback واقعی" }));
            factory.Clock.SetUtcNow(now.AddMinutes(2));
            await using (var scope = host.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(CancellationToken.None);
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            var pending = await ReadP2ScheduleAsync(client, setup.ConversationId, task.Id); Assert.Equal(ScheduledMessageStatus.Scheduled, pending.Status); Assert.Null(pending.DispatchedMessageId);
            Assert.Equal("SCHEDULED_DISPATCH_TEMPORARILY_UNAVAILABLE", pending.LastFailureCode);
            factory.Clock.SetUtcNow(now.AddMinutes(3));
            await using (var scope = host.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(CancellationToken.None);
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Dispatched, (await ReadP2ScheduleAsync(client, setup.ConversationId, task.Id)).Status);
            await factory.DrainNotificationOutboxAsync(); var sent = Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
            Assert.Equal(1, await factory.CountNotificationsAsync(sent.Id, setup.Second.UserId));
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    private sealed class P2ScheduledOutboxControl
    {
        public P2ScheduledOutboxControl() { }
        private int _remaining = 1;
        public bool ShouldFail() => Interlocked.Exchange(ref _remaining, 0) == 1;
    }
    private sealed class P2FailingScheduledOutbox(EfOutbox inner, P2ScheduledOutboxControl control) : IOutbox
    {
        public void Enqueue(OutboxEnvelope envelope)
        {
            if (control.ShouldFail()) throw new InvalidOperationException("خطای کنترل‌شدهٔ ثبت Outbox در تست زمان‌بندی.");
            inner.Enqueue(envelope);
        }
        public void EnqueueRange(IEnumerable<OutboxEnvelope> envelopes) { foreach (var envelope in envelopes) Enqueue(envelope); }
    }
}
