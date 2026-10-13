using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task SavedConversationIsLazyConcurrentUniqueAndOwnerOnly()
    {
        using var ownerClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        _ = await CreateMessagingUserAsync(outsiderClient);

        var responses = await Task.WhenAll(
            ownerClient.GetAsync("/api/v1/conversations/saved"),
            ownerClient.GetAsync("/api/v1/conversations/saved"));
        try
        {
            var results = await Task.WhenAll(responses.Select(response =>
                response.Content.ReadFromJsonAsync<SavedConversationResult>(JsonOptions)));
            Assert.All(responses, response => Assert.Contains(
                response.StatusCode,
                new[] { HttpStatusCode.OK, HttpStatusCode.Created }));
            Assert.Single(results.Select(result => result!.ConversationId).Distinct());
            Assert.Equal(1, await factory.CountSavedConversationsAsync(owner.UserId));

            var saved = await factory.GetSavedConversationAsync(owner.UserId);
            Assert.Equal(ConversationScope.Personal, saved.Scope);
            Assert.Null(saved.OrganizationId);
            Assert.Equal(owner.UserId, saved.SavedOwnerUserId);
            using var outsiderHistory = await outsiderClient.GetAsync(
                $"/api/v1/conversations/{saved.ConversationId}/messages");
            Assert.Equal(HttpStatusCode.NotFound, outsiderHistory.StatusCode);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task SavedConversationSupportsTextFileSearchMediaAndIndependentForwardCopy()
    {
        using var ownerClient = factory.CreateClient();
        using var peerClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var peer = await CreateMessagingUserAsync(peerClient);
        _ = await CreateMessagingUserAsync(outsiderClient);
        var saved = await ownerClient.GetFromJsonAsync<SavedConversationResult>(
            "/api/v1/conversations/saved", JsonOptions);
        const string savedText = "saved-search-14e-private";
        _ = await SendMessageAndReadAsync(ownerClient, saved!.ConversationId, Guid.NewGuid(), savedText);
        var file = await UploadFileAsync(ownerClient, "image/png", "saved-private.png");
        var mediaMessage = await SendAdvancedMessageAsync(
            ownerClient,
            saved.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            });

        var search = await ownerClient.GetFromJsonAsync<MessageSearchPageResult>(
            $"/api/v1/messages/search?q={savedText}&conversationId={saved.ConversationId}", JsonOptions);
        var media = await ownerClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{saved.ConversationId}/media", JsonOptions);
        using var outsiderFile = await outsiderClient.GetAsync($"/api/v1/files/{file.Id}/content");

        var direct = await StartDirectAndReadAsync(ownerClient, peer.UserId, ConversationScope.Personal);
        var source = await SendMessageAndReadAsync(
            ownerClient, direct.ConversationId, Guid.NewGuid(), "forward-source-14e");
        using var forwardResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{direct.ConversationId}/messages/{source.MessageId}/forward",
            new ForwardMessageCommand(saved.ConversationId, Guid.NewGuid()),
            JsonOptions);
        forwardResponse.EnsureSuccessStatusCode();
        var forwarded = await forwardResponse.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions);
        using var deleteCopy = await ownerClient.DeleteAsync(
            $"/api/v1/conversations/{saved.ConversationId}/messages/{forwarded!.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(forwarded.Version)}");
        deleteCopy.EnsureSuccessStatusCode();
        var sourceHistory = await peerClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{direct.ConversationId}/messages", JsonOptions);

        Assert.Single(search!.Items);
        Assert.Contains(media!.Items, item => item.MessageId == mediaMessage.MessageId);
        Assert.Equal(HttpStatusCode.NotFound, outsiderFile.StatusCode);
        Assert.Contains(sourceHistory!.Messages, item => item.MessageId == source.MessageId && !item.IsDeleted);
    }

    [Fact]
    public async Task ClearHistoryUsesWatermarkAcrossHistorySearchMediaFileReplyForwardAndUnread()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var oldText = "clear-watermark-14e";
        var text = await SendMessageAndReadAsync(
            setup.SecondClient, setup.ConversationId, Guid.NewGuid(), oldText);
        var file = await UploadFileAsync(setup.SecondClient, "image/png", "clear-history.png");
        _ = await SendAdvancedMessageAsync(
            setup.SecondClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            });

        using var firstClear = await setup.FirstClient.PostAsync(
            $"/api/v1/conversations/{setup.ConversationId}/clear-history", null);
        using var repeatedClear = await setup.FirstClient.PostAsync(
            $"/api/v1/conversations/{setup.ConversationId}/clear-history", null);
        firstClear.EnsureSuccessStatusCode();
        repeatedClear.EnsureSuccessStatusCode();
        var firstState = await firstClear.Content.ReadFromJsonAsync<ConversationUserStateResult>(JsonOptions);
        var repeatedState = await repeatedClear.Content.ReadFromJsonAsync<ConversationUserStateResult>(JsonOptions);
        var firstHistory = await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions);
        var secondHistory = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions);
        var search = await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>(
            $"/api/v1/messages/search?q={oldText}&conversationId={setup.ConversationId}", JsonOptions);
        var media = await setup.FirstClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media", JsonOptions);
        using var fileResponse = await setup.FirstClient.GetAsync($"/api/v1/files/{file.Id}/content");
        using var reply = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "reply denied",
                ReplyToMessageId = text.MessageId
            }, JsonOptions);
        var saved = await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>(
            "/api/v1/conversations/saved", JsonOptions);
        using var forward = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{text.MessageId}/forward",
            new ForwardMessageCommand(saved!.ConversationId, Guid.NewGuid()), JsonOptions);
        var newMessage = await SendMessageAndReadAsync(
            setup.SecondClient, setup.ConversationId, Guid.NewGuid(), "visible-after-clear");
        firstHistory = await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions);

        Assert.Equal(firstState!.HistoryHiddenThroughSequence, repeatedState!.HistoryHiddenThroughSequence);
        Assert.Contains(secondHistory!.Messages, item => item.MessageId == text.MessageId);
        Assert.Empty(search!.Items);
        Assert.Empty(media!.Items);
        Assert.Equal(HttpStatusCode.NotFound, fileResponse.StatusCode);
        Assert.False(reply.IsSuccessStatusCode);
        Assert.False(forward.IsSuccessStatusCode);
        Assert.Single(firstHistory!.Messages, item => item.MessageId == newMessage.MessageId);
    }

    [Fact]
    public async Task DirectNotificationIsDurableDeduplicatedSafeAndProviderFailureDoesNotRollbackMessage()
    {
        factory.NotificationDeliveryProvider.Reset();
        factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Permanent(
            NotificationDeliveryFailureReason.ProviderUnavailable);
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var register = await setup.SecondClient.PostAsJsonAsync(
            "/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, $"token-14e-{Guid.NewGuid():N}"),
            JsonOptions);
        register.EnsureSuccessStatusCode();
        var clientMessageId = Guid.NewGuid();
        const string privateText = "raw-private-message-14e";
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient, setup.ConversationId, clientMessageId, privateText);
        var retry = await SendMessageAndReadAsync(
            setup.FirstClient, setup.ConversationId, clientMessageId, privateText);

        _ = await factory.DrainNotificationOutboxAsync();
        var notification = await factory.GetMessageNotificationAsync(sent.MessageId, setup.Second.UserId);
        _ = await factory.ProcessNotificationDeliveriesAsync();
        var history = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions);

        Assert.Equal(sent.MessageId, retry.MessageId);
        Assert.Equal(1, await factory.CountNotificationsAsync(sent.MessageId, setup.Second.UserId));
        Assert.Equal(0, await factory.CountNotificationsAsync(sent.MessageId, setup.First.UserId));
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.MessageReceived, notification!.Type);
        Assert.Equal(NotificationResourceType.Conversation, notification.ResourceType);
        Assert.Equal(setup.ConversationId, notification.ResourceId);
        Assert.DoesNotContain(privateText, notification.Body, StringComparison.Ordinal);
        Assert.Contains(history!.Messages, item => item.MessageId == sent.MessageId);
        factory.NotificationDeliveryProvider.Reset();
    }

    [Fact]
    public async Task GroupNotificationRecipientsAndMentionReplyPriorityUseOneNotificationPerMessage()
    {
        using var ownerClient = factory.CreateClient();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, first.UserId);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, second.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(), ConversationScope.Personal, "اعلان گروه 14E", null,
                [first.UserId, second.UserId]));

        var normal = await SendMessageAndReadAsync(
            ownerClient, group.ConversationId, Guid.NewGuid(), "group normal");
        _ = await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountNotificationsAsync(normal.MessageId, first.UserId));
        Assert.Equal(1, await factory.CountNotificationsAsync(normal.MessageId, second.UserId));
        Assert.Equal(0, await factory.CountNotificationsAsync(normal.MessageId, owner.UserId));

        using var remove = await ownerClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{second.UserId}");
        remove.EnsureSuccessStatusCode();
        var afterRemoval = await SendMessageAndReadAsync(
            ownerClient, group.ConversationId, Guid.NewGuid(), "after removal");
        _ = await factory.DrainNotificationOutboxAsync();
        Assert.Equal(0, await factory.CountNotificationsAsync(afterRemoval.MessageId, second.UserId));

        var priority = await SendAdvancedMessageAsync(
            firstClient,
            group.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "reply and mention",
                ReplyToMessageId = normal.MessageId,
                MentionedUserIds = [owner.UserId]
            });
        _ = await factory.DrainNotificationOutboxAsync();
        var ownerNotification = await factory.GetMessageNotificationAsync(priority.MessageId, owner.UserId);
        Assert.Equal(NotificationType.MessageMention, ownerNotification!.Type);
        Assert.Equal(1, await factory.CountNotificationsAsync(priority.MessageId, owner.UserId));

        var replyOnly = await SendAdvancedMessageAsync(
            firstClient,
            group.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "reply only",
                ReplyToMessageId = normal.MessageId
            });
        _ = await factory.DrainNotificationOutboxAsync();
        Assert.Equal(
            NotificationType.MessageReply,
            (await factory.GetMessageNotificationAsync(replyOnly.MessageId, owner.UserId))!.Type);
    }

    [Fact]
    public async Task MuteSuppressesExternalDeliveryUntilTemporaryMuteExpires()
    {
        var nowUtc = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            using var register = await setup.SecondClient.PostAsJsonAsync(
                "/api/v1/notifications/devices",
                new RegisterPushDeviceCommand(PushDevicePlatform.Android, $"mute-token-{Guid.NewGuid():N}"),
                JsonOptions);
            register.EnsureSuccessStatusCode();
            using var mute = await setup.SecondClient.PutAsJsonAsync(
                $"/api/v1/conversations/{setup.ConversationId}/mute",
                new SetConversationMuteCommand(ConversationMuteMode.Until, nowUtc.AddMinutes(5)),
                JsonOptions);
            mute.EnsureSuccessStatusCode();

            var mutedMessage = await SendMessageAndReadAsync(
                setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "muted message");
            _ = await factory.DrainNotificationOutboxAsync();
            var mutedNotification = await factory.GetMessageNotificationAsync(
                mutedMessage.MessageId, setup.Second.UserId);
            var mutedPush = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(mutedNotification!.NotificationId),
                attempt => attempt.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryFailureReason.SuppressedByPreference, mutedPush.FailureReason);

            factory.Clock.SetUtcNow(nowUtc.AddMinutes(6));
            var afterExpiry = await SendMessageAndReadAsync(
                setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "after mute expiry");
            _ = await factory.DrainNotificationOutboxAsync();
            var activeNotification = await factory.GetMessageNotificationAsync(
                afterExpiry.MessageId, setup.Second.UserId);
            var activePush = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(activeNotification!.NotificationId),
                attempt => attempt.Channel == NotificationDeliveryChannel.Push);
            Assert.NotEqual(NotificationDeliveryFailureReason.SuppressedByPreference, activePush.FailureReason);

            using var permanentMute = await setup.SecondClient.PutAsJsonAsync(
                $"/api/v1/conversations/{setup.ConversationId}/mute",
                new SetConversationMuteCommand(ConversationMuteMode.Permanent, null),
                JsonOptions);
            permanentMute.EnsureSuccessStatusCode();
            var permanentlyMuted = await SendMessageAndReadAsync(
                setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "permanently muted");
            _ = await factory.DrainNotificationOutboxAsync();
            var permanentNotification = await factory.GetMessageNotificationAsync(
                permanentlyMuted.MessageId, setup.Second.UserId);
            var permanentPush = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(permanentNotification!.NotificationId),
                attempt => attempt.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryFailureReason.SuppressedByPreference, permanentPush.FailureReason);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task UnreadMentionsSenderFilterConversationNameSearchAndContactCardAreSafe()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var targetClient = factory.CreateClient();
        var target = await CreateMessagingUserAsync(targetClient);
        var mentioned = await SendAdvancedMessageAsync(
            setup.SecondClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "common-filter-14e from second",
                MentionedUserIds = [setup.First.UserId]
            });
        _ = await SendMessageAndReadAsync(
            setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "common-filter-14e from first");

        var list = await setup.FirstClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations", JsonOptions);
        var summary = Assert.Single(list!, item => item.ConversationId == setup.ConversationId);
        Assert.Equal(1, summary.UnreadMentionCount);
        using var read = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/read",
            new AdvanceConversationCursorCommand(mentioned.Sequence), JsonOptions);
        read.EnsureSuccessStatusCode();
        list = await setup.FirstClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations", JsonOptions);
        Assert.Equal(0, Assert.Single(list!, item => item.ConversationId == setup.ConversationId).UnreadMentionCount);

        var senderSearch = await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>(
            $"/api/v1/messages/search?q=common-filter-14e&senderUserId={setup.Second.UserId}", JsonOptions);
        Assert.Single(senderSearch!.Items);
        Assert.Equal(setup.Second.UserId, senderSearch.Items.Single().Sender.UserId);
        var conversationSearch = await setup.FirstClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            $"/api/v1/conversations?q={Uri.EscapeDataString(setup.Second.Username)}", JsonOptions);
        Assert.Contains(conversationSearch!, item => item.ConversationId == setup.ConversationId);

        var card = await SendAdvancedMessageAsync(
            setup.FirstClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.ContactCard,
                ContactUserId = target.UserId
            });
        var rawHistory = await setup.SecondClient.GetStringAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages");
        var history = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions);
        var cardMessage = Assert.Single(history!.Messages, item => item.MessageId == card.MessageId);
        Assert.Equal(target.UserId, cardMessage.ContactCard!.UserId);
        Assert.Equal(target.Username, cardMessage.ContactCard.Username);
        Assert.DoesNotContain(target.PhoneNumber, rawHistory, StringComparison.Ordinal);
        Assert.Equal(0, await factory.CountDirectConversationsAsync(
            setup.Second.UserId, target.UserId, ConversationScope.Personal, null));
    }

    [Fact]
    public async Task ChildSafetyBlockedAttemptCreatesNoMessageNotificationOutbox()
    {
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient);
        var recipient = await CreateMessagingUserAsync(recipientClient);
        await factory.SetCommunicationAgeCategoryAsync(recipient.UserId, CommunicationAgeCategory.Minor);
        var direct = await StartDirectAndReadAsync(senderClient, recipient.UserId, ConversationScope.Personal);
        var before = await CountMessageNotificationOutboxAsync();
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked);

        using var blocked = await SendMessageAsync(
            senderClient, direct.ConversationId, Guid.NewGuid(), "blocked notification content");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(direct.ConversationId));
        Assert.Equal(before, await CountMessageNotificationOutboxAsync());
    }

    private async Task<int> CountMessageNotificationOutboxAsync() =>
        await factory.CountOutboxMessagesAsync(NotificationOutboxContract.MessageReceivedMessageType) +
        await factory.CountOutboxMessagesAsync(NotificationOutboxContract.MessageReplyMessageType) +
        await factory.CountOutboxMessagesAsync(NotificationOutboxContract.MessageMentionMessageType);
}
