using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    private WebApplicationFactory<Program> AdaptiveHost(KnowledgeTransport? transport = null) => KnowledgeHost(transport ?? new()).WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["AiUsage:Mode"] = "Unmetered" }));
        builder.ConfigureTestServices(services => { services.RemoveAll<AiServiceTestControl>(); services.AddSingleton(factory.AiService); });
    });

    private static async Task<AiChatResult> AdaptiveChat(HttpClient client, SendAiChatCommand command)
    {
        using var result = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions);
        Assert.True(result.IsSuccessStatusCode, "Chat HTTP " + (int)result.StatusCode + ": " + await result.Content.ReadAsStringAsync());
        return (await result.Content.ReadFromJsonAsync<AiChatResult>(JsonOptions))!;
    }
    private static async Task<string> AdaptiveProblem(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task AdaptivePlainChatCreatesCanonicalConversationAndDatabaseOwnedHistoryAcrossSession()
    {
        using var setup = factory.CreateClient(); var actor = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        factory.AiService.Reset(); using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup);
        var first = await AdaptiveChat(client, new("first question", null)); var id = Guid.Parse(first.ConversationId!);
        Assert.NotEqual(Guid.Empty, id); Assert.Empty(factory.AiService.LastRequest!.History!);
        var second = await AdaptiveChat(client, new("follow up", id) { AssistantProfile = new(7, AiAssistantProficiency.Intermediate, null) });
        Assert.Equal(first.ConversationId, second.ConversationId);
        var sent = Assert.IsType<AiChatRequest>(factory.AiService.LastRequest);
        var sentHistory = Assert.IsAssignableFrom<IReadOnlyList<AiHistoryTurn>>(sent.History);
        Assert.Equal(new[] { "User", "Assistant" }, sentHistory.Select(x => x.Role));
        Assert.Equal("first question", sentHistory[0].Text);
        Assert.DoesNotContain(sentHistory, x => x.Text == "follow up");
        var sentProfile = Assert.IsType<AiAssistantProfile>(sent.Context.AssistantProfile);
        Assert.Equal(7, sentProfile.GradeLevel); Assert.Empty(sentProfile.Goals!);
        using var history = await client.GetAsync($"/api/v1/ai/conversations/{id}/history"); history.EnsureSuccessStatusCode();
        Assert.True(history.Headers.CacheControl!.NoStore);
        Assert.Contains("Authorization", history.Headers.Vary);
        Assert.Equal(4, (await history.Content.ReadFromJsonAsync<AiConversationHistoryResult>(JsonOptions))!.Turns.Count);
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(2, await db.UsageReservations.CountAsync(x => x.ConsumerUserId == actor.UserId && x.IsUnmetered && x.Status == UsageReservationStatus.Consumed));
        Assert.False(await db.Subscriptions.AnyAsync(x => x.OwnerId == actor.UserId));
        Assert.Equal(4, await db.AiConversationTurns.CountAsync(x => x.ConversationId == id && x.IsCompleted));
    }

    [Theory]
    [InlineData("{\"gradeLevel\":\"4\"}")]
    [InlineData("{\"gradeLevel\":true}")]
    [InlineData("{\"gradeLevel\":1.2}")]
    [InlineData("{\"gradeLevel\":13}")]
    [InlineData("{\"proficiency\":\"beginner\"}")]
    [InlineData("{\"goals\":\"bad\"}")]
    public async Task AdaptiveInvalidProfileIsRejectedBeforeConversationOrProvider(string profile)
    {
        using var setup = factory.CreateClient(); var actor = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        factory.AiService.Reset(); using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup);
        using var result = await client.PostAsync("/api/v1/ai/chat", new StringContent("{\"message\":\"q\",\"assistantProfile\":" + profile + "}", Encoding.UTF8, "application/json"));
        Assert.Contains(result.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity });
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.AiConversations.AnyAsync(x => x.OwnerUserId == actor.UserId));
    }

    [Fact]
    public async Task AdaptiveForeignAndCrossContextConversationAreConcealedWithoutProvider()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner); await PersonalKnowledgeContext(owner);
        using var stranger = factory.CreateClient(); await CreateMessagingUserAsync(stranger); await PersonalKnowledgeContext(stranger);
        using var host = AdaptiveHost(); using var client = KnowledgeClient(host, owner); using var outsider = KnowledgeClient(host, stranger);
        factory.AiService.Reset(); var first = await AdaptiveChat(client, new("owner private question", null)); var id = Guid.Parse(first.ConversationId!);
        var count = factory.AiService.ProviderCallCount;
        using (var read = await outsider.GetAsync($"/api/v1/ai/conversations/{id}/history")) Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using (var write = await outsider.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("forged", id), JsonOptions)) Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        var org = await factory.CreateOrganizationAsync("بافت مستقل History"); var membership = await factory.CreateMembershipAsync(user.UserId, org, OrganizationRole.Manager);
        using var currentResponse = await client.GetAsync("/api/v1/ai/assistant-context"); currentResponse.EnsureSuccessStatusCode();
        var current = (await currentResponse.Content.ReadFromJsonAsync<AssistantContextResult>(JsonOptions))!;
        using (var select = await client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Organization, Guid.NewGuid(), membership.MembershipId, OrganizationRole.Manager, ExpectedVersion: current.Version), JsonOptions)) select.EnsureSuccessStatusCode();
        using (var cross = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("cross context", id), JsonOptions)) Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        using (var read = await client.GetAsync($"/api/v1/ai/conversations/{id}/history")) Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(count, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AdaptiveStreamingDonePersistsOnePairAndCompletedReplayNeverInvokesAgain()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        factory.AiService.Reset(); using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup);
        var command = new SendAiChatCommand("stream me", null) { OperationId = Guid.NewGuid() };
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions); response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        var wire = await response.Content.ReadAsStringAsync(); Assert.Contains("event: start", wire); Assert.Contains("event: done", wire);
        Assert.Equal(2, wire.Split("event: delta").Length - 1); Assert.DoesNotContain("event: error", wire);
        var done = wire.Split('\n').First(x => x.StartsWith("data: ") && x.Contains("metadata")); Assert.DoesNotContain("text", done);
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(AiInvocationErrorCodes.Completed, await AdaptiveProblem(replay));
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(2, await db.AiConversationTurns.CountAsync(x => x.OwnerUserId == user.UserId && x.IsCompleted));
    }

    [Fact]
    public async Task AdaptivePartialFailureIsErrorUnknownAndNeverEntersHistoryOrRetries()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        factory.AiService.Reset(); factory.AiService.StreamFailure = new AiServiceException(AiServiceErrorCodes.RateLimited, "PRIVATE_SENTINEL", 429);
        using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup);
        var id = Guid.NewGuid(); var command = new SendAiChatCommand("private pending prompt", id) { OperationId = Guid.NewGuid() };
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions); response.EnsureSuccessStatusCode();
        var wire = await response.Content.ReadAsStringAsync(); Assert.Contains("event: delta", wire); Assert.Contains("event: error", wire);
        Assert.DoesNotContain("event: done", wire); Assert.DoesNotContain("PRIVATE_SENTINEL", wire);
        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        Assert.DoesNotContain(logs.Snapshot(), x => x.Contains("PRIVATE_SENTINEL", StringComparison.Ordinal) || x.Contains("private pending prompt", StringComparison.Ordinal));
        Assert.Contains(logs.Entries(), x => x.Category == typeof(MakanApp.Api.Controllers.AiController).FullName &&
            x.Level == Microsoft.Extensions.Logging.LogLevel.Error && x.ExceptionText is null && x.Properties.ContainsKey("CorrelationId"));
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions);
        Assert.Equal(AiInvocationErrorCodes.Unknown, await AdaptiveProblem(replay)); Assert.Equal(1, factory.AiService.ProviderCallCount);
        using var history = await client.GetAsync($"/api/v1/ai/conversations/{id}/history"); history.EnsureSuccessStatusCode();
        Assert.Empty((await history.Content.ReadFromJsonAsync<AiConversationHistoryResult>(JsonOptions))!.Turns);
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var pending = await db.AiConversationTurns.SingleAsync(x => x.OwnerUserId == user.UserId); Assert.Null(pending.Text); Assert.False(pending.IsCompleted);
        Assert.Equal(UsageReservationStatus.Unknown, (await db.UsageReservations.SingleAsync(x => x.OperationId == command.OperationId)).Status);
        factory.AiService.Reset();
    }

    [Fact]
    public async Task AdaptiveTenConcurrentStreamsShareOneReservationCanonicalConversationAndInvocation()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        factory.AiService.Reset();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.AiService.StreamGate = async ct => { reached.TrySetResult(); await gate.Task.WaitAsync(ct); };
        using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup);
        var command = new SendAiChatCommand("one logical stream", null) { OperationId = Guid.NewGuid() };
        var first = client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions);
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 9).Select(_ => client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions)));
        try
        {
            foreach (var duplicate in duplicates) { Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.Equal(AiInvocationErrorCodes.InProgress, await AdaptiveProblem(duplicate)); }
        }
        finally { foreach (var duplicate in duplicates) duplicate.Dispose(); gate.TrySetResult(); }
        using var success = await first; success.EnsureSuccessStatusCode(); Assert.Contains("event: done", await success.Content.ReadAsStringAsync());
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.UsageReservations.CountAsync(x => x.ConsumerUserId == user.UserId));
        Assert.Equal(1, await db.AiConversations.CountAsync(x => x.OwnerUserId == user.UserId));
        Assert.Equal(2, await db.AiConversationTurns.CountAsync(x => x.OwnerUserId == user.UserId && x.IsCompleted));
        factory.AiService.Reset();
    }

    [Fact]
    public async Task AdaptiveHttpClientDisconnectCancelsUpstreamAndDoesNotPersistPartialAnswer()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        factory.AiService.Reset(); factory.AiService.StreamGate = ct => Task.Delay(Timeout.InfiniteTimeSpan, ct);
        using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup);
        using var cancel = new CancellationTokenSource();
        var operation = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat/stream") {
            Content = JsonContent.Create(new SendAiChatCommand("disconnect", null) { OperationId = operation }, options: JsonOptions) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token); response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(body);
        while (true)
        {
            var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(line); if (line == "event: delta") break;
        }
        cancel.Cancel(); response.Dispose();
        await factory.AiService.StreamCancellation.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(factory.AiService.StreamCanceled); Assert.Equal(1, factory.AiService.ProviderCallCount);
        // عملیات بعدی فقط وضعیت durable را می‌خواند؛ همان intent اجازهٔ شروع AI تازه ندارد.
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", new SendAiChatCommand("disconnect", null) { OperationId = operation }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Contains(await AdaptiveProblem(replay), new[] { AiInvocationErrorCodes.Unknown, AiInvocationErrorCodes.InProgress });
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.AiConversationTurns.AnyAsync(x => x.OwnerUserId == user.UserId && x.IsCompleted));
        Assert.Equal(1, factory.AiService.ProviderCallCount); factory.AiService.Reset();
    }
}
