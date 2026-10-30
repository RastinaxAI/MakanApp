using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("manual")]
    [InlineData("phone")]
    [InlineData("username")]
    public async Task StabilizationEligibleContactFirstMessageReachesUnopenedInboxWithoutConversationSubscription(string source)
    {
        var evidence = new StabilizationObservation("contact-" + source);
        using var sender = factory.CreateDefaultClient(evidence.Handler());
        using var receiver = factory.CreateDefaultClient(evidence.Handler());
        using var otherPeer = factory.CreateDefaultClient(evidence.Handler());
        var a = await CreateMessagingUserAsync(sender);
        var b = await CreateMessagingUserAsync(receiver);
        var c = await CreateMessagingUserAsync(otherPeer);
        var older = await StartDirectAndReadAsync(receiver, c.UserId, ConversationScope.Personal);
        await SendMessageAndReadAsync(otherPeer, older.ConversationId, Guid.NewGuid(), "گفتگوی پیشین مستقل");
        var olderBaseline = (await GetChangesAsync(receiver, older.ConversationId)).Changes.Select(change => change.Cursor).ToHashSet();
        if (source != "manual")
        {
            if (source == "phone")
                await UpdatePrivacyAsync(receiver, PrivacyAudience.Everyone, PrivacyAudience.Nobody,
                    PrivacyAudience.Everyone, PrivacyAudience.Contacts, null);
            var resolved = source == "phone" ? await ResolvePhoneAsync(sender, "0" + b.PhoneNumber[3..])
                : await ResolveUsernameForStabilizationAsync(sender, b.Username);
            Assert.True(resolved.IsDiscoverable);
            Assert.Equal(b.UserId, resolved.Profile!.UserId);
            Assert.True(resolved.Profile.CanStartDirect);
        }
        using var saved = await sender.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(b.UserId, "مخاطب آزمایشی"), JsonOptions);
        saved.EnsureSuccessStatusCode();
        var contact = (await saved.Content.ReadFromJsonAsync<UserContactResult>(JsonOptions))!;
        Assert.Equal(b.UserId, contact.ContactUserId);
        Assert.Equal(b.UserId, contact.Profile.UserId);
        Assert.True(contact.Profile.CanStartDirect);

        // گیرنده قبل از ساخت گفتگو فقط به نشست خودش متصل است؛ SubscribeConversation عمداً فراخوانی نمی‌شود.
        await using var live = CreateHubConnection(b.AccessToken);
        await using var senderLive = CreateHubConnection(a.AccessToken);
        var senderEvents = System.Threading.Channels.Channel.CreateBounded<MessagingRealtimeNotification>(8);
        senderLive.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged,
            notification => { if (notification.Type == MessagingChangeType.MessageCreated) senderEvents.Writer.TryWrite(notification); });
        var observed = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var olderUpdated = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var targetConversationId = Guid.Empty;
        live.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, notification =>
        {
                if (notification.Type == MessagingChangeType.MessageCreated && notification.ConversationId == targetConversationId)
                {
                    evidence.Mark("receivedAtUtc");
                    observed.TrySetResult(notification);
                }
                if (notification.Type == MessagingChangeType.MessageCreated && notification.ConversationId == older.ConversationId && !olderBaseline.Contains(notification.Cursor))
                    olderUpdated.TrySetResult(notification);
        });
        await live.StartAsync();
        await senderLive.StartAsync();
        var conversation = await StartDirectAndReadAsync(sender, contact.Profile.UserId, ConversationScope.Personal);
        targetConversationId = conversation.ConversationId;
        evidence.Mark("commandStartedAtUtc");
        var receipt = await SendMessageAndReadAsync(sender, conversation.ConversationId, Guid.NewGuid(), "پذیرش محلی گیرنده");
        evidence.Mark("receiptObservedAtUtc");
        var received = await observed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(receipt.MessageId, received.ResourceId);
        Assert.Equal(conversation.ConversationId, received.ConversationId);
        var inbox = (await receiver.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!;
        var summary = Assert.Single(inbox, x => x.ConversationId == conversation.ConversationId);
        Assert.Equal(1, summary.UnreadCount);
        Assert.Equal("پذیرش محلی گیرنده", summary.LastMessagePreview);
        Assert.Equal(conversation.ConversationId, inbox[0].ConversationId);
        evidence.Mark("renderEquivalentObservedAtUtc");
        var reply = await SendMessageAndReadAsync(receiver, conversation.ConversationId, Guid.NewGuid(), "پاسخ آزمایشی محلی");
        Assert.True(reply.MessageId != Guid.Empty);
        MessagingRealtimeNotification senderReceived;
        do { senderReceived = await senderEvents.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)); }
        while (senderReceived.ResourceId != reply.MessageId);
        Assert.Equal(conversation.ConversationId, senderReceived.ConversationId);
        evidence.Mark("senderReplyReceivedAtUtc");
        var latest = await SendMessageAndReadAsync(otherPeer, older.ConversationId, Guid.NewGuid(), "آخرین پیام گفتگوی پیشین");
        Assert.Equal(latest.MessageId, (await olderUpdated.Task.WaitAsync(TimeSpan.FromSeconds(15))).ResourceId);
        var reordered = (await receiver.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!;
        Assert.Equal(older.ConversationId, reordered[0].ConversationId);
        Assert.Equal("آخرین پیام گفتگوی پیشین", reordered[0].LastMessagePreview);
        Assert.Equal(2, reordered[0].UnreadCount);
        Assert.Equal(2, reordered.Select(item => item.ConversationId).Distinct().Count());
        evidence.Mark("existingConversationReorderedAtUtc");
        evidence.Save(receipt.SentAtUtc, factory.Services.GetRequiredService<RealtimeFailureSwitch>().PublicationStartedAtUtc(receipt.MessageId));
    }

    [Fact]
    public async Task StabilizationPhoneFormatsResolveOneAccountAndOneContact()
    {
        using var sender = factory.CreateClient();
        using var receiver = factory.CreateClient();
        var a = await CreateMessagingUserAsync(sender);
        var b = await CreateMessagingUserAsync(receiver);
        await UpdatePrivacyAsync(receiver, PrivacyAudience.Everyone, PrivacyAudience.Nobody,
            PrivacyAudience.Everyone, PrivacyAudience.Contacts, null);
        var national = "0" + b.PhoneNumber[3..];
        string Digits(string value, char zero) => string.Concat(value.Select(c => char.IsAsciiDigit(c) ? (char)(zero + c - '0') : c));
        string[] formats = [national, b.PhoneNumber, b.PhoneNumber[1..], "00" + b.PhoneNumber[1..],
            Digits(national, '۰'), Digits(national, '٠'), national[..4] + " " + national[4..], national[..4] + "-" + national[4..]];
        foreach (var format in formats)
        {
            var resolved = await ResolvePhoneAsync(sender, format);
            Assert.Equal(b.UserId, resolved.Profile!.UserId);
            using var saved = await sender.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(resolved.Profile.UserId, null), JsonOptions);
            saved.EnsureSuccessStatusCode();
        }
        Assert.Equal(1, await factory.CountUserContactsAsync(a.UserId, b.UserId));
    }

    [Fact]
    public async Task StabilizationParentManagerPersonalDirectDoesNotDependOnOrganizationRole()
    {
        using var parentClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        var parent = await CreateMessagingUserAsync(parentClient);
        var manager = await CreateMessagingUserAsync(managerClient);
        var organization = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateMembershipAsync(parent.UserId, organization, OrganizationRole.Parent);
        await factory.CreateMembershipAsync(manager.UserId, organization, OrganizationRole.Manager);
        await SelectPersonalAsync(parentClient);
        await SelectPersonalAsync(managerClient);
        var direct = await StartDirectAndReadAsync(parentClient, manager.UserId, ConversationScope.Personal);
        await SendMessageAndReadAsync(parentClient, direct.ConversationId, Guid.NewGuid(), "پرسش محلی والد");
        await SendMessageAndReadAsync(managerClient, direct.ConversationId, Guid.NewGuid(), "پاسخ محلی مدیر");
        await UpdatePrivacyAsync(managerClient, PrivacyAudience.Nobody, PrivacyAudience.Nobody,
            PrivacyAudience.Nobody, PrivacyAudience.Nobody, null);
        using var denied = await StartDirectAsync(parentClient, manager.UserId, ConversationScope.Personal);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(MessagingErrorCodes.DirectRecipientNotAvailable, await ReadProblemCodeAsync(denied));
    }

    private static async Task<PeopleResolveResult> ResolveUsernameForStabilizationAsync(HttpClient client, string username)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(username, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PeopleResolveResult>(JsonOptions))!;
    }

    [Fact]
    public async Task StabilizationTwoIndependentReceiverSessionsSynchronizeReadWithoutRegression()
    {
        using var sender = factory.CreateClient();
        using var firstDevice = factory.CreateClient();
        using var secondDevice = factory.CreateClient();
        var a = await CreateMessagingUserAsync(sender);
        var b = await CreateMessagingUserAsync(firstDevice);
        using var challengeResponse = await secondDevice.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(b.PhoneNumber), JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var login = await secondDevice.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, b.PhoneNumber, factory.GetLatestOtpCode(b.PhoneNumber)), JsonOptions);
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        Assert.Equal(b.UserId, auth.User.Id);
        Assert.NotEqual(b.AccessToken, auth.AccessToken);
        UseBearerToken(secondDevice, auth.AccessToken);
        await using var firstLive = CreateHubConnection(b.AccessToken);
        await using var secondLive = CreateHubConnection(auth.AccessToken);
        var firstMessage = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondMessage = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readChanged = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        firstLive.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, e =>
        {
            if (e.Type == MessagingChangeType.MessageCreated) firstMessage.TrySetResult(e);
            if (e.Type == MessagingChangeType.ReadCursorAdvanced) readChanged.TrySetResult(e);
        });
        secondLive.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, e =>
        { if (e.Type == MessagingChangeType.MessageCreated) secondMessage.TrySetResult(e); });
        await firstLive.StartAsync(); await secondLive.StartAsync();
        var direct = await StartDirectAndReadAsync(sender, b.UserId, ConversationScope.Personal);
        var sent = await SendMessageAndReadAsync(sender, direct.ConversationId, Guid.NewGuid(), "پیام برای دو نشست مستقل");
        Assert.Equal(sent.MessageId, (await firstMessage.Task.WaitAsync(TimeSpan.FromSeconds(15))).ResourceId);
        Assert.Equal(sent.MessageId, (await secondMessage.Task.WaitAsync(TimeSpan.FromSeconds(15))).ResourceId);
        var newer = await SendMessageAndReadAsync(sender, direct.ConversationId, Guid.NewGuid(), "پیام جدیدتر برای رسید معتبر قدیمی");
        await AdvanceCursorAsync(secondDevice, direct.ConversationId, "read", newer.Sequence);
        Assert.Equal(direct.ConversationId, (await readChanged.Task.WaitAsync(TimeSpan.FromSeconds(15))).ConversationId);
        await AdvanceCursorAsync(firstDevice, direct.ConversationId, "read", sent.Sequence);
        var summary = (await firstDevice.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{direct.ConversationId}/summary", JsonOptions))!;
        Assert.Equal(newer.Sequence, summary.LastReadMessageSequence);
        Assert.Equal(0, summary.UnreadCount);
    }

    [Fact]
    public async Task StabilizationOfflineThreeMessagesAndEditRecoverFromDurableConversationCursor()
    {
        using var sender = factory.CreateClient();
        using var receiver = factory.CreateClient();
        _ = await CreateMessagingUserAsync(sender);
        var b = await CreateMessagingUserAsync(receiver);
        var direct = await StartDirectAndReadAsync(sender, b.UserId, ConversationScope.Personal);
        var before = await GetChangesAsync(receiver, direct.ConversationId);
        await using var live = CreateHubConnection(b.AccessToken);
        await live.StartAsync(); await live.StopAsync();
        var receipts = new List<MessageReceiptResult>();
        for (var i = 0; i < 3; i++) receipts.Add(await SendMessageAndReadAsync(sender, direct.ConversationId, Guid.NewGuid(), "پیام آفلاین " + i));
        using var edited = await sender.PatchAsJsonAsync($"/api/v1/conversations/{direct.ConversationId}/messages/{receipts[0].MessageId}",
            new EditMessageCommand("نسخهٔ تازهٔ پیام آفلاین", receipts[0].Version), JsonOptions);
        edited.EnsureSuccessStatusCode();
        await live.StartAsync();
        var replay = await GetChangesAsync(receiver, direct.ConversationId, before.NextCursor);
        Assert.Equal(3, replay.Changes.Count(e => e.Type == MessagingChangeType.MessageCreated));
        Assert.Single(replay.Changes, e => e.Type == MessagingChangeType.MessageEdited);
        Assert.Empty((await GetChangesAsync(receiver, direct.ConversationId, replay.NextCursor)).Changes);
        var duplicate = await GetChangesAsync(receiver, direct.ConversationId, before.NextCursor);
        Assert.Equal(replay.Changes.Select(e => e.Cursor), duplicate.Changes.Select(e => e.Cursor));
        var messages = (await receiver.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{direct.ConversationId}/messages", JsonOptions))!;
        Assert.Equal(3, messages.Messages.Count);
        Assert.Equal(3, messages.Messages.Select(m => m.MessageId).Distinct().Count());
        Assert.Equal("نسخهٔ تازهٔ پیام آفلاین", messages.Messages.Single(m => m.MessageId == receipts[0].MessageId).Text);
    }

    [Fact]
    public async Task StabilizationDeepLinkSummaryFindsArchivedDirectAndConcealsOutsider()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        _ = await CreateMessagingUserAsync(outsiderClient);
        var direct = await StartDirectAndReadAsync(ownerClient, target.UserId, ConversationScope.Personal);
        using var archived = await ownerClient.PostAsync($"/api/v1/conversations/{direct.ConversationId}/archive", null);
        archived.EnsureSuccessStatusCode();
        var inbox = await ownerClient.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations?pageSize=1", JsonOptions);
        Assert.DoesNotContain(inbox!, item => item.ConversationId == direct.ConversationId);
        var summary = await ownerClient.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{direct.ConversationId}/summary", JsonOptions);
        Assert.Equal(direct.ConversationId, summary!.ConversationId);
        Assert.True(summary.IsArchived);
        Assert.Equal(target.UserId, summary.OtherParticipant!.UserId);
        using var denied = await outsiderClient.GetAsync($"/api/v1/conversations/{direct.ConversationId}/summary");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(denied));
        using var managed = await ownerClient.GetAsync($"/api/v1/conversations/{direct.ConversationId}");
        Assert.Equal(HttpStatusCode.NotFound, managed.StatusCode);
    }

    [Fact]
    public async Task StabilizationDirectDenialHasSafeInternalCategoryWithoutDisclosingPrivateReason()
    {
        using var actorClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient);
        var target = await CreateMessagingUserAsync(targetClient);
        await UpdatePrivacyAsync(targetClient, PrivacyAudience.Nobody, PrivacyAudience.Nobody,
            PrivacyAudience.Nobody, PrivacyAudience.Nobody, null);
        factory.SensitiveLogs.Clear();
        using var denied = await StartDirectAsync(actorClient, target.UserId, ConversationScope.Personal);
        Assert.Equal(MessagingErrorCodes.DirectRecipientNotAvailable, await ReadProblemCodeAsync(denied));
        var denialLogs = factory.SensitiveLogs.Entries().Where(e => e.Properties.ContainsKey("DenialCategory")).ToArray();
        var entry = Assert.Single(denialLogs);
        Assert.Equal("PrivacyDenied", entry.Properties["DenialCategory"]);
        Assert.NotNull(entry.Properties["TraceId"]);
        var log = string.Join(' ', entry.Properties.Values);
        Assert.DoesNotContain(target.PhoneNumber, log);
        Assert.DoesNotContain(target.Username, log);
        Assert.DoesNotContain(target.UserId.ToString(), log);
        Assert.DoesNotContain(actor.AccessToken, log);
        Assert.Null(entry.ExceptionText);
        var problem = await denied.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PrivacyDenied", problem);
    }
}
