using MakanApp.Application.Notification;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessengerCompletionModelTests
{
    private static readonly DateTime NowUtc =
        new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SavedConversationHasDedicatedPersonalOwnerShape()
    {
        var ownerUserId = Guid.NewGuid();

        var conversation = Conversation.CreateSaved(ownerUserId, NowUtc);

        Assert.Equal(ConversationType.Saved, conversation.Type);
        Assert.Equal(ConversationScope.Personal, conversation.Scope);
        Assert.Equal(ownerUserId, conversation.SavedOwnerUserId);
        Assert.Null(conversation.OrganizationId);
        Assert.Null(conversation.DirectUserLowId);
        Assert.Null(conversation.DirectUserHighId);
    }

    [Fact]
    public void ClearHistoryWatermarkIsMonotonicAndClearsManualUnread()
    {
        var state = ConversationUserState.Create(Guid.NewGuid(), Guid.NewGuid(), NowUtc);
        state.MarkUnread(NowUtc.AddMinutes(1));

        Assert.True(state.HideHistoryThrough(12, NowUtc.AddMinutes(2)));
        Assert.False(state.HideHistoryThrough(12, NowUtc.AddMinutes(3)));
        Assert.False(state.HideHistoryThrough(8, NowUtc.AddMinutes(4)));
        Assert.Equal(12, state.HistoryHiddenThroughSequence);
        Assert.False(state.IsManuallyUnread);
    }

    [Fact]
    public void ContactCardUsesTypedSnapshotWithoutPhone()
    {
        var message = Message.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            MessageKind.ContactCard, null, 0, null, null,
            Message.StorageMaximumTextLength, NowUtc);
        var contactUserId = Guid.NewGuid();

        message.SetContactCardSnapshot(contactUserId, "safe.username", "نام امن");

        Assert.Equal(contactUserId, message.SharedContactUserId);
        Assert.Equal("safe.username", message.SharedContactUsername);
        Assert.Equal("نام امن", message.SharedContactDisplayName);
        Assert.Null(message.Text);
    }

    [Fact]
    public void MessageNotificationUsesMessagesPreferenceAndSafeExternalPreview()
    {
        var notification = Domain.Notification.Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            NotificationCategory.Communication,
            NotificationType.MessageMention,
            "عنوان داخلی",
            "متن خصوصی نباید خارج شود",
            NotificationResourceType.Conversation,
            Guid.NewGuid(),
            NowUtc);

        var preview = NotificationExternalContentPolicy.CreatePush(notification);

        Assert.Equal(NotificationPreferenceCategory.Messages,
            NotificationPreferencePolicy.Map(notification.Type));
        Assert.Equal("پیام جدیدی در ماکان دارید.", preview.Body);
        Assert.DoesNotContain("متن خصوصی", preview.Body, StringComparison.Ordinal);
    }
}
