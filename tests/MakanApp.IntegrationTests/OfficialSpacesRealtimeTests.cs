using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Threading.Channels;
using MakanApp.Application.Messaging;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task OfficialSpacesManagerGetsFirstActivityWithoutOpeningConversationAndReplayHasNoDuplicateEffect()
    {
        using var s = await CreateOfficialScenarioAsync();
        var boundary = await AccountChangesAsync(s.Manager);
        var events = Channel.CreateUnbounded<MessagingRealtimeNotification>();
        await using var connection = CreateHubConnection(s.Manager.DefaultRequestHeaders.Authorization!.Parameter!);
        connection.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged,
            e => { if (e.Type == MessagingChangeType.MessageCreated) events.Writer.TryWrite(e); });
        await connection.StartAsync();
        var publication = await PublishOfficialSourceAsync(s, AcademicActivityKind.Lesson, "بدنهٔ درس در event زنده ارسال نمی‌شود");
        await DeliverOfficialAsync(publication);
        Guid groupMessage; Guid group;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var delivered = await db.AcademicActivityDeliveries.SingleAsync(d => d.PublicationId == publication && d.Purpose == ConversationType.Group);
            groupMessage = delivered.MessageId!.Value; group = delivered.ConversationId!.Value;
        }
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            MessagingRealtimeNotification item;
            do { item = await events.Reader.ReadAsync(timeout.Token); } while (item.ResourceId != groupMessage);
            Assert.Equal(boundary.ContextRevision, item.ContextRevision);
        }
        var inbox = (await s.Manager.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!;
        var summary = Assert.Single(inbox, c => c.ConversationId == group);
        Assert.True(summary.IsOfficial); Assert.Equal(1, summary.UnreadCount); Assert.Equal(1, summary.LastMessageSequence);
        await connection.StopAsync();
        var offline = await PublishOfficialSourceAsync(s, AcademicActivityKind.Assignment, "بدنهٔ تکلیف منتقل نمی‌شود");
        await DeliverOfficialAsync(offline); await DeliverOfficialAsync(offline);
        await connection.StartAsync();
        var replay = await DrainAccountAsync(s.Manager, boundary.NextCursor, 1);
        Assert.Equal(4, replay.Values.Count(c => c.Conversation?.Type == MessagingChangeType.MessageCreated));
        Assert.Equal(replay.Values.Count, replay.Values.Select(c => c.EventId).Distinct().Count());
        var current = (await s.Manager.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{group}/summary", JsonOptions))!;
        Assert.Equal(2, current.UnreadCount); Assert.Equal(2, current.LastMessageSequence);
    }

    [Fact]
    public async Task OfficialSpacesNewMembershipInvalidatesInboxAndEnrollmentRevocationFencesFutureActivity()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        var before = await AccountChangesAsync(s.Student);
        var received = new ConcurrentQueue<RealtimeInvalidation>();
        await using var live = CreateHubConnection(s.Student.DefaultRequestHeaders.Authorization!.Parameter!);
        live.On<RealtimeInvalidation>("ClientInvalidated", received.Enqueue); await live.StartAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(s.OrganizationId, s.ClassId, default));
        await FlushReplayOutboxAsync();
        Assert.Contains(received, e => e.Key == "access");
        Assert.Contains((await DrainAccountAsync(s.Student, before.NextCursor)).Values, e => e.Invalidation?.Key == "access");
        var pair = (await s.Student.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        var inbox = (await s.Student.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", JsonOptions))!;
        Assert.Contains(inbox, c => c.ConversationId == pair.Group.ConversationId);
        var publication = await PublishOfficialSourceAsync(s, AcademicActivityKind.Lesson, "دسترسی باید هنگام تحویل دوباره بررسی شود");
        var boundary = await AccountChangesAsync(s.Student);
        await factory.EndEnrollmentAsync(s.EnrollmentId);
        await DeliverOfficialAsync(publication);
        var replay = await DrainAccountAsync(s.Student, boundary.NextCursor);
        Assert.DoesNotContain(replay.Values, e => e.Conversation?.ConversationId == pair.Group.ConversationId || e.Conversation?.ConversationId == pair.Channel.ConversationId);
        await using var scopeAfter = factory.Services.CreateAsyncScope(); var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<MakanDbContext>();
        var audience = scopeAfter.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>();
        var change = await dbAfter.MessagingChangeEvents.SingleAsync(e => e.ConversationId == pair.Group.ConversationId && e.ChangeType == MessagingChangeType.MessageCreated);
        var notification = new MessagingRealtimeNotification(change.ConversationId, change.ChangeType, change.ResourceId, change.ResourceVersion,
            MessagingChangeCursor.Encode(change.ConversationId, change.ChangeSequence), DateTime.SpecifyKind(change.OccurredAtUtc, DateTimeKind.Utc), 1);
        var allowed = new List<Guid>(); await foreach (var id in audience.GetActiveSessionIdsAsync(notification, null, factory.Clock.GetUtcNow().UtcDateTime, default)) allowed.Add(id);
        var ownSessions = await dbAfter.UserSessions.Where(x => x.UserId == s.StudentId).Select(x => x.Id).ToArrayAsync();
        Assert.DoesNotContain(allowed, ownSessions.Contains);
    }

    [Fact]
    public async Task OfficialSpacesGroupReadDoesNotMarkChannelReadAndMuteRemainsPerConversation()
    {
        using var s = await CreateOfficialScenarioAsync();
        var publication = await PublishOfficialSourceAsync(s, AcademicActivityKind.Lesson, "بدون اعلان خارجی تکراری"); await DeliverOfficialAsync(publication);
        var pair = (await s.Student.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        using var read = await s.Student.PostAsJsonAsync($"/api/v1/conversations/{pair.Group.ConversationId}/read", new AdvanceConversationCursorCommand(1), JsonOptions); read.EnsureSuccessStatusCode();
        var group = (await s.Student.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{pair.Group.ConversationId}/summary", JsonOptions))!;
        var channel = (await s.Student.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{pair.Channel.ConversationId}/summary", JsonOptions))!;
        Assert.Equal(0, group.UnreadCount); Assert.Equal(1, channel.UnreadCount);
        using var mute = await s.Student.PutAsJsonAsync($"/api/v1/conversations/{pair.Group.ConversationId}/mute", new SetConversationMuteCommand(ConversationMuteMode.Permanent, null), JsonOptions);
        mute.EnsureSuccessStatusCode();
        channel = (await s.Student.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{pair.Channel.ConversationId}/summary", JsonOptions))!;
        Assert.False(channel.IsMuted);
    }

    private async Task DeliverOfficialAsync(Guid publication)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOfficialSpacesWorkHandler>().HandleAsync(publication, OfficialSpaceEventWriter.PublicationType, "{}", default);
    }
}
