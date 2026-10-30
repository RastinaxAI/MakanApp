using System.Net.Http.Json;
using System.Threading.Channels;
using MakanApp.Application.Messaging;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountRealtimeLiveContextRevisionAndDuplicateOldEventCannotRollBackAuthoritativeInbox()
    {
        using var sender = factory.CreateClient(); using var receiver = factory.CreateClient();
        _ = await CreateMessagingUserAsync(sender); var owner = await CreateMessagingUserAsync(receiver);
        var context = await AccountChangesAsync(receiver);
        await using var live = CreateHubConnection(owner.AccessToken);
        var received = Channel.CreateBounded<MessagingRealtimeNotification>(32);
        live.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged,
            e => { if (e.Type == MessagingChangeType.MessageCreated) received.Writer.TryWrite(e); });
        await live.StartAsync();
        var conversation = await StartDirectAndReadAsync(sender, owner.UserId, ConversationScope.Personal);
        var first = await SendMessageAndReadAsync(sender, conversation.ConversationId, Guid.NewGuid(), "پیام قدیمی");
        var earlier = await NextAsync(first.MessageId);
        Assert.Equal(context.ContextRevision, earlier.ContextRevision);
        var latest = await SendMessageAndReadAsync(sender, conversation.ConversationId, Guid.NewGuid(), "پیام جدید");
        _ = await NextAsync(latest.MessageId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<IMessagingRealtimeNotifier>();
            await notifier.NotifyConversationChangedAsync(earlier, owner.UserId, default);
            await notifier.NotifyConversationChangedAsync(earlier, owner.UserId, default);
        }
        _ = await NextAsync(first.MessageId); _ = await NextAsync(first.MessageId);
        var current = (await receiver.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{conversation.ConversationId}/summary", JsonOptions))!;
        Assert.Equal("پیام جدید", current.LastMessagePreview); Assert.Equal(2, current.UnreadCount);
        var replay = await DrainAccountAsync(receiver, context.NextCursor);
        Assert.Equal(2, replay.Values.Count(x => x.Conversation?.Type == MessagingChangeType.MessageCreated));
        Assert.All(replay.Values.Where(x => x.Conversation is not null), x => Assert.Equal(context.ContextRevision, x.Conversation!.ContextRevision));
        var org = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(owner.UserId, org, OrganizationRole.Manager);
        await SelectOrganizationAsync(receiver, membership.MembershipId, OrganizationRole.Manager);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var sessionId = await db.UserSessions.Where(s => s.UserId == owner.UserId).Select(s => s.Id).SingleAsync();
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IRealtimeSessionScope>().AuthorizeConversationAsync(sessionId, earlier, default));
        }
        Assert.NotEqual(context.ContextRevision, (await AccountChangesAsync(receiver)).ContextRevision);

        async Task<MessagingRealtimeNotification> NextAsync(Guid id)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true) { var item = await received.Reader.ReadAsync(timeout.Token); if (item.ResourceId == id) return item; }
        }
    }
}
