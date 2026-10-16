using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    private static CreateConversationPollCommand P2Poll(bool anonymous = true, bool quiz = false, bool multiple = false) =>
        new(Guid.NewGuid(), "پرسش نمونه", ["گزینهٔ اول", "گزینهٔ دوم", "گزینهٔ سوم"], multiple, anonymous, quiz, quiz ? 2 : null, quiz ? "توضیح محرمانهٔ پاسخ" : null);
    private static string P2PollRoute(Guid conversationId, Guid messageId) => $"/api/v1/conversations/{conversationId}/messages/{messageId}/poll";
    private static async Task<MessageReceiptResult> CreateP2PollAsync(HttpClient client, Guid conversationId, CreateConversationPollCommand? command = null)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/polls", command ?? P2Poll(), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
    }
    private static async Task<ConversationPollResult> ReadP2PollAsync(HttpClient client, Guid conversationId, Guid messageId) =>
        (await client.GetFromJsonAsync<ConversationPollResult>(P2PollRoute(conversationId, messageId), JsonOptions))!;
    private static async Task<ConversationPollResult> VoteP2PollAsync(HttpClient client, Guid conversationId, Guid messageId, int[] options, string? version = null)
    {
        using var response = await client.PostAsJsonAsync(P2PollRoute(conversationId, messageId) + "/vote", new VoteConversationPollCommand(options, version), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ConversationPollResult>(JsonOptions))!;
    }
    private static async Task<ConversationPollResult> CloseP2PollAsync(HttpClient client, ConversationPollResult poll)
    {
        using var response = await client.PostAsJsonAsync(P2PollRoute(poll.ConversationId, poll.MessageId) + "/close", new CloseConversationPollCommand(poll.Version), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ConversationPollResult>(JsonOptions))!;
    }
    [Fact]
    public async Task P2PollCreationPersistsOneImmutableDefinitionAndExactRetryReceipt()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Poll(anonymous: false);
        var created = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command);
        var retried = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command);
        Assert.Equal(created, retried); Assert.Equal(MessageKind.Poll, created.Kind);
        var poll = await ReadP2PollAsync(setup.SecondClient, setup.ConversationId, created.MessageId);
        Assert.False(poll.IsAnonymous); Assert.Equal(command.Options, poll.Options.Select(o => o.Text)); Assert.Equal(0, poll.TotalVoters);
        foreach (var changed in new[] { command with { Options = ["تعریف متفاوت", "دوم"] }, command with { IsAnonymous = true } })
        {
            var conflict = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/polls", changed, JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode); Assert.Equal(MessagingErrorCodes.MessageIdempotencyConflict, await ReadProblemCodeAsync(conflict));
        }
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.ConversationPolls.CountAsync(p => p.ConversationId == setup.ConversationId)); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
    }
    [Fact]
    public async Task P2PollRequiresExplicitPrivacyAndRejectsInvalidDefinitionsOrGenericSend()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var valid = P2Poll();
        foreach (var invalid in new[] { valid with { IsAnonymous = null }, valid with { Options = ["اول"] }, valid with { Options = ["x", " X "] },
            valid with { IsQuiz = true }, valid with { CorrectOptionNumber = 1 }, valid with { Question = new string('q', 301) },
            valid with { Options = null! }, valid with { Explanation = "پاسخ غیرمجاز" }, P2Poll(quiz: true) with { AllowsMultipleAnswers = true } })
            Assert.Equal(HttpStatusCode.BadRequest, (await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/polls", invalid, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages", new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Poll, Text = "نباید orphan باشد" }, JsonOptions)).StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
    }
    [Fact]
    public async Task P2PollVoteMutationUsesOwnVersionAndExactRetryDoesNotDuplicate()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        var first = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1]);
        var retry = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1]);
        Assert.Equal(first.Version, retry.Version); Assert.Equal(first.OwnVoteVersion, retry.OwnVoteVersion);
        var missing = await setup.SecondClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/vote", new VoteConversationPollCommand([2]), JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, missing.StatusCode);
        var changed = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [2], first.OwnVoteVersion);
        Assert.Equal(1, changed.TotalVoters); Assert.Equal(0, changed.Options.Single(o => o.Number == 1).VoteCount); Assert.Equal(1, changed.Options.Single(o => o.Number == 2).VoteCount);
        var stale = await setup.SecondClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/vote", new VoteConversationPollCommand([3], first.OwnVoteVersion), JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Single(await db.ConversationPollVotes.Where(v => v.PollId == message.MessageId).ToArrayAsync()); Assert.Single(await db.ConversationPollSelections.Where(v => v.PollId == message.MessageId).ToArrayAsync());
    }
    [Fact]
    public async Task P2PollMultiChoiceCountsVotersOnceAndRejectsInvalidSelections()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, P2Poll(multiple: true));
        var vote = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [3, 1]);
        Assert.Equal(1, vote.TotalVoters); Assert.Equal(2, vote.Options.Sum(o => o.VoteCount)); Assert.Equal(new[] { 1, 3 }, vote.Options.Where(o => o.IsSelected).Select(o => o.Number));
        foreach (var options in new[] { Array.Empty<int>(), new[] { 0 }, new[] { 4 }, new[] { 1, 1 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await setup.SecondClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/vote", new VoteConversationPollCommand(options, vote.OwnVoteVersion), JsonOptions)).StatusCode);
    }
    [Fact]
    public async Task P2AnonymousPollNeverExposesVoterIdentitiesEvenToCreator()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1]);
        foreach (var client in new[] { setup.FirstClient, setup.SecondClient })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/voters")).StatusCode);
            var wire = await client.GetStringAsync(P2PollRoute(setup.ConversationId, message.MessageId));
            Assert.DoesNotContain(setup.Second.UserId.ToString(), wire, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(setup.Second.PhoneNumber, wire);
        }
        var changes = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        Assert.All(changes.Changes.Where(c => c.Type == MessagingChangeType.PollChanged), c => Assert.Null(c.ActorUserId));
        Assert.Single(changes.Changes, c => c.Type == MessagingChangeType.PollChanged);
    }
    [Fact]
    public async Task P2NonAnonymousVotersArePagedSafeAndVisibleOnlyToCurrentMembers()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var message = await CreateP2PollAsync(setup.OwnerClient, id, P2Poll(anonymous: false));
        await VoteP2PollAsync(setup.OwnerClient, id, message.MessageId, [1]); await VoteP2PollAsync(setup.TargetClient, id, message.MessageId, [2]);
        var route = P2PollRoute(id, message.MessageId) + "/voters?pageSize=1";
        var first = (await setup.TargetClient.GetFromJsonAsync<ConversationPollVoterPageResult>(route, JsonOptions))!;
        Assert.True(first.HasMore); Assert.Single(first.Items);
        var second = (await setup.TargetClient.GetFromJsonAsync<ConversationPollVoterPageResult>(route + "&page=2", JsonOptions))!;
        Assert.False(second.HasMore); Assert.NotEqual(first.Items.Single().User.UserId, second.Items.Single().User.UserId);
        var filtered = (await setup.OwnerClient.GetFromJsonAsync<ConversationPollVoterPageResult>(P2PollRoute(id, message.MessageId) + "/voters?optionNumber=2", JsonOptions))!;
        Assert.Equal(setup.Target.UserId, Assert.Single(filtered.Items).User.UserId);
        var wire = await setup.OwnerClient.GetStringAsync(route); Assert.DoesNotContain(setup.Target.PhoneNumber, wire); Assert.DoesNotContain("correctOption", wire, StringComparison.OrdinalIgnoreCase);
        using var stranger = factory.CreateClient(); await CreateMessagingUserAsync(stranger); Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(route)).StatusCode);
        (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(route)).StatusCode);
    }
    [Fact]
    public async Task P2QuizHidesAnswerUntilOwnFinalVoteAndNeverCreatesEducationalGrade()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Poll(quiz: true);
        await using var evidenceScope = factory.Services.CreateAsyncScope(); var evidenceDb = evidenceScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var educationBefore = (await evidenceDb.ExamAttempts.CountAsync(), await evidenceDb.ExamGradeRevisions.CountAsync(), await evidenceDb.ExamGradeReleases.CountAsync());
        var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command);
        foreach (var client in new[] { setup.FirstClient, setup.SecondClient })
        { var poll = await ReadP2PollAsync(client, setup.ConversationId, message.MessageId); Assert.Null(poll.CorrectOptionNumber); Assert.Null(poll.Explanation); }
        var answer = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1]);
        Assert.Equal(2, answer.CorrectOptionNumber); Assert.Equal(command.Explanation, answer.Explanation);
        var owner = await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, message.MessageId); Assert.Null(owner.CorrectOptionNumber); Assert.Null(owner.Explanation);
        var changed = await setup.SecondClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/vote", new VoteConversationPollCommand([2], answer.OwnVoteVersion), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode); Assert.Equal(MessagingErrorCodes.PollVoteImmutable, await ReadProblemCodeAsync(changed));
        var closed = await CloseP2PollAsync(setup.FirstClient, owner); Assert.Null(closed.CorrectOptionNumber); Assert.Null(closed.Explanation);
        Assert.Equal(answer.OwnVoteVersion, (await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1])).OwnVoteVersion);
        Assert.Null((await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, message.MessageId)).CorrectOptionNumber);
        Assert.Single(await factory.GetMessageRevisionTextsAsync(message.MessageId));
        Assert.Equal(educationBefore, (await evidenceDb.ExamAttempts.CountAsync(), await evidenceDb.ExamGradeRevisions.CountAsync(), await evidenceDb.ExamGradeReleases.CountAsync()));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Single(await db.ConversationQuizAnswers.Where(k => k.PollId == message.MessageId).ToArrayAsync()); Assert.Equal(1, (await db.ConversationPollVotes.SingleAsync(v => v.PollId == message.MessageId)).Revision);
    }
    [Fact]
    public async Task P2PollCloseIsFinalVersionedIdempotentAndRefusesNewOrChangedVotes()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId);
        var stale = await ReadP2PollAsync(setup.FirstClient, setup.ConversationId, message.MessageId);
        var voted = await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1]);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await setup.FirstClient.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/close", new CloseConversationPollCommand(stale.Version), JsonOptions)).StatusCode);
        var closed = await CloseP2PollAsync(setup.FirstClient, voted); Assert.Equal(ConversationPollStatus.Closed, closed.Status);
        Assert.Equal(closed.Version, (await CloseP2PollAsync(setup.FirstClient, voted)).Version);
        foreach (var (client, command) in new[] { (setup.FirstClient, new VoteConversationPollCommand([2])), (setup.SecondClient, new VoteConversationPollCommand([2], voted.OwnVoteVersion)) })
        { var response = await client.PostAsJsonAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/vote", command, JsonOptions); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal(MessagingErrorCodes.PollClosed, await ReadProblemCodeAsync(response)); }
        Assert.Equal(voted.OwnVoteVersion, (await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1])).OwnVoteVersion);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.PostAsync(P2PollRoute(setup.ConversationId, message.MessageId) + "/reopen", null)).StatusCode);
    }
    [Fact]
    public async Task P2PollForwardIsIndependentTextWithOptionsButWithoutVotesOrQuizKey()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Poll(quiz: true);
        var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command); await VoteP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId, [1]);
        var saved = (await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}/forward", new ForwardMessageCommand(saved.ConversationId, Guid.NewGuid()), JsonOptions);
        response.EnsureSuccessStatusCode(); var receipt = (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
        Assert.Equal(MessageKind.Text, receipt.Kind); var forwarded = Assert.Single(await factory.GetMessagesAsync(saved.ConversationId));
        Assert.Contains(command.Question, forwarded.Text); Assert.All(command.Options, o => Assert.Contains(o, forwarded.Text)); Assert.DoesNotContain(command.Explanation!, forwarded.Text!);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.GetAsync(P2PollRoute(saved.ConversationId, receipt.MessageId))).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationPolls.AnyAsync(p => p.Id == receipt.MessageId));
    }
    [Fact]
    public async Task P2QuizHistorySearchReplyAndRevisionNeverExposeKeyOrExplanation()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Poll(quiz: true); var message = await CreateP2PollAsync(setup.FirstClient, setup.ConversationId, command);
        await SendAdvancedMessageAsync(setup.SecondClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پاسخ به پیام", ReplyToMessageId = message.MessageId });
        foreach (var route in new[] { $"/api/v1/conversations/{setup.ConversationId}/messages", $"/api/v1/messages/search?q={Uri.EscapeDataString(command.Question)}&kind=Poll" })
        { var wire = await setup.SecondClient.GetStringAsync(route); Assert.DoesNotContain(command.Explanation!, wire); Assert.DoesNotContain("correctOption", wire, StringComparison.OrdinalIgnoreCase); }
        Assert.Equal(command.Question, Assert.Single(await factory.GetMessageRevisionTextsAsync(message.MessageId)));
        var edit = await setup.FirstClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{message.MessageId}", new EditMessageCommand("بازنویسی ممنوع", message.Version), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode); Assert.Equal(MessagingErrorCodes.MessageNotEditable, await ReadProblemCodeAsync(edit));
        Assert.Null((await ReadP2PollAsync(setup.SecondClient, setup.ConversationId, message.MessageId)).CorrectOptionNumber);
    }
}
