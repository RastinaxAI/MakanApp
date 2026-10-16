using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2PollSqlClosedStateRequiresTimestampRatherThanAcceptingUnknownCheck()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        // SQL CHECK مقدار UNKNOWN را می‌پذیرد؛ اعتبارسنجی NULL باید صریح و در خود constraint باشد.
        var failure = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [messaging].[ConversationPolls] SET [Status]=2,[ClosedByUserId]={setup.First.UserId},[ClosedAtUtc]=NULL WHERE [Id]={receipt.MessageId}"));
        Assert.Equal(547, failure.Number); Assert.Contains("CK_ConversationPolls_State", failure.Message, StringComparison.Ordinal);
        Assert.Equal(ConversationPollStatus.Open, (await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId)).Status);
    }
    [Fact]
    public async Task P2PollSqlQuizKeyCannotBeAttachedToOrdinaryPoll()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var definition = ConversationPollDefinition.Create("پرسش نمونه", ["گزینهٔ اول", "گزینهٔ دوم"], false, true, true, 1, null);
        db.ConversationQuizAnswers.Add(ConversationQuizAnswer.Create(receipt.MessageId, definition));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(547, Assert.IsType<SqlException>(failure.InnerException).Number);
    }
    [Fact]
    public async Task P2PollConcurrentCreationHasOneMessageDefinitionAndNotification()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Poll(quiz: true);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<MessageReceiptResult> Create() { await start.Task; return await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command); }
        var first = Create(); var second = Create(); start.SetResult(); await Task.WhenAll(first, second); Assert.Equal(await first, await second);
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)); await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountNotificationsAsync((await first).MessageId, setup.Second.UserId));
    }
    [Fact]
    public async Task P2PollConcurrentDistinctVotersKeepAggregatesAndOneIdentityBatch()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var clients = new List<HttpClient> { setup.OwnerClient, setup.TargetClient };
        try
        {
            for (var index = 0; index < 10; index++)
            {
                var client = factory.CreateClient(); clients.Add(client); var user = await CreateMessagingUserAsync(client);
                await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, user.UserId);
                (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/members", new AddConversationMemberCommand(user.UserId), JsonOptions)).EnsureSuccessStatusCode();
            }
            var receipt = await CreateP2PollAsync(setup.OwnerClient, id, P2Poll(anonymous: false));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task Vote(HttpClient client) { await start.Task; await VoteP2PollAsync(client, id, receipt.MessageId, [1]); }
            var votes = clients.Select(Vote).ToArray(); start.SetResult(); await Task.WhenAll(votes);
            var current = await ReadP2PollAsync(setup.TargetClient, id, receipt.MessageId); Assert.Equal(12, current.TotalVoters); Assert.Equal(12, current.Options.Single(o => o.Number == 1).VoteCount);
            var counter = new P2IdentityQueryCounter();
            using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
                services.AddDbContext<MakanDbContext>((_, options) => options.AddInterceptors(counter));
            }));
            using var reader = host.CreateClient(); UseBearerToken(reader, setup.Owner.AccessToken);
            var page = (await reader.GetFromJsonAsync<ConversationPollVoterPageResult>(P2PollRoute(id, receipt.MessageId) + "/voters?pageSize=100", JsonOptions))!;
            Assert.Equal(12, page.Items.Count); Assert.False(page.HasMore); Assert.Equal(12, page.Items.Select(v => v.User.UserId).Distinct().Count()); Assert.Equal(1, counter.Reads);
        }
        finally { foreach (var client in clients.Skip(2)) client.Dispose(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P2PollConcurrentSameAccountVotesHaveOneFinalSqlEffect(bool quiz)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, P2Poll(quiz: quiz));
        using var device = factory.CreateClient(); await AuthenticateP1SessionAsync(device, setup.Second.PhoneNumber, factory.Services);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ConversationPollResult> Vote(HttpClient client) { await start.Task; return await VoteP2PollAsync(client, setup.ConversationId, receipt.MessageId, [1]); }
        var first = Vote(setup.SecondClient); var second = Vote(device); start.SetResult(); await Task.WhenAll(first, second);
        Assert.Equal((await first).OwnVoteVersion, (await second).OwnVoteVersion); Assert.Equal(1, (await first).TotalVoters);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Single(await db.ConversationPollVotes.Where(v => v.PollId == receipt.MessageId).ToArrayAsync()); Assert.Single(await db.ConversationPollSelections.Where(s => s.PollId == receipt.MessageId).ToArrayAsync());
        Assert.Single(await db.MessagingChangeEvents.Where(c => c.ResourceId == receipt.MessageId && c.ChangeType == MessagingChangeType.PollChanged).ToArrayAsync());
    }
    [Fact]
    public async Task P2PollConcurrentDifferentDeviceChangesRejectLostUpdate()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        var initial = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId, [1]);
        using var device = factory.CreateClient(); await AuthenticateP1SessionAsync(device, setup.Second.PhoneNumber, factory.Services);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Vote(HttpClient client, int number)
        { await start.Task; return await client.PostAsJsonAsync(P2PollRoute(setup.ConversationId, receipt.MessageId) + "/vote", new VoteConversationPollCommand([number], initial.OwnVoteVersion), JsonOptions); }
        var first = Vote(setup.SecondClient, 2); var second = Vote(device, 3); start.SetResult(); var responses = await Task.WhenAll(first, second);
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.PreconditionFailed); }
        finally { foreach (var response in responses) response.Dispose(); }
        var final = await ReadP2PollAsync(device, setup.ConversationId, receipt.MessageId); Assert.Equal(1, final.TotalVoters); Assert.Single(final.Options, o => o.IsSelected); Assert.Equal(1, final.Options.Sum(o => o.VoteCount));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task P2PollVoteRacingFinalCloseIsTransactionalAndDeterministic(int round)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, P2Poll() with { Question = "رقابت رأی و بستن " + round });
        var initial = await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId); var route = P2PollRoute(setup.ConversationId, receipt.MessageId);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Vote() { await start.Task; return await setup.SecondClient.PostAsJsonAsync(route + "/vote", new VoteConversationPollCommand([1]), JsonOptions); }
        async Task<HttpResponseMessage> Close() { await start.Task; return await setup.FirstClient.PostAsJsonAsync(route + "/close", new CloseConversationPollCommand(initial.Version), JsonOptions); }
        var vote = Vote(); var close = Close(); start.SetResult(); await Task.WhenAll(vote, close);
        using var voteResponse = await vote; using var closeResponse = await close; var current = await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        if (voteResponse.StatusCode == HttpStatusCode.OK)
        { Assert.Equal(HttpStatusCode.PreconditionFailed, closeResponse.StatusCode); Assert.Equal(1, current.TotalVoters); Assert.Equal(ConversationPollStatus.Open, current.Status); current = await CloseP2PollAsync(setup.FirstClient, current); }
        else
        { Assert.Equal(HttpStatusCode.Conflict, voteResponse.StatusCode); Assert.Equal(MessagingErrorCodes.PollClosed, await ReadProblemCodeAsync(voteResponse)); Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode); Assert.Equal(0, current.TotalVoters); }
        Assert.Equal(ConversationPollStatus.Closed, current.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await setup.SecondClient.PostAsJsonAsync(route + "/vote", new VoteConversationPollCommand([2]), JsonOptions)).StatusCode);
    }
    [Fact]
    public async Task P2PollSqlKeysRejectDuplicateVotesAndInvalidOptionSelections()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId, [1]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var poll = await db.ConversationPolls.AsNoTracking().SingleAsync(p => p.Id == receipt.MessageId);
            db.ConversationPollVotes.Add(ConversationPollVote.Create(poll, setup.Second.UserId, DateTime.UtcNow));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Contains(Assert.IsType<SqlException>(duplicate.InnerException).Number, new[] { 2601, 2627 });
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var vote = await db.ConversationPollVotes.AsNoTracking().SingleAsync(v => v.PollId == receipt.MessageId);
            db.ConversationPollSelections.Add(ConversationPollSelection.Create(vote, 10));
            var invalid = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(547, Assert.IsType<SqlException>(invalid.InnerException).Number);
        }
        Assert.Equal(1, (await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId)).TotalVoters);
    }
}
