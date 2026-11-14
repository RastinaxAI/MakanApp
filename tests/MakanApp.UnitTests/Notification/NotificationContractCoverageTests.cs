using System.Text.Json;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Notification;
using Xunit;

namespace MakanApp.UnitTests.Notification;

public sealed class NotificationContractCoverageTests
{
    [Fact]
    public void EveryNotificationTypeHasOneVersionedContractAndOneValidPreferenceCategory()
    {
        var contracts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            Assert.True(Enum.IsDefined(NotificationPreferencePolicy.Map(type)));
            var user = Guid.NewGuid();
            var intent = new NotificationIntent(Guid.NewGuid(), null, NotificationCategory.AccountSystem, type,
                NotificationResourceType.PersonalClass, Guid.NewGuid(), DateTime.UtcNow,
                [new(user, "خبر آزمون", "متن عمومی"), new(user, "خبر آزمون", "متن عمومی")]);
            var envelope = Assert.Single(NotificationOutboxContract.Create(intent));
            Assert.StartsWith("notification.", envelope.MessageType, StringComparison.Ordinal);
            Assert.EndsWith(".v1", envelope.MessageType, StringComparison.Ordinal);
            Assert.True(contracts.Add(envelope.MessageType));
            Assert.True(NotificationOutboxContract.Matches(envelope.MessageType, type));
        }
    }

    [Fact]
    public void EveryPushUsesTheNotificationResolverLinkAndIgnoresPrivateOrInjectedTransportText()
    {
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            var id = Guid.NewGuid(); var conversation = Guid.NewGuid();
            var delivery = new PushNotificationDelivery(Guid.NewGuid(), id, "SYNTHETIC_PRIVATE_DEVICE_TOKEN",
                "SYNTHETIC_PRIVATE_FULL_NAME", "SYNTHETIC_PRIVATE_SUPPORT_GRADE_PAYMENT_TEXT",
                "https://invalid.example/private-file", Type: type, ConversationId: conversation);
            var payload = PushWirePayload.Create(delivery);
            Assert.Equal($"makan://notifications/{id:D}", payload.DeepLink);
            Assert.Equal(id.ToString("N"), payload.Tag);
            Assert.Equal(id.ToString("D"), payload.NotificationId);
            var wire = JsonSerializer.Serialize(payload);
            Assert.DoesNotContain("SYNTHETIC_PRIVATE", wire, StringComparison.Ordinal);
            Assert.DoesNotContain("invalid.example", wire, StringComparison.Ordinal);
            Assert.DoesNotContain("makan://conversations/", wire, StringComparison.Ordinal);
            Assert.Equal(type is NotificationType.MessageReceived or NotificationType.MessageReply or NotificationType.MessageMention
                ? conversation.ToString("D") : null, payload.ConversationId);
        }
    }
}
