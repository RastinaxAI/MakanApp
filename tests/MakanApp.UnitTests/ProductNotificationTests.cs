using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Application.Outbox;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.UnitTests;

public sealed class ProductNotificationTests
{
    [Fact]
    public void SameClientOperationOnDifferentTicketsCannotCollapseNotifications()
    {
        using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().Options);
        var outbox = new RecordingOutbox(); var writer = new ProductNotificationWriter(db, outbox, TimeProvider.System);
        var operation = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid(); var recipient = Guid.NewGuid();
        writer.SupportUpdated(operation, first, recipient); writer.SupportUpdated(operation, second, recipient); writer.SupportUpdated(operation, first, recipient);
        var payloads = outbox.Items.Select(e => JsonSerializer.Deserialize<NotificationOutboxPayload>(e.Payload)!).ToArray();
        Assert.NotEqual(payloads[0].SourceEventId, payloads[1].SourceEventId);
        Assert.Equal(payloads[0].SourceEventId, payloads[2].SourceEventId);
    }
    private sealed class RecordingOutbox : IOutbox
    {
        public List<OutboxEnvelope> Items { get; } = [];
        public void Enqueue(OutboxEnvelope envelope) => Items.Add(envelope);
        public void EnqueueRange(IEnumerable<OutboxEnvelope> envelopes) => Items.AddRange(envelopes);
    }
    [Theory]
    [InlineData(NotificationType.OrganizationInvitation, NotificationResourceType.OrganizationInvitation, NotificationPreferenceCategory.Invitations)]
    [InlineData(NotificationType.MembershipChanged, NotificationResourceType.Membership, NotificationPreferenceCategory.ServiceUpdates)]
    [InlineData(NotificationType.OrderPaid, NotificationResourceType.Order, NotificationPreferenceCategory.ServiceUpdates)]
    [InlineData(NotificationType.SupportUpdated, NotificationResourceType.SupportTicket, NotificationPreferenceCategory.ServiceUpdates)]
    [InlineData(NotificationType.SchoolApplicationUpdated, NotificationResourceType.SchoolApplication, NotificationPreferenceCategory.ServiceUpdates)]
    public void ProductEventsHaveStableOutboxContractsOptionalSmsAndNoPrivateExternalContent(NotificationType type, NotificationResourceType resource, NotificationPreferenceCategory category)
    {
        var now = DateTime.UtcNow; var eventId = Guid.NewGuid(); var user = Guid.NewGuid(); var id = Guid.NewGuid();
        const string marker = "SYNTHETIC_PRIVATE_BODY";
        var notification = DomainNotification.Create(eventId, user, null, NotificationCategory.AccountSystem, type, "عنوان خصوصی", marker, resource, id, now);
        Assert.Equal(category, NotificationPreferencePolicy.Map(type)); Assert.False(NotificationPreferencePolicy.IsMandatory(category));
        Assert.False(NotificationPreferencePolicy.GetDefaults(category).SmsEnabled);
        var push = NotificationExternalContentPolicy.CreatePush(notification); Assert.DoesNotContain(marker, push.Body);
        Assert.Equal($"makan://notifications/{notification.Id:D}", push.DeepLink);
        var envelope = Assert.Single(NotificationOutboxContract.Create(new(eventId, null, NotificationCategory.AccountSystem, type, resource, id, now, [new(user, "اعلان", "متن امن")])));
        Assert.True(NotificationOutboxContract.Matches(envelope.MessageType, type));
    }
}
