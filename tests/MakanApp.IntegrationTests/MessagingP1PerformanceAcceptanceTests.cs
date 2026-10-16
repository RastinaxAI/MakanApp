using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Notification;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task ReconnectBurstRecoversPagedDeltaAndDuplicateRetryWithoutNewLogicalEffects()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var baseline = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        var connections = Enumerable.Range(0, 3).Select(_ => CreateHubConnection(setup.Second.AccessToken)).ToArray();
        try
        {
            await Task.WhenAll(connections.Select(async connection =>
            { await connection.StartAsync(); await connection.InvokeAsync("SubscribeConversation", setup.ConversationId); }));
            await Task.WhenAll(connections.Select(connection => connection.StopAsync()));
            var key = Guid.NewGuid();
            var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendMessageAsync(setup.FirstClient, setup.ConversationId, key, "p1-offline-duplicate")));
            var duplicateIds = new List<Guid>();
            foreach (var response in replies)
            {
                using (response) { response.EnsureSuccessStatusCode(); duplicateIds.Add((await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!.MessageId); }
            }
            Assert.Single(duplicateIds.Distinct());
            var expectedIds = new List<Guid> { duplicateIds[0] };
            for (var index = 0; index < 4; index++)
                expectedIds.Add((await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), $"p1-offline-{index}")).MessageId);
            await Task.WhenAll(connections.Select(async connection =>
            { await connection.StartAsync(); await connection.InvokeAsync("SubscribeConversation", setup.ConversationId); }));
            var checkpoint = baseline.NextCursor; var changes = new List<ConversationChangeResult>();
            for (var pageIndex = 0; pageIndex < 10; pageIndex++)
            {
                var page = await GetChangesAsync(setup.SecondClient, setup.ConversationId, checkpoint, 2);
                changes.AddRange(page.Changes);
                // checkpoint فقط پس از پردازش همان صفحه جلو می‌رود.
                checkpoint = page.NextCursor;
                if (!page.HasMore) break;
                Assert.True(pageIndex < 9);
            }
            Assert.Equal(5, changes.Count); Assert.Equal(5, changes.Select(change => change.ChangeId).Distinct().Count());
            Assert.True(expectedIds.ToHashSet().SetEquals(changes.Select(change => change.ResourceId)));
            var head = (await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
                $"/api/v1/conversations/{setup.ConversationId}/messages?limit=2", JsonOptions))!;
            var seenMessages = head.Messages.Select(message => message.MessageId).ToList();
            var arriving = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-arrived-between-pages");
            var before = head.NextBeforeSequence;
            for (var pageIndex = 0; before.HasValue && pageIndex < 10; pageIndex++)
            {
                var page = (await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
                    $"/api/v1/conversations/{setup.ConversationId}/messages?limit=2&beforeSequence={before}", JsonOptions))!;
                seenMessages.AddRange(page.Messages.Select(message => message.MessageId)); before = page.NextBeforeSequence;
            }
            Assert.Null(before); Assert.Equal(5, seenMessages.Distinct().Count()); Assert.Equal(5, seenMessages.Count);
            Assert.True(expectedIds.ToHashSet().SetEquals(seenMessages)); Assert.DoesNotContain(arriving.MessageId, seenMessages);
            Assert.Equal(arriving.MessageId, Assert.Single((await GetChangesAsync(setup.SecondClient, setup.ConversationId, checkpoint)).Changes).ResourceId);
            await factory.DrainNotificationOutboxAsync();
            Assert.Equal(1, await factory.CountNotificationsAsync(duplicateIds[0], setup.Second.UserId));
        }
        finally { foreach (var connection in connections) await connection.DisposeAsync(); }
    }

    [Fact]
    public async Task BoundedP1SmokeTraverses105ConversationsAndMultiPageHistoryAndSearch()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var frozen = DateTime.UtcNow.AddMinutes(2); factory.Clock.SetUtcNow(frozen);
        try
        {
            var ids = new List<Guid>();
            for (var index = 0; index < 105; index++)
                ids.Add((await CreateManagedAndReadAsync(client, "groups", PersonalCommand($"p1-smoke-{index}"))).ConversationId);
            var first = await ReadPagesAsync(); var repeated = await ReadPagesAsync();
            Assert.Equal(first, repeated); Assert.Equal(105, first.Count); Assert.Equal(105, first.Distinct().Count());
            Assert.True(ids.ToHashSet().SetEquals(first));
            var expected = new List<Guid>();
            for (var index = 0; index < 41; index++)
                expected.Add((await SendMessageAndReadAsync(client, ids[0], Guid.NewGuid(), $"p1-smoke-search {index}")).MessageId);
            var messages = new List<Guid>(); long? before = null;
            for (var pageIndex = 0; pageIndex < 10; pageIndex++)
            {
                var page = (await client.GetFromJsonAsync<ConversationMessagePageResult>(
                    $"/api/v1/conversations/{ids[0]}/messages?limit=7" + (before.HasValue ? $"&beforeSequence={before}" : ""), JsonOptions))!;
                messages.AddRange(page.Messages.Select(message => message.MessageId)); before = page.NextBeforeSequence;
                if (!before.HasValue) break;
                Assert.True(pageIndex < 9);
            }
            Assert.Equal(41, messages.Count); Assert.Equal(41, messages.Distinct().Count()); Assert.True(expected.ToHashSet().SetEquals(messages));
            var found = new List<Guid>(); string? cursor = null;
            for (var pageIndex = 0; pageIndex < 10; pageIndex++)
            {
                var page = (await client.GetFromJsonAsync<MessageSearchPageResult>(
                    $"/api/v1/messages/search?q=p1-smoke-search&conversationId={ids[0]}&limit=7" +
                    (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}"), JsonOptions))!;
                Assert.Equal(41, page.TotalCount); found.AddRange(page.Items.Select(item => item.MessageId)); cursor = page.NextCursor;
                if (cursor is null) break;
                Assert.True(pageIndex < 9);
            }
            Assert.Equal(41, found.Count); Assert.Equal(41, found.Distinct().Count()); Assert.True(expected.ToHashSet().SetEquals(found));
            factory.Clock.SetUtcNow(frozen.AddSeconds(1));
            await SendMessageAndReadAsync(client, first[^1], Guid.NewGuid(), "p1-smoke-new-activity");
            var refreshed = await ReadPagesAsync(); Assert.Equal(first[^1], refreshed[0]);
            Assert.Equal(105, refreshed.Distinct().Count()); Assert.True(ids.ToHashSet().SetEquals(refreshed));
        }
        finally { factory.Clock.UseSystemTime(); }

        async Task<List<Guid>> ReadPagesAsync()
        {
            var result = new List<Guid>();
            for (var page = 1; page <= 6; page++)
            {
                var rows = (await client.GetFromJsonAsync<ConversationSummaryResult[]>($"/api/v1/conversations?page={page}&pageSize=25", JsonOptions))!;
                if (page == 6) Assert.Empty(rows);
                result.AddRange(rows.Select(row => row.ConversationId));
            }
            return result;
        }
    }

    [Fact]
    public async Task BoundedNotificationBatchAndDuplicateSendRaceKeepOneLogicalNotification()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        _ = await RegisterP1NotificationDeviceAsync(setup.SecondClient);
        var key = Guid.NewGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            SendMessageAsync(setup.FirstClient, setup.ConversationId, key, "p1-queue-duplicate")));
        var raceIds = new List<Guid>();
        foreach (var response in responses)
        {
            using (response) { response.EnsureSuccessStatusCode(); raceIds.Add((await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!.MessageId); }
        }
        Assert.Single(raceIds.Distinct());
        var messageIds = new List<Guid> { raceIds[0] };
        for (var index = 0; index < 17; index++)
            messageIds.Add((await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), $"p1-queue-{index}")).MessageId);
        await factory.DrainNotificationOutboxAsync();
        var notificationIds = new List<Guid>();
        foreach (var messageId in messageIds) notificationIds.Add(await factory.GetNotificationIdAsync(messageId, setup.Second.UserId));
        Assert.Equal(18, notificationIds.Distinct().Count()); Assert.Equal(1, await factory.CountNotificationsAsync(raceIds[0], setup.Second.UserId));
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<NotificationDeliveryOptions>();
            services.AddSingleton(new NotificationDeliveryOptions { BatchSize = 4, ClaimLease = TimeSpan.FromSeconds(3), ProviderTimeout = TimeSpan.FromSeconds(1) });
        }));
        var control = host.Services.GetRequiredService<NotificationDeliveryProviderTestControl>(); control.Reset();
        var batches = 0; var complete = false;
        for (; batches < 100; batches++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var processed = await scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>().DispatchBatchAsync(default);
            Assert.InRange(processed, 0, 4);
            var states = new List<NotificationDeliveryStatus>();
            foreach (var id in notificationIds) states.Add((await P1PushAttemptAsync(id)).Status);
            complete = states.All(status => status == NotificationDeliveryStatus.Delivered);
            if (complete) break;
            Assert.True(processed > 0);
        }
        Assert.True(complete); Assert.InRange(batches + 1, 5, 100);
        foreach (var id in notificationIds) Assert.Single(control.PushDeliveries, delivery => delivery.NotificationId == id);
        Assert.Equal(18, (await factory.GetMessagesAsync(setup.ConversationId)).Count());
    }
}
