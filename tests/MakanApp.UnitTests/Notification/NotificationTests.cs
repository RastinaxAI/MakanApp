using MakanApp.Domain.Notification;
using MakanApp.Application.Notification;
using System.Text.Json;
using Xunit;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.UnitTests.Notification;

public sealed class NotificationTests
{
    [Fact]
    public void CreateProducesUnreadNotificationWithScopeAndSafeNavigationReference()
    {
        var occurredAtUtc = new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc);
        var eventId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var notification = DomainNotification.Create(
            eventId,
            recipientId,
            organizationId,
            NotificationCategory.Educational,
            NotificationType.ExamGradeReleased,
            " نتیجه آزمون منتشر شد ",
            " نتیجه جدید یک آزمون برای شما منتشر شده است. ",
            NotificationResourceType.ExamAttempt,
            resourceId,
            occurredAtUtc);

        Assert.Equal(eventId, notification.SourceEventId);
        Assert.Equal(recipientId, notification.RecipientUserId);
        Assert.Equal(organizationId, notification.OrganizationId);
        Assert.Equal(resourceId, notification.ResourceId);
        Assert.Equal("نتیجه آزمون منتشر شد", notification.Title);
        Assert.Equal(occurredAtUtc, notification.CreatedAtUtc);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void MarkReadIsIdempotentAndPreservesFirstReadTime()
    {
        var notification = CreateNotification();
        var firstReadAtUtc = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

        notification.MarkRead(firstReadAtUtc);
        notification.MarkRead(firstReadAtUtc.AddMinutes(1));

        Assert.Equal(firstReadAtUtc, notification.ReadAtUtc);
    }

    [Fact]
    public void CreateRejectsNonUtcTimestamp()
    {
        Assert.Throws<ArgumentException>(() => DomainNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            NotificationCategory.AccountSystem,
            NotificationType.AssignmentPublished,
            "عنوان",
            "متن",
            NotificationResourceType.Assignment,
            Guid.NewGuid(),
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)));
    }

    [Fact]
    public void OutboxContractCreatesOneSmallVersionedMessagePerDistinctRecipient()
    {
        var sourceEventId = Guid.NewGuid();
        var firstRecipient = Guid.NewGuid();
        var secondRecipient = Guid.NewGuid();
        var intent = new NotificationIntent(
            sourceEventId,
            Guid.NewGuid(),
            NotificationCategory.Educational,
            NotificationType.AssignmentPublished,
            NotificationResourceType.Assignment,
            Guid.NewGuid(),
            new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc),
            [
                new(firstRecipient, "عنوان", "متن"),
                new(secondRecipient, "عنوان", "متن"),
                new(firstRecipient, "عنوان تکراری", "متن تکراری")
            ]);

        var envelopes = NotificationOutboxContract.Create(intent);
        var payloads = envelopes
            .Select(envelope => JsonSerializer.Deserialize<NotificationOutboxPayload>(envelope.Payload)!)
            .ToArray();

        Assert.Equal(2, envelopes.Count);
        Assert.All(envelopes, envelope => Assert.Equal(
            "notification.assignment-published.v1",
            envelope.MessageType));
        Assert.All(payloads, payload => Assert.Equal(sourceEventId, payload.SourceEventId));
        Assert.Equal([firstRecipient, secondRecipient], payloads.Select(payload => payload.Recipient.UserId));
    }

    private static DomainNotification CreateNotification() =>
        DomainNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationCategory.Educational,
            NotificationType.AssignmentPublished,
            "تکلیف جدید",
            "یک تکلیف جدید منتشر شده است.",
            NotificationResourceType.Assignment,
            Guid.NewGuid(),
            new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc));
}
