using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using System.Reflection;
using System.Threading.RateLimiting;
using MakanApp.Api.Controllers;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2EphemeralTypingReachesOnlyAuthorizedSubscribedPeerWithoutMessageOrDelta()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var before = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        var messages = (await factory.GetMessagesAsync(setup.ConversationId)).Count();
        await using var source = CreateHubConnection(setup.First.AccessToken); await using var peer = CreateHubConnection(setup.Second.AccessToken);
        var events = Channel.CreateUnbounded<TypingNotification>(); peer.On<TypingNotification>("TypingChanged", n => events.Writer.TryWrite(n));
        await Task.WhenAll(source.StartAsync(), peer.StartAsync());
        await source.InvokeAsync("SubscribeConversation", setup.ConversationId); await peer.InvokeAsync("SubscribeConversation", setup.ConversationId);
        await source.InvokeAsync("SetTyping", setup.ConversationId, true);
        var notification = await ReadP2TypingAsync(events);
        Assert.True(notification.IsTyping); Assert.Equal(setup.First.UserId, notification.UserId); Assert.Equal(DateTimeKind.Utc, notification.ExpiresAtUtc.Kind);
        Assert.InRange(notification.ExpiresAtUtc - DateTime.UtcNow, TimeSpan.Zero, TimeSpan.FromSeconds(7));
        Assert.Equal(messages, (await factory.GetMessagesAsync(setup.ConversationId)).Count());
        Assert.Empty((await GetChangesAsync(setup.SecondClient, setup.ConversationId, before.NextCursor)).Changes);
        var audience = factory.Services.GetRequiredService<MessagingEphemeralTestControl>().Typing.Last(n => n.Value.ConversationId == setup.ConversationId);
        Assert.Equal(peer.ConnectionId, Assert.Single(audience.Connections));
    }

    [Fact]
    public async Task P2EphemeralTypingExpiresWithoutDisconnectAndSendStopsCurrentSessionTyping()
    {
        var start = DateTime.UtcNow; factory.Clock.SetUtcNow(start);
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            await using var source = CreateHubConnection(setup.First.AccessToken); await using var peer = CreateHubConnection(setup.Second.AccessToken);
            var events = Channel.CreateUnbounded<TypingNotification>(); peer.On<TypingNotification>("TypingChanged", n => events.Writer.TryWrite(n));
            await Task.WhenAll(source.StartAsync(), peer.StartAsync());
            await source.InvokeAsync("SubscribeConversation", setup.ConversationId); await peer.InvokeAsync("SubscribeConversation", setup.ConversationId);
            await source.InvokeAsync("SetTyping", setup.ConversationId, true); Assert.True((await ReadP2TypingAsync(events)).IsTyping);
            factory.Clock.SetUtcNow(start.AddSeconds(7)); await FlushP2EphemeralAsync();
            Assert.False((await ReadP2TypingAsync(events)).IsTyping);
            await source.InvokeAsync("SetTyping", setup.ConversationId, true); Assert.True((await ReadP2TypingAsync(events)).IsTyping);
            await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p2 stop typing");
            await FlushP2EphemeralAsync(); Assert.False((await ReadP2TypingAsync(events)).IsTyping);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task P2EphemeralTwoSessionsAggregateTypingUntilBothStop()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var secondDevice = factory.CreateClient();
        var login = await AuthenticateP1SessionAsync(secondDevice, setup.First.PhoneNumber, factory.Services);
        await using var one = CreateHubConnection(setup.First.AccessToken); await using var two = CreateHubConnection(login.AccessToken);
        await using var peer = CreateHubConnection(setup.Second.AccessToken);
        var events = Channel.CreateUnbounded<TypingNotification>(); peer.On<TypingNotification>("TypingChanged", n => events.Writer.TryWrite(n));
        foreach (var c in new[] { one, two, peer }) { await c.StartAsync(); await c.InvokeAsync("SubscribeConversation", setup.ConversationId); }
        await one.InvokeAsync("SetTyping", setup.ConversationId, true); Assert.True((await ReadP2TypingAsync(events)).IsTyping);
        await two.InvokeAsync("SetTyping", setup.ConversationId, true); Assert.True((await ReadP2TypingAsync(events)).IsTyping);
        await one.InvokeAsync("SetTyping", setup.ConversationId, false); Assert.True((await ReadP2TypingAsync(events)).IsTyping);
        await two.InvokeAsync("SetTyping", setup.ConversationId, false); Assert.False((await ReadP2TypingAsync(events)).IsTyping);
    }

    [Fact]
    public async Task P2EphemeralTypingStopsOnUnsubscribeAndDisconnect()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await using var source = CreateHubConnection(setup.First.AccessToken); await using var peer = CreateHubConnection(setup.Second.AccessToken);
        var events = Channel.CreateUnbounded<TypingNotification>(); peer.On<TypingNotification>("TypingChanged", n => events.Writer.TryWrite(n));
        foreach (var c in new[] { source, peer }) { await c.StartAsync(); await c.InvokeAsync("SubscribeConversation", setup.ConversationId); }
        await source.InvokeAsync("SetTyping", setup.ConversationId, true); await ReadP2TypingAsync(events);
        await source.InvokeAsync("UnsubscribeConversation", setup.ConversationId); await FlushP2EphemeralAsync(); Assert.False((await ReadP2TypingAsync(events)).IsTyping);
        await source.InvokeAsync("SubscribeConversation", setup.ConversationId); await source.InvokeAsync("SetTyping", setup.ConversationId, true); await ReadP2TypingAsync(events);
        await source.StopAsync(); await FlushP2EphemeralAsync(); Assert.False((await ReadP2TypingAsync(events)).IsTyping);
    }

    [Fact]
    public async Task P2EphemeralBlockedPublisherAndPendingHintAreDeniedWithoutDeletingHistory()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p2 retained history");
        await using var source = CreateHubConnection(setup.First.AccessToken); await using var peer = CreateHubConnection(setup.Second.AccessToken);
        foreach (var c in new[] { source, peer }) { await c.StartAsync(); await c.InvokeAsync("SubscribeConversation", setup.ConversationId); }
        // حالت مجاز قبل از Block را enqueue می‌کنیم؛ dispatch باید وضعیت تازه را بسنجد.
        var state = factory.Services.GetRequiredService<IMessagingEphemeralState>(); state.SetTyping(source.ConnectionId!, setup.ConversationId, true, DateTime.UtcNow);
        (await setup.SecondClient.PostAsync($"/api/v1/messaging/blocks/{setup.First.UserId}", null)).EnsureSuccessStatusCode();
        await FlushP2EphemeralAsync();
        Assert.DoesNotContain(factory.Services.GetRequiredService<MessagingEphemeralTestControl>().Typing, n => n.Value.ConversationId == setup.ConversationId);
        await Assert.ThrowsAsync<HubException>(() => source.InvokeAsync("SetTyping", setup.ConversationId, true));
        Assert.Single((await setup.FirstClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!.Messages);
    }

    [Fact]
    public async Task P2EphemeralPresenceContactsUsesTargetsContactNotViewersContactAndBlockHidesIt()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await using var subject = CreateHubConnection(setup.Second.AccessToken);
        await StartP2HubAsync(subject);
        Assert.Equal(MessagingPresenceState.Hidden, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
        (await setup.FirstClient.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(setup.Second.UserId, "p2 viewer contact"), JsonOptions)).EnsureSuccessStatusCode();
        Assert.Equal(MessagingPresenceState.Hidden, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
        (await setup.SecondClient.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(setup.First.UserId, "p2 target contact"), JsonOptions)).EnsureSuccessStatusCode();
        Assert.Equal(MessagingPresenceState.Online, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
        (await setup.FirstClient.PostAsync($"/api/v1/messaging/blocks/{setup.Second.UserId}", null)).EnsureSuccessStatusCode();
        var hidden = await P2PresenceAsync(setup.FirstClient, setup.Second.UserId);
        Assert.Equal(MessagingPresenceState.Hidden, hidden.State); Assert.Null(hidden.LastSeenAtUtc);
    }

    [Fact]
    public async Task P2EphemeralOneSessionDisconnectKeepsPresenceOnlineAndLastDisconnectGivesLastSeen()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var other = factory.CreateClient();
        var login = await AuthenticateP1SessionAsync(other, setup.Second.PhoneNumber, factory.Services);
        (await setup.SecondClient.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(setup.First.UserId, "p2 presence"), JsonOptions)).EnsureSuccessStatusCode();
        await using var one = CreateHubConnection(setup.Second.AccessToken); await using var two = CreateHubConnection(login.AccessToken);
        await StartP2HubAsync(one); await StartP2HubAsync(two); await one.StopAsync();
        Assert.Equal(MessagingPresenceState.Online, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
        await two.StopAsync(); var offline = await P2PresenceAsync(setup.FirstClient, setup.Second.UserId);
        Assert.Equal(MessagingPresenceState.Recently, offline.State); Assert.NotNull(offline.LastSeenAtUtc); Assert.Equal(DateTimeKind.Utc, offline.LastSeenAtUtc.Value.Kind);
    }

    [Fact]
    public async Task P2EphemeralAbruptLeaseExpiryDoesNotLeavePermanentOnline()
    {
        var start = DateTime.UtcNow; factory.Clock.SetUtcNow(start);
        try
        {
            using var setup = await CreateAdvancedConversationSetupAsync();
            (await setup.SecondClient.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(setup.First.UserId, "p2 lease"), JsonOptions)).EnsureSuccessStatusCode();
            await using var subject = CreateHubConnection(setup.Second.AccessToken); await StartP2HubAsync(subject);
            Assert.Equal(MessagingPresenceState.Online, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
            factory.Clock.SetUtcNow(start.AddSeconds(46));
            var expired = await P2PresenceAsync(setup.FirstClient, setup.Second.UserId);
            Assert.Equal(MessagingPresenceState.Recently, expired.State); Assert.Equal(start, expired.LastSeenAtUtc);
            await FlushP2EphemeralAsync(); await Assert.ThrowsAsync<HubException>(() => subject.InvokeAsync("Heartbeat"));
            factory.Clock.SetUtcNow(start.AddMinutes(6)); Assert.Equal(MessagingPresenceState.Offline, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task P2EphemeralNobodyPrivacyAndLegacyUpdateDoNotRevealOrResetPresence()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await using var subject = CreateHubConnection(setup.Second.AccessToken); await StartP2HubAsync(subject);
        var privacy = (await setup.SecondClient.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
        Assert.Equal(PrivacyAudience.Contacts, privacy.OnlineAudience); Assert.Equal(PrivacyAudience.Contacts, privacy.LastSeenAudience);
        var changed = await UpdateP2PresencePrivacyAsync(setup.SecondClient, PrivacyAudience.Nobody, PrivacyAudience.Nobody);
        var hidden = await P2PresenceAsync(setup.FirstClient, setup.Second.UserId); Assert.Equal(MessagingPresenceState.Hidden, hidden.State); Assert.Null(hidden.LastSeenAtUtc);
        (await setup.SecondClient.PutAsJsonAsync("/api/v1/messaging/privacy", new {
            changed.DirectMessagesAudience, changed.PhoneNumberVisibilityAudience, changed.PhoneNumberDiscoveryAudience,
            changed.GroupInvitesAudience, ExpectedVersion = changed.Version }, JsonOptions)).EnsureSuccessStatusCode();
        var after = (await setup.SecondClient.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
        Assert.Equal(PrivacyAudience.Nobody, after.OnlineAudience); Assert.Equal(PrivacyAudience.Nobody, after.LastSeenAudience);
        Assert.NotEqual(changed.Version, after.Version);
    }

    [Fact]
    public async Task P2EphemeralOnlineNobodyDoesNotLeakHeartbeatTimestampThroughLastSeen()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await UpdateP2PresencePrivacyAsync(setup.SecondClient, PrivacyAudience.Nobody, PrivacyAudience.Everyone);
        await using var subject = CreateHubConnection(setup.Second.AccessToken); await StartP2HubAsync(subject);
        var result = await P2PresenceAsync(setup.FirstClient, setup.Second.UserId);
        Assert.Equal(MessagingPresenceState.Recently, result.State); Assert.Null(result.LastSeenAtUtc);
        await subject.StopAsync(); Assert.NotNull((await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).LastSeenAtUtc);
    }

    [Fact]
    public async Task P2EphemeralPresenceRealtimeIsReauthorizedAfterBlockAndDoesNotEnumerateStrangers()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var outsider = factory.CreateClient(); await CreateMessagingUserAsync(outsider);
        await UpdateP2PresencePrivacyAsync(setup.SecondClient, PrivacyAudience.Everyone, PrivacyAudience.Everyone);
        Assert.Equal(MessagingPresenceState.Hidden, (await P2PresenceAsync(outsider, setup.Second.UserId)).State);
        Assert.Equal(MessagingPresenceState.Hidden, (await P2PresenceAsync(outsider, Guid.NewGuid())).State);
        await using var viewer = CreateHubConnection(setup.First.AccessToken); await using var subject = CreateHubConnection(setup.Second.AccessToken);
        var received = new TaskCompletionSource<PresenceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<PresenceResult>("PresenceChanged", n => { if (n.UserId == setup.Second.UserId) received.TrySetResult(n); });
        await viewer.StartAsync(); await viewer.InvokeAsync<PresenceResult>("SubscribePresence", setup.Second.UserId);
        await StartP2HubAsync(subject); await FlushP2EphemeralAsync(); Assert.Equal(MessagingPresenceState.Online, (await received.Task.WaitAsync(TimeSpan.FromSeconds(10))).State);
        var control = factory.Services.GetRequiredService<MessagingEphemeralTestControl>(); var before = control.Presence.Count(n => n.Value.UserId == setup.Second.UserId);
        (await setup.FirstClient.PostAsync($"/api/v1/messaging/blocks/{setup.Second.UserId}", null)).EnsureSuccessStatusCode();
        await subject.StopAsync(); await FlushP2EphemeralAsync();
        Assert.Equal(before, control.Presence.Count(n => n.Value.UserId == setup.Second.UserId));
        await Assert.ThrowsAsync<HubException>(() => viewer.InvokeAsync<PresenceResult>("SubscribePresence", setup.Second.UserId));
    }

    [Fact]
    public async Task P2EphemeralRevokedSessionLosesRestTypingHeartbeatAndOnlineContribution()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var survivor = factory.CreateClient();
        await AuthenticateP1SessionAsync(survivor, setup.Second.PhoneNumber, factory.Services);
        var sessions = (await setup.SecondClient.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions", JsonOptions))!;
        await using var subject = CreateHubConnection(setup.Second.AccessToken); await subject.StartAsync(); await subject.InvokeAsync("SubscribeConversation", setup.ConversationId);
        (await survivor.DeleteAsync($"/api/v1/auth/sessions/{sessions.Single(s => s.IsCurrent).Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await setup.SecondClient.GetAsync($"/api/v1/people/{setup.First.UserId}/presence")).StatusCode);
        await AssertP2RevokedHubDeniedAsync(subject, () => subject.InvokeAsync("SetTyping", setup.ConversationId, true));
        await AssertP2RevokedHubDeniedAsync(subject, () => subject.InvokeAsync("Heartbeat"));
        await FlushP2EphemeralAsync();
        Assert.DoesNotContain(factory.Services.GetRequiredService<IMessagingEphemeralState>().Connections(DateTime.UtcNow), c => c.SessionId == sessions.Single(s => s.IsCurrent).Id);
    }

    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task P2EphemeralRemovedMemberCannotPublishOrReceiveTyping(string resource)
    {
        using var ownerClient = factory.CreateClient(); using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var conversation = await CreateManagedAndReadAsync(ownerClient, resource, PersonalCommand("وضعیت گذرای گروه", member.UserId));
        await using var ownerHub = CreateHubConnection(owner.AccessToken); await using var memberHub = CreateHubConnection(member.AccessToken);
        foreach (var hub in new[] { ownerHub, memberHub }) { await StartP2HubAsync(hub); await hub.InvokeAsync("SubscribeConversation", conversation.ConversationId); }
        if (resource == "channels")
            await Assert.ThrowsAsync<HubException>(() => memberHub.InvokeAsync("SetTyping", conversation.ConversationId, true));
        else
            await memberHub.InvokeAsync("SetTyping", conversation.ConversationId, true);
        (await ownerClient.DeleteAsync($"/api/v1/conversations/{conversation.ConversationId}/members/{member.UserId}")).EnsureSuccessStatusCode();
        await Assert.ThrowsAsync<HubException>(() => memberHub.InvokeAsync("SetTyping", conversation.ConversationId, true));
        var control = factory.Services.GetRequiredService<MessagingEphemeralTestControl>(); var before = control.Typing.Count;
        await ownerHub.InvokeAsync("SetTyping", conversation.ConversationId, true);
        Assert.DoesNotContain(control.Typing.Skip(before), n => n.Connections.Contains(memberHub.ConnectionId!));
    }

    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2EphemeralGuardianRelationRevocationHidesPresenceAndRejectsTyping(CommunicationAgeCategory age)
    {
        using var managerClient = factory.CreateClient(); using var childClient = factory.CreateClient(); using var parentClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var child = await CreateMessagingUserAsync(childClient); var parent = await CreateMessagingUserAsync(parentClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, age);
        var school = await CreatePolicySchoolAsync(managerClient, manager, child);
        await factory.CreateMembershipAsync(parent.UserId, school.OrganizationId, OrganizationRole.Parent);
        var relation = await factory.CreateGuardianRelationAsync(parent.UserId, school.OrganizationId, school.PersonId);
        var direct = await StartDirectAndReadAsync(parentClient, child.UserId, ConversationScope.Personal);
        await UpdateP2PresencePrivacyAsync(childClient, PrivacyAudience.Everyone, PrivacyAudience.Everyone);
        await using var parentHub = CreateHubConnection(parent.AccessToken); await using var childHub = CreateHubConnection(child.AccessToken);
        await StartP2HubAsync(childHub); await StartP2HubAsync(parentHub); await parentHub.InvokeAsync("SubscribeConversation", direct.ConversationId);
        Assert.Equal(MessagingPresenceState.Online, (await P2PresenceAsync(parentClient, child.UserId)).State);
        await parentHub.InvokeAsync("SetTyping", direct.ConversationId, true);
        await factory.RevokeGuardianRelationAsync(relation);
        Assert.Equal(MessagingPresenceState.Hidden, (await P2PresenceAsync(parentClient, child.UserId)).State);
        await Assert.ThrowsAsync<HubException>(() => parentHub.InvokeAsync("SetTyping", direct.ConversationId, true));
        Assert.Equal(HttpStatusCode.OK, (await parentClient.GetAsync($"/api/v1/conversations/{direct.ConversationId}/messages")).StatusCode);
    }

    [Fact]
    public async Task P2EphemeralUnwatchRemovesOnlyCurrentConnectionAndPresenceRequiresAuthentication()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await UpdateP2PresencePrivacyAsync(setup.SecondClient, PrivacyAudience.Everyone, PrivacyAudience.Everyone);
        await using var viewer = CreateHubConnection(setup.First.AccessToken); await StartP2HubAsync(viewer);
        await viewer.InvokeAsync<PresenceResult>("SubscribePresence", setup.Second.UserId);
        var state = factory.Services.GetRequiredService<IMessagingEphemeralState>();
        Assert.Contains(state.PresenceObservers(setup.Second.UserId, DateTime.UtcNow), c => c.ConnectionId == viewer.ConnectionId);
        await viewer.InvokeAsync("UnsubscribePresence", setup.Second.UserId);
        Assert.DoesNotContain(state.PresenceObservers(setup.Second.UserId, DateTime.UtcNow), c => c.ConnectionId == viewer.ConnectionId);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/people/{setup.Second.UserId}/presence")).StatusCode);
    }

    [Fact]
    public async Task P2EphemeralHiddenOnlineDoesNotBroadcastActivityTimingThroughRecentlyHints()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await UpdateP2PresencePrivacyAsync(setup.SecondClient, PrivacyAudience.Nobody, PrivacyAudience.Everyone);
        await using var viewer = CreateHubConnection(setup.First.AccessToken); await StartP2HubAsync(viewer);
        await viewer.InvokeAsync<PresenceResult>("SubscribePresence", setup.Second.UserId);
        var control = factory.Services.GetRequiredService<MessagingEphemeralTestControl>();
        var before = control.Presence.Count(n => n.Value.UserId == setup.Second.UserId);
        await using var subject = CreateHubConnection(setup.Second.AccessToken); await StartP2HubAsync(subject);
        Assert.Equal(before, control.Presence.Count(n => n.Value.UserId == setup.Second.UserId));
        await subject.StopAsync(); await FlushP2EphemeralAsync();
        var stopped = Assert.Single(control.Presence.Where(n => n.Value.UserId == setup.Second.UserId).Skip(before));
        Assert.Equal(MessagingPresenceState.Recently, stopped.Value.State); Assert.NotNull(stopped.Value.LastSeenAtUtc);
    }

    [Fact]
    public async Task P2EphemeralHubBudgetRejectsFloodAndHttpPresenceUsesDiscoveryBudget()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await using var hub = CreateHubConnection(user.AccessToken); await StartP2HubAsync(hub);
        for (var index = 1; index < 120; index++) await hub.InvokeAsync("Heartbeat");
        var rejected = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("Heartbeat"));
        Assert.Contains(MessagingErrorCodes.ActivityRateLimited, rejected.Message);
        Assert.Equal("messaging-discovery", typeof(PeoplePresenceController).GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        // محیط Testing سقف policyها را افزایش می‌دهد؛ برای بررسی قطعی 429 از همان الگوی P1 استفاده می‌کنیم.
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<RateLimiterOptions>(options => options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api/v1/people")
                    ? RateLimitPartition.GetFixedWindowLimiter("p2-presence", _ => new FixedWindowRateLimiterOptions
                        { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("other")))));
        using var limitedClient = host.CreateClient(); await AuthenticateP1SessionAsync(limitedClient, user.PhoneNumber, host.Services);
        Assert.Equal(HttpStatusCode.OK, (await limitedClient.GetAsync($"/api/v1/people/{user.UserId}/presence")).StatusCode);
        var limited = await limitedClient.GetAsync($"/api/v1/people/{user.UserId}/presence");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(MessagingErrorCodes.DiscoveryRateLimited, await ReadProblemCodeAsync(limited));
    }

    private async Task FlushP2EphemeralAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MessagingEphemeralService>().FlushAsync(default);
    }

    private static async Task StartP2HubAsync(HubConnection connection)
    {
        await connection.StartAsync();
        // StartAsync به‌تنهایی پایان OnConnectedAsync سمت سرور را تضمین نمی‌کند.
        await connection.InvokeAsync("Heartbeat");
    }

    private static async Task AssertP2RevokedHubDeniedAsync(HubConnection connection, Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.NotNull(exception);
        if (exception is HttpRequestException http) Assert.Equal(HttpStatusCode.Unauthorized, http.StatusCode);
        else if (exception is HubException) Assert.Contains(IdentityErrorCodes.AuthRequired, exception.Message);
        else { Assert.IsType<InvalidOperationException>(exception); Assert.NotEqual(HubConnectionState.Connected, connection.State); }
    }

    private static Task<TypingNotification> ReadP2TypingAsync(Channel<TypingNotification> channel) => channel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    private static async Task<PresenceResult> P2PresenceAsync(HttpClient client, Guid userId) =>
        (await client.GetFromJsonAsync<PresenceResult>($"/api/v1/people/{userId}/presence", JsonOptions))!;

    private static async Task<MessagingPrivacySettingsResult> UpdateP2PresencePrivacyAsync(HttpClient client, PrivacyAudience online, PrivacyAudience lastSeen)
    {
        var current = (await client.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
        using var response = await client.PutAsJsonAsync("/api/v1/messaging/privacy", new UpdateMessagingPrivacyCommand(current.DirectMessagesAudience,
            current.PhoneNumberVisibilityAudience, current.PhoneNumberDiscoveryAudience, current.GroupInvitesAudience, current.Version, online, lastSeen), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<MessagingPrivacySettingsResult>(JsonOptions))!;
    }
}
