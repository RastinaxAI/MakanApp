using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    private static readonly DateTime BehaviorBaseUtc =
        new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(59, 59, HttpStatusCode.OK, null)]
    [InlineData(60, 0, HttpStatusCode.OK, null)]
    [InlineData(60, 1, HttpStatusCode.Conflict, MessagingErrorCodes.MessageEditWindowExpired)]
    public async Task EditUsesExactOneHourServerWindow(
        int elapsedMinutes,
        int elapsedSeconds,
        HttpStatusCode expectedStatus,
        string? expectedCode)
    {
        factory.Clock.SetUtcNow(BehaviorBaseUtc);
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            var sent = await SendMessageAndReadAsync(
                setup.FirstClient,
                setup.ConversationId,
                Guid.NewGuid(),
                "متن اولیه");
            factory.Clock.SetUtcNow(
                BehaviorBaseUtc.AddMinutes(elapsedMinutes).AddSeconds(elapsedSeconds));

            using var response = await setup.FirstClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
                new EditMessageCommand("متن ویرایش‌شده", sent.Version),
                JsonOptions);

            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedCode is not null)
            {
                Assert.Equal(expectedCode, await ReadProblemCodeAsync(response));
            }
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Theory]
    [InlineData(60, 0, HttpStatusCode.OK, null)]
    [InlineData(60, 1, HttpStatusCode.Conflict, MessagingErrorCodes.MessageDeleteWindowExpired)]
    public async Task SenderDeleteForEveryoneUsesExactOneHourServerWindow(
        int elapsedMinutes,
        int elapsedSeconds,
        HttpStatusCode expectedStatus,
        string? expectedCode)
    {
        factory.Clock.SetUtcNow(BehaviorBaseUtc);
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            var sent = await SendMessageAndReadAsync(
                setup.FirstClient,
                setup.ConversationId,
                Guid.NewGuid(),
                "محتوای حذف");
            factory.Clock.SetUtcNow(
                BehaviorBaseUtc.AddMinutes(elapsedMinutes).AddSeconds(elapsedSeconds));

            using var response = await setup.FirstClient.DeleteAsync(
                $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(sent.Version)}");

            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedCode is not null)
            {
                Assert.Equal(expectedCode, await ReadProblemCodeAsync(response));
            }
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task DeleteForMeIsIdempotentPrivateAndReplyUsesUnavailablePlaceholder()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        const string secret = "hidden-source-14c";
        var source = await SendMessageAndReadAsync(
            setup.SecondClient,
            setup.ConversationId,
            Guid.NewGuid(),
            secret);

        using var firstHide = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}/for-me");
        using var duplicateHide = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}/for-me");
        var firstResult = await firstHide.Content.ReadFromJsonAsync<DeleteMessageForMeResult>(JsonOptions);
        var duplicateResult = await duplicateHide.Content.ReadFromJsonAsync<DeleteMessageForMeResult>(JsonOptions);
        var firstHistoryBeforeReply = await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            JsonOptions);
        var secondHistory = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            JsonOptions);

        var reply = await SendAdvancedMessageAsync(
            setup.SecondClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "پاسخ",
                ReplyToMessageId = source.MessageId
            });
        var firstHistoryAfterReply = await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            JsonOptions);
        var search = await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>(
            $"/api/v1/messages/search?q={secret}&conversationId={setup.ConversationId}",
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, firstHide.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateHide.StatusCode);
        Assert.False(firstResult!.AlreadyHidden);
        Assert.True(duplicateResult!.AlreadyHidden);
        Assert.Empty(firstHistoryBeforeReply!.Messages);
        Assert.Contains(secondHistory!.Messages, item => item.MessageId == source.MessageId);
        var visibleReply = Assert.Single(firstHistoryAfterReply!.Messages);
        Assert.Equal(reply.MessageId, visibleReply.MessageId);
        Assert.True(visibleReply.Reply!.IsUnavailable);
        Assert.Null(visibleReply.Reply.Text);
        Assert.Empty(search!.Items);
    }

    [Fact]
    public async Task DeleteForMeAlsoBlocksSharedMediaAndDirectFileAccessOnlyForCaller()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(setup.SecondClient, "image/png", "hidden-14c.png");
        var message = await SendAdvancedMessageAsync(
            setup.SecondClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            });

        using var hide = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/for-me");
        hide.EnsureSuccessStatusCode();
        var firstMedia = await setup.FirstClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media",
            JsonOptions);
        var secondMedia = await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media",
            JsonOptions);
        using var firstDownload = await setup.FirstClient.GetAsync($"/api/v1/files/{file.Id}/content");
        using var secondDownload = await setup.SecondClient.GetAsync($"/api/v1/files/{file.Id}/content");

        Assert.Empty(firstMedia!.Items);
        Assert.Contains(secondMedia!.Items, item => item.MessageId == message.MessageId);
        Assert.Equal(HttpStatusCode.NotFound, firstDownload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondDownload.StatusCode);
    }

    [Fact]
    public async Task PublicFromJoinGroupIsDiscoverableAndPreservesJoinSequenceBoundary()
    {
        using var ownerClient = factory.CreateClient();
        using var joinerClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        _ = await CreateMessagingUserAsync(joinerClient);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Personal,
                "گروه عمومی تاریخچه",
                null,
                [],
                ConversationVisibility.Public,
                GroupHistoryPolicy.FromJoin,
                GroupMemberCapability.LegacyMemberDefault));
        var oldMessage = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "قبل از عضویت");

        var discovery = await joinerClient.GetFromJsonAsync<DiscoverableConversationPageResult>(
            "/api/v1/conversations/discover?type=Group&q=تاریخچه",
            JsonOptions);
        using var join = await joinerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);
        join.EnsureSuccessStatusCode();
        var newMessage = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "بعد از عضویت");
        var history = await joinerClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            JsonOptions);

        Assert.Contains(discovery!.Items, item => item.ConversationId == group.ConversationId);
        var visible = Assert.Single(history!.Messages);
        Assert.Equal(newMessage.MessageId, visible.MessageId);
        Assert.DoesNotContain(history.Messages, item => item.MessageId == oldMessage.MessageId);
    }

    [Fact]
    public async Task HistoryPolicyChangeAffectsFutureJoinsWithoutRewritingExistingBoundary()
    {
        using var ownerClient = factory.CreateClient();
        using var firstJoinerClient = factory.CreateClient();
        using var secondJoinerClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(ownerClient);
        _ = await CreateMessagingUserAsync(firstJoinerClient);
        _ = await CreateMessagingUserAsync(secondJoinerClient);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Personal,
                "تغییر سیاست تاریخچه",
                null,
                [],
                ConversationVisibility.Public,
                GroupHistoryPolicy.FullHistory,
                GroupMemberCapability.LegacyMemberDefault));
        var first = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پیام اول");
        using var firstJoin = await firstJoinerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);
        firstJoin.EnsureSuccessStatusCode();
        var current = await ownerClient.GetFromJsonAsync<ManagedConversationResult>(
            $"/api/v1/conversations/{group.ConversationId}",
            JsonOptions);
        using var settings = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/settings",
            new UpdateConversationSettingsCommand(
                ConversationVisibility.Public,
                GroupHistoryPolicy.FromJoin,
                GroupMemberCapability.LegacyMemberDefault,
                current!.Version),
            JsonOptions);
        settings.EnsureSuccessStatusCode();
        var second = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پیام دوم");
        using var secondJoin = await secondJoinerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);
        secondJoin.EnsureSuccessStatusCode();
        var third = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پیام سوم");

        var firstHistory = await firstJoinerClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            JsonOptions);
        var secondHistory = await secondJoinerClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            JsonOptions);

        Assert.Equal(
            new[] { first.MessageId, second.MessageId, third.MessageId },
            firstHistory!.Messages.Select(item => item.MessageId));
        Assert.Equal(third.MessageId, Assert.Single(secondHistory!.Messages).MessageId);
    }

    [Fact]
    public async Task GroupMemberCapabilitiesSeparateTextAndMediaPublishing()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Personal,
                "قابلیت اعضا",
                null,
                [member.UserId],
                ConversationVisibility.Private,
                GroupHistoryPolicy.FullHistory,
                GroupMemberCapability.SendMessages));

        using var text = await SendMessageAsync(
            memberClient,
            group.ConversationId,
            Guid.NewGuid(),
            "متن مجاز");
        var file = await UploadFileAsync(memberClient, "image/png", "capability.png");
        using var media = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, text.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, media.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessagePublishNotAllowed, await ReadProblemCodeAsync(media));
    }

    [Fact]
    public async Task PrivateConversationIsNotDiscoverableOrJoinableByGuid()
    {
        using var ownerClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(ownerClient);
        _ = await CreateMessagingUserAsync(outsiderClient);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("گروه خصوصی 14C"));

        var discovery = await outsiderClient.GetFromJsonAsync<DiscoverableConversationPageResult>(
            "/api/v1/conversations/discover?q=خصوصی",
            JsonOptions);
        using var join = await outsiderClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);

        Assert.DoesNotContain(discovery!.Items, item => item.ConversationId == group.ConversationId);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotJoinable, await ReadProblemCodeAsync(join));
    }

    [Fact]
    public async Task ModeratorDeleteRequiresExplicitPermissionAndWorksBeyondSenderWindow()
    {
        factory.Clock.SetUtcNow(BehaviorBaseUtc);
        try
        {
            using var ownerClient = factory.CreateClient();
            using var adminClient = factory.CreateClient();
            using var memberClient = factory.CreateClient();
            var owner = await CreateMessagingUserAsync(ownerClient);
            var admin = await CreateMessagingUserAsync(adminClient);
            var member = await CreateMessagingUserAsync(memberClient);
            await factory.AllowPersonalGroupInviteAsync(owner.UserId, admin.UserId);
            await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
            var group = await CreateManagedAndReadAsync(
                ownerClient,
                "groups",
                PersonalCommand("مدیریت حذف 14C", admin.UserId, member.UserId));
            using var promoteResponse = await ownerClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/role",
                new ChangeConversationMemberRoleCommand(
                    ConversationParticipantRole.Admin,
                    ConversationAdminPermission.None),
                JsonOptions);
            promoteResponse.EnsureSuccessStatusCode();
            var promoted = await promoteResponse.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
            var adminVersion = promoted!.Participants.Single(item => item.User.UserId == admin.UserId).Version;
            var message = await SendMessageAndReadAsync(
                memberClient,
                group.ConversationId,
                Guid.NewGuid(),
                "پیام نیازمند moderation");
            using var promoteTarget = await ownerClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}/role",
                new ChangeConversationMemberRoleCommand(
                    ConversationParticipantRole.Admin,
                    ConversationAdminPermission.None),
                JsonOptions);
            promoteTarget.EnsureSuccessStatusCode();
            var promotedTarget = await promoteTarget.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
            var targetVersion = promotedTarget!.Participants.Single(item => item.User.UserId == member.UserId).Version;
            factory.Clock.SetUtcNow(BehaviorBaseUtc.AddHours(2));

            using var denied = await adminClient.DeleteAsync(
                $"/api/v1/conversations/{group.ConversationId}/messages/{message.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(message.Version)}");
            using var grant = await ownerClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/permissions",
                new UpdateConversationPermissionsCommand(
                    ConversationAdminPermission.DeleteMessages,
                    adminVersion),
                JsonOptions);
            grant.EnsureSuccessStatusCode();
            var granted = await grant.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
            var grantedVersion = granted!.Participants.Single(item => item.User.UserId == admin.UserId).Version;
            using var deniedManageAdmin = await adminClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}/permissions",
                new UpdateConversationPermissionsCommand(
                    ConversationAdminPermission.DeleteMessages,
                    targetVersion),
                JsonOptions);
            using var staleGrant = await ownerClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/permissions",
                new UpdateConversationPermissionsCommand(
                    ConversationAdminPermission.All,
                    adminVersion),
                JsonOptions);
            using var grantManageAdmins = await ownerClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/permissions",
                new UpdateConversationPermissionsCommand(
                    ConversationAdminPermission.DeleteMessages |
                    ConversationAdminPermission.ManageAdmins,
                    grantedVersion),
                JsonOptions);
            grantManageAdmins.EnsureSuccessStatusCode();
            using var deniedEscalation = await adminClient.PatchAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}/permissions",
                new UpdateConversationPermissionsCommand(
                    ConversationAdminPermission.All,
                    targetVersion),
                JsonOptions);
            using var accepted = await adminClient.DeleteAsync(
                $"/api/v1/conversations/{group.ConversationId}/messages/{message.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(message.Version)}");

            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationPermissionRequired, await ReadProblemCodeAsync(denied));
            Assert.Equal(HttpStatusCode.Forbidden, deniedManageAdmin.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationPermissionRequired, await ReadProblemCodeAsync(deniedManageAdmin));
            Assert.Equal(HttpStatusCode.PreconditionFailed, staleGrant.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationStateConflict, await ReadProblemCodeAsync(staleGrant));
            Assert.Equal(HttpStatusCode.Forbidden, deniedEscalation.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationPermissionRequired, await ReadProblemCodeAsync(deniedEscalation));
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task PersonalMutePinAndManualUnreadRemainPrivateAndReadCursorMonotonic()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "پیام state");
        using var readResponse = await setup.SecondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/read",
            new AdvanceConversationCursorCommand(sent.Sequence),
            JsonOptions);
        var read = await readResponse.Content.ReadFromJsonAsync<ConversationCursorStateResult>(JsonOptions);
        using var unreadResponse = await setup.SecondClient.PostAsync(
            $"/api/v1/conversations/{setup.ConversationId}/mark-unread",
            null);
        var unread = await unreadResponse.Content.ReadFromJsonAsync<ConversationUserStateResult>(JsonOptions);
        using var muteResponse = await setup.SecondClient.PutAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/mute",
            new SetConversationMuteCommand(ConversationMuteMode.Permanent, null),
            JsonOptions);
        using var temporaryMuteResponse = await setup.SecondClient.PutAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/mute",
            new SetConversationMuteCommand(
                ConversationMuteMode.Until,
                DateTime.UtcNow.AddHours(1)),
            JsonOptions);
        var temporaryMute = await temporaryMuteResponse.Content.ReadFromJsonAsync<ConversationUserStateResult>(
            JsonOptions);
        using var unmuteResponse = await setup.SecondClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/mute");
        var unmuted = await unmuteResponse.Content.ReadFromJsonAsync<ConversationUserStateResult>(JsonOptions);
        using var restoreMuteResponse = await setup.SecondClient.PutAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/mute",
            new SetConversationMuteCommand(ConversationMuteMode.Permanent, null),
            JsonOptions);
        using var pinResponse = await setup.SecondClient.PostAsync(
            $"/api/v1/conversations/{setup.ConversationId}/state/pin",
            null);
        var secondInbox = await setup.SecondClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations?filter=Unread",
            JsonOptions);
        var firstInbox = await setup.FirstClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations",
            JsonOptions);

        Assert.Equal(sent.Sequence, read!.LastReadMessageSequence);
        Assert.True(unread!.IsManuallyUnread);
        Assert.Equal(HttpStatusCode.OK, muteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, temporaryMuteResponse.StatusCode);
        Assert.Equal(ConversationMuteMode.Until, temporaryMute!.MuteMode);
        Assert.NotNull(temporaryMute.MutedUntilUtc);
        Assert.Equal(HttpStatusCode.OK, unmuteResponse.StatusCode);
        Assert.False(unmuted!.IsMuted);
        Assert.Equal(HttpStatusCode.OK, restoreMuteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pinResponse.StatusCode);
        var secondSummary = Assert.Single(secondInbox!);
        Assert.True(secondSummary.IsManuallyUnread);
        Assert.True(secondSummary.IsMuted);
        Assert.True(secondSummary.IsPinned);
        var firstSummary = Assert.Single(firstInbox!);
        Assert.False(firstSummary.IsManuallyUnread);
        Assert.False(firstSummary.IsMuted);
        Assert.False(firstSummary.IsPinned);

        using var unpinResponse = await setup.SecondClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/state/pin");
        var unpinned = await unpinResponse.Content.ReadFromJsonAsync<ConversationUserStateResult>(JsonOptions);
        using var clearManualUnreadResponse = await setup.SecondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/read",
            new AdvanceConversationCursorCommand(sent.Sequence),
            JsonOptions);
        var clearedInbox = await setup.SecondClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations",
            JsonOptions);
        var clearedSummary = Assert.Single(clearedInbox!);
        Assert.Equal(HttpStatusCode.OK, unpinResponse.StatusCode);
        Assert.False(unpinned!.IsPinned);
        Assert.Equal(HttpStatusCode.OK, clearManualUnreadResponse.StatusCode);
        Assert.Equal(sent.Sequence, clearedSummary.LastReadMessageSequence);
        Assert.False(clearedSummary.IsManuallyUnread);
    }

    [Fact]
    public async Task ChatListSupportsTypeFiltersBoundedPaginationAndPinnedFirstOrdering()
    {
        using var ownerClient = factory.CreateClient();
        using var peerClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(ownerClient);
        var peer = await CreateMessagingUserAsync(peerClient);
        var direct = await StartDirectAndReadAsync(
            ownerClient,
            peer.UserId,
            ConversationScope.Personal);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("list-filter-group"));
        var channel = await CreateManagedAndReadAsync(
            ownerClient,
            "channels",
            PersonalCommand("list-filter-channel"));

        await SendMessageAndReadAsync(
            ownerClient,
            channel.ConversationId,
            Guid.NewGuid(),
            "latest activity");
        using var pinResponse = await ownerClient.PostAsync(
            $"/api/v1/conversations/{direct.ConversationId}/state/pin",
            null);

        var firstPage = await ownerClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations?page=1&pageSize=1",
            JsonOptions);
        var directItems = await ownerClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations?filter=Direct",
            JsonOptions);
        var groupItems = await ownerClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations?filter=Group",
            JsonOptions);
        var channelItems = await ownerClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations?filter=Channel",
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, pinResponse.StatusCode);
        Assert.Equal(direct.ConversationId, Assert.Single(firstPage!).ConversationId);
        Assert.All(directItems!, item => Assert.Equal(ConversationType.Direct, item.Type));
        Assert.Contains(directItems!, item => item.ConversationId == direct.ConversationId);
        Assert.All(groupItems!, item => Assert.Equal(ConversationType.Group, item.Type));
        Assert.Contains(groupItems!, item => item.ConversationId == group.ConversationId);
        Assert.All(channelItems!, item => Assert.Equal(ConversationType.Channel, item.Type));
        Assert.Contains(channelItems!, item => item.ConversationId == channel.ConversationId);
    }

    [Fact]
    public async Task OrganizationPublicDiscoveryAndJoinStayInsideSelectedOrganization()
    {
        using var managerClient = factory.CreateClient();
        using var colleagueClient = factory.CreateClient();
        using var managerOnlyClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient);
        var colleague = await CreateMessagingUserAsync(colleagueClient);
        var managerOnly = await CreateMessagingUserAsync(managerOnlyClient);
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        var organizationName = NewOrganizationName();
        var organizationId = await factory.CreateOrganizationAsync(organizationName);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerMembership = await factory.CreateMembershipAsync(
            manager.UserId,
            organizationId,
            MakanApp.Domain.Organization.OrganizationRole.Manager);
        var colleagueMembership = await factory.CreateMembershipAsync(
            colleague.UserId,
            organizationId,
            MakanApp.Domain.Organization.OrganizationRole.Teacher);
        var managerOnlyMembership = await factory.CreateMembershipAsync(
            managerOnly.UserId,
            organizationId,
            MakanApp.Domain.Organization.OrganizationRole.Manager);
        var outsiderMembership = await factory.CreateMembershipAsync(
            outsider.UserId,
            otherOrganizationId,
            MakanApp.Domain.Organization.OrganizationRole.Manager);
        await SelectOrganizationAsync(
            managerClient,
            managerMembership.MembershipId,
            MakanApp.Domain.Organization.OrganizationRole.Manager);
        await SelectOrganizationAsync(
            colleagueClient,
            colleagueMembership.MembershipId,
            MakanApp.Domain.Organization.OrganizationRole.Teacher);
        await SelectOrganizationAsync(
            managerOnlyClient,
            managerOnlyMembership.MembershipId,
            MakanApp.Domain.Organization.OrganizationRole.Manager);
        await SelectOrganizationAsync(
            outsiderClient,
            outsiderMembership.MembershipId,
            MakanApp.Domain.Organization.OrganizationRole.Manager);
        var group = await CreateManagedAndReadAsync(
            managerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Organization,
                "گروه عمومی سازمان",
                null,
                [],
                ConversationVisibility.Public,
                GroupHistoryPolicy.FullHistory,
                GroupMemberCapability.LegacyMemberDefault));

        var colleagueDiscovery = await colleagueClient.GetFromJsonAsync<DiscoverableConversationPageResult>(
            "/api/v1/conversations/discover",
            JsonOptions);
        var outsiderDiscovery = await outsiderClient.GetFromJsonAsync<DiscoverableConversationPageResult>(
            "/api/v1/conversations/discover",
            JsonOptions);
        using var colleagueJoin = await colleagueClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);
        using var managerOnlyJoin = await managerOnlyClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);
        using var outsiderJoin = await outsiderClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/join",
            null);
        var colleagueMessage = await SendMessageAndReadAsync(
            colleagueClient,
            group.ConversationId,
            Guid.NewGuid(),
            "organization role is not conversation authority");
        using var managerOnlyDelete = await managerOnlyClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{colleagueMessage.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(colleagueMessage.Version)}");
        var systemManagedId = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            manager.UserId,
            ConversationType.Group);
        using var systemManagedJoin = await colleagueClient.PostAsync(
            $"/api/v1/conversations/{systemManagedId}/join",
            null);

        var discovered = Assert.Single(
            colleagueDiscovery!.Items,
            item => item.ConversationId == group.ConversationId);
        Assert.Equal(organizationId, discovered.Organization!.OrganizationId);
        Assert.Equal(organizationName, discovered.Organization.OrganizationName);
        Assert.DoesNotContain(outsiderDiscovery!.Items, item => item.ConversationId == group.ConversationId);
        Assert.Equal(HttpStatusCode.OK, colleagueJoin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, managerOnlyJoin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerOnlyDelete.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationPermissionRequired, await ReadProblemCodeAsync(managerOnlyDelete));
        Assert.Equal(HttpStatusCode.NotFound, outsiderJoin.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotJoinable, await ReadProblemCodeAsync(outsiderJoin));
        Assert.Equal(HttpStatusCode.NotFound, systemManagedJoin.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotJoinable, await ReadProblemCodeAsync(systemManagedJoin));
    }
}
