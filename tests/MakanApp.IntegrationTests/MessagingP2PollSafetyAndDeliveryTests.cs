using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2QuizNeverQueriesAnswerKeyForUnansweredReaderEvenAfterClose()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, P2Poll(quiz: true));
        var counter = new P2QuizKeyReadCounter();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.AddDbContext<MakanDbContext>((_, options) => options.AddInterceptors(counter));
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken);
        var opened = await ReadP2PollAsync(client, setup.ConversationId, receipt.MessageId); Assert.Null(opened.CorrectOptionNumber); Assert.Equal(0, counter.Reads);
        await CloseP2PollAsync(client, opened); Assert.Null((await ReadP2PollAsync(client, setup.ConversationId, receipt.MessageId)).CorrectOptionNumber); Assert.Equal(0, counter.Reads);
        var next = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, P2Poll(quiz: true));
        Assert.Equal(2, (await VoteP2PollAsync(client, setup.ConversationId, next.MessageId, [1])).CorrectOptionNumber); Assert.Equal(1, counter.Reads);
    }
    private sealed class P2QuizKeyReadCounter : DbCommandInterceptor
    {
        private int _reads; public int Reads => Volatile.Read(ref _reads);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            // فقط شمارندهٔ جدول هدف؛ هیچ متن SQL یا پارامتر حساس ثبت نمی‌شود.
            if (command.CommandText.Contains("FROM [messaging].[ConversationQuizAnswers]", StringComparison.Ordinal)) Interlocked.Increment(ref _reads);
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task P2PollOutboxFailureRollsBackWholeDefinitionBeforeSafeRetry()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Poll(quiz: true);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.AddSingleton<P2ScheduledOutboxControl>(); services.AddScoped<EfOutbox>(); services.RemoveAll<IOutbox>();
            services.AddScoped<IOutbox>(p => new P2FailingScheduledOutbox(p.GetRequiredService<EfOutbox>(), p.GetRequiredService<P2ScheduledOutboxControl>()));
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken);
        var failure = await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/polls", command, JsonOptions); Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await using (var scope = factory.Services.CreateAsyncScope())
        { var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); Assert.False(await db.ConversationPolls.AnyAsync(p => p.ConversationId == setup.ConversationId)); }
        var success = await CreateP2PollAsync(client, setup.ConversationId, command); var retry = await CreateP2PollAsync(client, setup.ConversationId, command);
        Assert.Equal(success, retry); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        await factory.DrainNotificationOutboxAsync(); Assert.Equal(1, await factory.CountNotificationsAsync(success.MessageId, setup.Second.UserId));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P2PollReportsPreserveOptionsWithoutQuizKeyAndContactCardKindIsSupported(bool contactCard)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var subjectClient = factory.CreateClient(); var subject = await CreateMessagingUserAsync(subjectClient);
        var command = P2Poll(quiz: true);
        var receipt = contactCard
            ? await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.ContactCard, ContactUserId = subject.UserId })
            : await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command);
        var report = new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Other, "شاهد محدود به همان پیام");
        var response = await setup.SecondClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/reports", report, JsonOptions); response.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var evidence = await db.AbuseReports.SingleAsync(r => r.ClientReportId == report.ClientReportId);
        Assert.Equal(contactCard ? MessageKind.ContactCard : MessageKind.Poll, evidence.ReportedMessageKind);
        Assert.True(await db.ModerationCases.AnyAsync(c => c.AbuseReportId == evidence.Id));
        if (!contactCard) { Assert.All(command.Options, o => Assert.Contains(o, evidence.ReportedContentSnapshot)); Assert.DoesNotContain(command.Explanation!, evidence.ReportedContentSnapshot!); }
    }
    [Fact]
    public async Task P2PollRealInternalSafetyInspectsOptionContentRatherThanOnlyQuestion()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor); await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IContentSafetyProvider>(); services.AddSingleton<IContentSafetyProvider, InternalMakanContentSafetyProvider>();
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken);
        var response = await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/polls", P2Poll() with { Options = ["گزینهٔ عادی", "می کشمت"] }, JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var incident = await db.SafetyIncidents.SingleAsync(i => i.ConversationId == setup.ConversationId); Assert.Contains("می کشمت", incident.ContentSnapshot);
        Assert.True(await db.ModerationCases.AnyAsync(c => c.SafetyIncidentId == incident.Id));
    }
    [Fact]
    public async Task P2PollAdjacentRealContactCardSafetyReviewPersistsInsteadOfConstraintFailure()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var subjectClient = factory.CreateClient(); var subject = await CreateMessagingUserAsync(subjectClient);
        await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId); await factory.AllowPersonalGroupInviteAsync(subject.UserId, setup.Second.UserId);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IContentSafetyProvider>(); services.AddSingleton<IContentSafetyProvider, InternalMakanContentSafetyProvider>();
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken); var operation = Guid.NewGuid();
        var response = await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", new SendMessageCommand { ClientMessageId = operation, Kind = MessageKind.ContactCard, ContactUserId = subject.UserId }, JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); Assert.Equal(MessagingErrorCodes.MessageSafetyReviewRequired, await ReadProblemCodeAsync(response));
        var incident = await factory.GetSafetyIncidentAsync(setup.ConversationId, setup.First.UserId, operation); Assert.NotNull(incident); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await using var scope = factory.Services.CreateAsyncScope(); Assert.Equal(MessageKind.ContactCard, (await scope.ServiceProvider.GetRequiredService<MakanDbContext>().SafetyIncidents.SingleAsync(i => i.Id == incident.Id)).ContentKind);
    }
    [Fact]
    public async Task P2PollRealtimeInvalidatesAuthorizedViewerAndVoteDoesNotCreateMessageOrNotification()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await factory.DrainNotificationOutboxAsync(); var baseline = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        await using var connection = CreateHubConnection(setup.Second.AccessToken); var arrived = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n => { if (n.Type == MessagingChangeType.PollChanged && n.ResourceId == receipt.MessageId) arrived.TrySetResult(n); });
        await StartP2HubAsync(connection); await connection.InvokeAsync("SubscribeConversation", setup.ConversationId);
        await VoteP2PollAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId, [1]);
        var notification = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(receipt.MessageId, notification.ResourceId);
        var changes = await GetChangesAsync(setup.SecondClient, setup.ConversationId, baseline.NextCursor);
        Assert.Single(changes.Changes, c => c.Type == MessagingChangeType.PollChanged); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        await factory.DrainNotificationOutboxAsync(); Assert.Equal(1, await factory.CountNotificationsAsync(receipt.MessageId, setup.Second.UserId));
        Assert.DoesNotContain("correctOption", System.Text.Json.JsonSerializer.Serialize(notification, JsonOptions), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task P2PollDeletedForEveryoneKeepsSqlVotesButHidesPollAndVoteDelta()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId, [1]);
        (await setup.FirstClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(receipt.Version)}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.GetAsync(P2PollRoute(setup.ConversationId, receipt.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.SecondClient, setup.ConversationId)).Changes, c => c.Type == MessagingChangeType.PollChanged);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationPollVotes.AnyAsync(v => v.PollId == receipt.MessageId));
    }
}
