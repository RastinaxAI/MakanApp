using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2AcceptanceSearchSenderUtcWindowAndMediaFiltersStayWithinVisibleScope()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        static string Utc(DateTime value) => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
        try
        {
            await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p2-date-filter");
            factory.Clock.SetUtcNow(now.AddMinutes(1)); var second = await SendMessageAndReadAsync(setup.SecondClient, setup.ConversationId, Guid.NewGuid(), "p2-date-filter");
            factory.Clock.SetUtcNow(now.AddMinutes(2)); var file = await UploadFileAsync(setup.FirstClient, "image/png", "p2-date.png");
            var media = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, Text = "p2-date-filter", AttachmentIds = [file.Id] });
            var query = $"q=p2-date-filter&conversationId={setup.ConversationId}";
            var window = await P2SearchAsync(setup.FirstClient, query + $"&senderUserId={setup.Second.UserId}&fromUtc={Utc(now.AddSeconds(30))}&toUtc={Utc(now.AddSeconds(90))}");
            Assert.Equal(1, window.TotalCount); Assert.Equal(second.MessageId, Assert.Single(window.Items).MessageId);
            Assert.Equal(media.MessageId, Assert.Single((await P2SearchAsync(setup.SecondClient, query + $"&kind=Image&hasAttachment=true&senderUserId={setup.First.UserId}")).Items).MessageId);
            Assert.Equal(0, (await P2SearchAsync(setup.FirstClient, query + $"&fromUtc={Utc(now.AddMinutes(3))}")).TotalCount);
            Assert.Equal(HttpStatusCode.BadRequest, (await setup.FirstClient.GetAsync("/api/v1/messages/search?" + query + $"&fromUtc={Utc(now)}&toUtc={Utc(now)}")).StatusCode);
        }
        finally { factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2AcceptanceScheduledBatchIsBoundedAndDrainsDurableWorkOnce()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            for (var index = 0; index < 21; index++) await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "صف محدود " + index }));
            factory.Clock.SetUtcNow(now.AddMinutes(2));
            for (var batch = 0; batch < 4 && (await factory.GetMessagesAsync(setup.ConversationId)).Count() < 21; batch++) Assert.InRange(await DispatchP2ScheduledAsync(), 0, 20);
            var messages = (await factory.GetMessagesAsync(setup.ConversationId)).ToArray(); Assert.Equal(21, messages.Length); Assert.Equal(21, messages.Select(m => m.ClientMessageId).Distinct().Count());
            await DispatchP2ScheduledAsync(); Assert.Equal(21, (await factory.GetMessagesAsync(setup.ConversationId)).Count());
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.Equal(21, await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.CountAsync(s => s.ConversationId == setup.ConversationId && s.Status == ScheduledMessageStatus.Dispatched));
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2AcceptanceTopicHistoryAndSearchStayFilteredAcrossBoundedPages()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var topic = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "صفحه‌بندی موضوع"));
        for (var index = 0; index < 35; index++) (await SendP2TopicAsync(setup.OwnerClient, topic, Guid.NewGuid(), "p2-topic-pages " + index)).EnsureSuccessStatusCode();
        await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "p2-topic-pages خارج موضوع");
        var ids = new List<Guid>(); long? before = null;
        do
        {
            var route = $"/api/v1/conversations/{id}/messages?topicId={topic.TopicId}&limit=10" + (before.HasValue ? "&beforeSequence=" + before : "");
            var page = (await setup.TargetClient.GetFromJsonAsync<ConversationMessagePageResult>(route, JsonOptions))!;
            Assert.InRange(page.Messages.Count, 1, 10); Assert.All(page.Messages, m => Assert.Equal(topic.TopicId, m.TopicId));
            ids.AddRange(page.Messages.Select(m => m.MessageId)); before = page.NextBeforeSequence;
            Assert.True(ids.Count <= 35, "cursor باید پیش برود؛ این فقط smoke محدود محلی است.");
        } while (before.HasValue);
        Assert.Equal(35, ids.Distinct().Count());
        var matches = new List<Guid>(); string? cursor = null;
        do
        {
            var page = await P2SearchAsync(setup.TargetClient, $"q=p2-topic-pages&topicId={topic.TopicId}&limit=10" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            Assert.Equal(35, page.TotalCount); Assert.InRange(page.Items.Count, 1, 10); matches.AddRange(page.Items.Select(m => m.MessageId)); cursor = page.NextCursor;
            Assert.True(matches.Count <= 35);
        } while (cursor is not null);
        Assert.Equal(ids.Order(), matches.Order());
    }
    [Fact]
    public async Task P2AcceptanceNineConnectionsAndTypingBurstKeepPresenceAndSqlHistoryBounded()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var thirdClient = factory.CreateClient(); using var fourthClient = factory.CreateClient();
        var third = await CreateMessagingUserAsync(thirdClient); var fourth = await CreateMessagingUserAsync(fourthClient);
        var thirdDirect = await StartDirectAndReadAsync(setup.FirstClient, third.UserId, ConversationScope.Personal);
        var fourthDirect = await StartDirectAndReadAsync(setup.FirstClient, fourth.UserId, ConversationScope.Personal);
        var targets = new[] { (User: setup.Second, Client: setup.SecondClient, ConversationId: setup.ConversationId), (User: third, Client: thirdClient, ConversationId: thirdDirect.ConversationId), (User: fourth, Client: fourthClient, ConversationId: fourthDirect.ConversationId) };
        var clients = new List<HttpClient>(); var hubs = new List<(HubConnection Hub, Guid UserId, Guid ConversationId)>();
        try
        {
            foreach (var target in targets)
            {
                (await target.Client.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(setup.First.UserId, "حضور مجاز"), JsonOptions)).EnsureSuccessStatusCode();
                var tokens = new List<string> { target.User.AccessToken };
                for (var device = 0; device < 2; device++) { var client = factory.CreateClient(); clients.Add(client); tokens.Add((await AuthenticateP1SessionAsync(client, target.User.PhoneNumber, factory.Services)).AccessToken); }
                foreach (var token in tokens)
                {
                    var hub = CreateHubConnection(token); hubs.Add((hub, target.User.UserId, target.ConversationId)); await StartP2HubAsync(hub); await hub.InvokeAsync("SubscribeConversation", target.ConversationId);
                }
            }
            for (var round = 0; round < 8; round++) foreach (var source in hubs) { await source.Hub.InvokeAsync("SetTyping", source.ConversationId, true); await source.Hub.InvokeAsync("SetTyping", source.ConversationId, false); }
            foreach (var source in hubs) await source.Hub.InvokeAsync("Heartbeat");
            foreach (var target in targets) { Assert.Equal(MessagingPresenceState.Online, (await P2PresenceAsync(setup.FirstClient, target.User.UserId)).State); Assert.Empty(await factory.GetMessagesAsync(target.ConversationId)); }
            var first = hubs.Where(h => h.UserId == setup.Second.UserId).ToArray(); await first[0].Hub.StopAsync(); await first[1].Hub.StopAsync();
            Assert.Equal(MessagingPresenceState.Online, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
            await first[2].Hub.StopAsync(); Assert.Equal(MessagingPresenceState.Recently, (await P2PresenceAsync(setup.FirstClient, setup.Second.UserId)).State);
        }
        finally { foreach (var source in hubs) await source.Hub.DisposeAsync(); foreach (var client in clients) client.Dispose(); }
    }
}
