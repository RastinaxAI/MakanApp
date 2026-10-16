using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("mute")]
    [InlineData("preference")]
    [InlineData("block")]
    [InlineData("for-me")]
    [InlineData("minor")]
    [InlineData("unknown")]
    [InlineData("clear")]
    [InlineData("everyone-deleted")]
    [InlineData("absolute-expiry")]
    public async Task NotificationWorkerRechecksCurrentStateAfterScheduling(string mutation)
    {
        factory.NotificationDeliveryProvider.Reset();
        using var setup = await CreateAdvancedConversationSetupAsync();
        _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "notification private source");
        await factory.DrainNotificationOutboxAsync();
        var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
        Assert.Equal(NotificationDeliveryStatus.Pending, (await P1PushAttemptAsync(notificationId)).Status);
        if (mutation == "mute")
        {
            using var response = await setup.SecondClient.PutAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/mute",
                new SetConversationMuteCommand(ConversationMuteMode.Permanent, null), JsonOptions);
            response.EnsureSuccessStatusCode();
        }
        else if (mutation == "preference")
        {
            using var response = await setup.SecondClient.PutAsJsonAsync("/api/v1/notification-preferences/Messages",
                new UpdateNotificationPreferenceCommand(false, false, false, null, null, null, null), JsonOptions);
            response.EnsureSuccessStatusCode();
        }
        else if (mutation == "block")
        {
            using var response = await setup.SecondClient.PostAsync($"/api/v1/messaging/blocks/{setup.First.UserId}", null);
            response.EnsureSuccessStatusCode();
        }
        else if (mutation == "for-me")
        {
            using var response = await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/for-me");
            response.EnsureSuccessStatusCode();
        }
        else if (mutation is "minor" or "unknown")
            await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId,
                mutation == "minor" ? CommunicationAgeCategory.Minor : CommunicationAgeCategory.Unknown);
        else if (mutation == "clear")
        {
            using var response = await setup.SecondClient.PostAsync($"/api/v1/conversations/{setup.ConversationId}/clear-history", null);
            response.EnsureSuccessStatusCode();
        }
        else if (mutation == "everyone-deleted")
        {
            using var response = await setup.FirstClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(sent.Version)}");
            response.EnsureSuccessStatusCode();
        }
        else
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var session = await db.UserSessions.SingleAsync(s => s.UserId == setup.Second.UserId);
            db.Entry(session).Property(s => s.AbsoluteExpiresAtUtc).CurrentValue = factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await factory.ProcessNotificationDeliveriesAsync();
        Assert.Equal(NotificationDeliveryStatus.Suppressed, (await P1PushAttemptAsync(notificationId)).Status);
        Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notificationId);
        Assert.Equal(1, await factory.CountNotificationsAsync(sent.MessageId, setup.Second.UserId));
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerRestartRecoversExpiredClaimOrTerminatesExhaustedClaim(bool exhausted)
    {
        factory.NotificationDeliveryProvider.Reset();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
            var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "notification restart");
            await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                var attempt = await db.NotificationDeliveryAttempts.SingleAsync(a => a.NotificationId == notificationId && a.Channel == NotificationDeliveryChannel.Push);
                now = DateTime.SpecifyKind(attempt.CreatedAtUtc, DateTimeKind.Utc);
                for (var index = 0; index < (exhausted ? 3 : 1); index++)
                    attempt.Claim(Guid.NewGuid(), now.AddSeconds(index * 2), now.AddSeconds(index * 2 + 1));
                await db.SaveChangesAsync();
            }
            factory.Clock.SetUtcNow(now.AddSeconds(10));
            await factory.ProcessNotificationDeliveriesAsync();
            var result = await P1PushAttemptAsync(notificationId);
            Assert.Equal(exhausted ? NotificationDeliveryStatus.Failed : NotificationDeliveryStatus.Delivered, result.Status);
            Assert.Equal(exhausted ? 3 : 2, result.AttemptCount);
            Assert.Equal(exhausted ? 0 : 1, factory.NotificationDeliveryProvider.PushDeliveries.Count(d => d.NotificationId == notificationId));
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(result.AttemptCount, (await P1PushAttemptAsync(notificationId)).AttemptCount);
        }
        finally { factory.Clock.UseSystemTime(); factory.NotificationDeliveryProvider.Reset(); }
    }

    [Fact]
    public async Task PermanentInvalidRegistrationIsRevokedAndFutureMessagesDoNotRetryIt()
    {
        factory.NotificationDeliveryProvider.Reset();
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            var device = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
            var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "invalid destination");
            await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
            factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.InvalidRegistration);
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryStatus.Failed, (await P1PushAttemptAsync(notificationId)).Status);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                Assert.NotNull((await db.PushDeviceRegistrations.SingleAsync(d => d.Id == device.Id)).RevokedAtUtc);
            }
            var next = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "after invalid destination");
            await factory.DrainNotificationOutboxAsync();
            var nextId = await factory.GetNotificationIdAsync(next.MessageId, setup.Second.UserId);
            Assert.Equal(NotificationDeliveryFailureReason.NoDevice, (await P1PushAttemptAsync(nextId)).FailureReason);
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Single(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notificationId);
        }
        finally { factory.NotificationDeliveryProvider.Reset(); }
    }

    [Fact]
    public async Task WorkerHonorsProviderRetryAfterWithoutLosingLogicalNotification()
    {
        factory.NotificationDeliveryProvider.Reset();
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
            var key = Guid.NewGuid();
            var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, key, "bounded provider retry");
            await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, key, "bounded provider retry");
            await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
            factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Transient(
                NotificationDeliveryFailureReason.ProviderRateLimited, TimeSpan.FromSeconds(90));
            var now = factory.Clock.GetUtcNow().UtcDateTime;
            await factory.ProcessNotificationDeliveriesAsync();
            var attempt = await P1PushAttemptAsync(notificationId);
            Assert.Equal(NotificationDeliveryStatus.Deferred, attempt.Status);
            Assert.True(attempt.NextAttemptAtUtc >= now.AddSeconds(90));
            factory.Clock.SetUtcNow(now.AddSeconds(20));
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Single(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notificationId);
            factory.Clock.SetUtcNow(DateTime.SpecifyKind(attempt.NextAttemptAtUtc!.Value, DateTimeKind.Utc).AddSeconds(1));
            factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Delivered("provider accepted");
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryStatus.Delivered, (await P1PushAttemptAsync(notificationId)).Status);
            Assert.Equal(1, await factory.CountNotificationsAsync(sent.MessageId, setup.Second.UserId));
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        }
        finally { factory.Clock.UseSystemTime(); factory.NotificationDeliveryProvider.Reset(); }
    }

    [Fact]
    public async Task ChangedQuietHoursDefersAlreadyQueuedDeliveryWithoutUsingAttemptBudget()
    {
        factory.NotificationDeliveryProvider.Reset();
        factory.Clock.SetUtcNow(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
            var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "new quiet hours");
            await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
            using var response = await setup.SecondClient.PutAsJsonAsync("/api/v1/notification-preferences/Messages",
                new UpdateNotificationPreferenceCommand(true, false, true, new TimeOnly(11, 0), new TimeOnly(13, 0), "UTC", null), JsonOptions);
            response.EnsureSuccessStatusCode();
            await factory.ProcessNotificationDeliveriesAsync();
            var deferred = await P1PushAttemptAsync(notificationId);
            Assert.Equal(NotificationDeliveryStatus.Deferred, deferred.Status);
            Assert.Equal(0, deferred.AttemptCount);
            Assert.Equal(new DateTime(2026, 10, 16, 13, 0, 0, DateTimeKind.Utc), deferred.NextAttemptAtUtc);
            factory.Clock.SetUtcNow(DateTime.SpecifyKind(deferred.NextAttemptAtUtc!.Value, DateTimeKind.Utc));
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryStatus.Delivered, (await P1PushAttemptAsync(notificationId)).Status);
        }
        finally { factory.Clock.UseSystemTime(); factory.NotificationDeliveryProvider.Reset(); }
    }

    private static async Task<PushDeviceRegistrationResult> RegisterP1NotificationDeviceAsync(HttpClient client, string? token = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, token ?? $"p1-device-{Guid.NewGuid():N}"), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!;
    }

    private async Task<MakanAppWebApplicationFactory.DeliveryAttemptState> P1PushAttemptAsync(Guid notificationId) =>
        Assert.Single(await factory.GetDeliveryAttemptsAsync(notificationId), a => a.Channel == NotificationDeliveryChannel.Push);

    [Fact]
    public async Task InvalidResultForOldDeviceVersionDoesNotRevokeRefreshedRegistration()
    {
        factory.NotificationDeliveryProvider.Reset();
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            var token = $"p1-refreshed-token-{Guid.NewGuid():N}";
            var device = await RegisterP1NotificationDeviceAsync(setup.SecondClient, token);
            var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "refresh during transport");
            await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
            factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.InvalidRegistration);
            factory.NotificationDeliveryProvider.OnPushSending = async (delivery, ct) =>
            {
                if (delivery.NotificationId != notificationId) return;
                factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(1));
                Assert.Equal(device.Id, (await RegisterP1NotificationDeviceAsync(setup.SecondClient, token)).Id);
            };
            await factory.ProcessNotificationDeliveriesAsync();
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Null((await db.PushDeviceRegistrations.SingleAsync(d => d.Id == device.Id)).RevokedAtUtc);
            Assert.Equal(NotificationDeliveryStatus.Failed, (await P1PushAttemptAsync(notificationId)).Status);
        }
        finally { factory.Clock.UseSystemTime(); factory.NotificationDeliveryProvider.Reset(); }
    }

    [Fact]
    public async Task WorkerCancellationLeavesRecoverableDurableClaim()
    {
        factory.NotificationDeliveryProvider.Reset();
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
            var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "cancel worker after claim");
            await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
            var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            factory.NotificationDeliveryProvider.OnPushSending = (delivery, ct) =>
            {
                if (delivery.NotificationId == notificationId) sending.TrySetResult();
                return Task.CompletedTask;
            };
            factory.NotificationDeliveryProvider.PushWaitsForCancellation = true;
            using var cancellation = new CancellationTokenSource();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var work = scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>().DispatchBatchAsync(cancellation.Token);
                await sending.Task.WaitAsync(TimeSpan.FromSeconds(20));
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
            }
            Assert.Equal(NotificationDeliveryStatus.Processing, (await P1PushAttemptAsync(notificationId)).Status);
            factory.NotificationDeliveryProvider.Reset();
            factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(2));
            await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryStatus.Delivered, (await P1PushAttemptAsync(notificationId)).Status);
            Assert.Equal(1, await factory.CountNotificationsAsync(sent.MessageId, setup.Second.UserId));
        }
        finally { factory.Clock.UseSystemTime(); factory.NotificationDeliveryProvider.Reset(); }
    }

    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task PendingNotificationDoesNotReachRemovedGroupOrChannelMember(string kind)
    {
        factory.NotificationDeliveryProvider.Reset();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var conversation = await CreateManagedAndReadAsync(ownerClient, kind, PersonalCommand("queued private notification", member.UserId));
        _ = await RegisterP1NotificationDeviceAsync(memberClient);
        var sent = await SendMessageAndReadAsync(ownerClient, conversation.ConversationId, Guid.NewGuid(), "pending before remove");
        await factory.DrainNotificationOutboxAsync();
        var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, member.UserId);
        using var removed = await ownerClient.DeleteAsync($"/api/v1/conversations/{conversation.ConversationId}/members/{member.UserId}");
        removed.EnsureSuccessStatusCode();
        await factory.ProcessNotificationDeliveriesAsync();
        Assert.Equal(NotificationDeliveryFailureReason.ResourceAccessRevoked, (await P1PushAttemptAsync(notificationId)).FailureReason);
        Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notificationId);
    }

    [Fact]
    public async Task MinorNotificationUsesMinimalPreviewNotPrivateMessageOrSafetyMetadata()
    {
        factory.NotificationDeliveryProvider.Reset();
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var consent = await setup.SecondClient.PostAsJsonAsync("/api/v1/contacts",
            new { contactUserId = setup.First.UserId, savedName = "مخاطب مجاز" }, JsonOptions);
        consent.EnsureSuccessStatusCode();
        await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor);
        _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "PRIVATE-P1-CONTENT");
        await factory.DrainNotificationOutboxAsync();
        var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
        await factory.ProcessNotificationDeliveriesAsync();
        var delivery = Assert.Single(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notificationId);
        Assert.Equal("پیام جدیدی در ماکان دارید.", delivery.Body);
        var preview = delivery.Title + delivery.Body + delivery.DeepLink + delivery.ToString();
        Assert.DoesNotContain("PRIVATE-P1-CONTENT", preview);
        Assert.DoesNotContain(setup.Second.PhoneNumber, preview);
        Assert.DoesNotContain("Minor", preview);
        Assert.DoesNotContain("Guardian", preview);
    }
}
