using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task P2SilentStillPersistsUnreadRealtimeAndInternalNotificationButSuppressesOnlyExternalDelivery(bool silent)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); factory.NotificationDeliveryProvider.Reset();
        try
        {
            (await setup.SecondClient.PostAsJsonAsync("/api/v1/notifications/devices", new RegisterPushDeviceCommand(PushDevicePlatform.Android, $"p2-silent-{Guid.NewGuid():N}"), JsonOptions)).EnsureSuccessStatusCode();
            await using var peer = CreateHubConnection(setup.Second.AccessToken); var changed = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.On<MessagingRealtimeNotification>("ConversationChanged", n => { if (n.ConversationId == setup.ConversationId && n.Type == MessagingChangeType.MessageCreated) changed.TrySetResult(n); });
            await StartP2HubAsync(peer); await peer.InvokeAsync("SubscribeConversation", setup.ConversationId);
            var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "intent مستقل از mute", IsSilent = silent };
            var response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", command, JsonOptions); response.EnsureSuccessStatusCode();
            var receipt = (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
            Assert.Equal(receipt.MessageId, (await changed.Task.WaitAsync(TimeSpan.FromSeconds(10))).ResourceId);
            var history = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions);
            Assert.Equal(silent, Assert.Single(history!.Messages).IsSilent);
            var summary = Assert.Single((await setup.SecondClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!, c => c.ConversationId == setup.ConversationId);
            Assert.Equal(1, summary.UnreadCount); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
            await factory.DrainNotificationOutboxAsync(); var notification = await factory.GetMessageNotificationAsync(receipt.MessageId, setup.Second.UserId); Assert.NotNull(notification);
            if (silent)
            {
                var attempts = await factory.GetDeliveryAttemptsAsync(notification!.NotificationId); Assert.NotEmpty(attempts);
                Assert.All(attempts, a => Assert.Equal(NotificationDeliveryStatus.Suppressed, a.Status));
                await factory.ProcessNotificationDeliveriesAsync();
                Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notification.NotificationId);
                Assert.DoesNotContain(factory.NotificationDeliveryProvider.SmsDeliveries, d => d.NotificationId == notification.NotificationId);
            }
            else
            {
                for (var i = 0; i < 20 && !factory.NotificationDeliveryProvider.PushDeliveries.Any(d => d.NotificationId == notification!.NotificationId); i++)
                    if (await factory.ProcessNotificationDeliveriesAsync() == 0) break;
                Assert.Contains(factory.NotificationDeliveryProvider.PushDeliveries, d => d.NotificationId == notification!.NotificationId);
            }
            var changedIntent = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", new SendMessageCommand {
                ClientMessageId = command.ClientMessageId, Text = command.Text, IsSilent = !silent }, JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, changedIntent.StatusCode); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        }
        finally { factory.NotificationDeliveryProvider.Reset(); }
    }
}
