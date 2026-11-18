using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(AiChatMode.SourcesOnly, AiSourceIntent.General)]
    [InlineData(AiChatMode.Auto, AiSourceIntent.SourceFocused)]
    public async Task AdaptiveSourceFocusedRequestWithoutSourceDoesNotSilentlyGenerateGeneral(AiChatMode mode, AiSourceIntent intent)
    {
        using var setup = factory.CreateClient(); await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        using var host = AdaptiveHost(); using var client = KnowledgeClient(host, setup); factory.AiService.Reset();
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("file question", null) { Mode = mode, SourceIntent = intent }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(AiServiceErrorCodes.InvalidRequest, await AdaptiveProblem(response));
        Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AdaptiveSourcesOnlyUsesExactAuthorizedRagAndHistoryCannotOutliveSourceAcl()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId)); var source = await PublishedPersonalKnowledge(setup);
        var transport = new KnowledgeTransport(); using var host = AdaptiveHost(transport); using var client = KnowledgeClient(host, setup);
        await IngestPersonalKnowledge(client, source); factory.AiService.Reset();
        var command = new SendAiChatCommand("grounded question", null) { OperationId = Guid.NewGuid(), Mode = AiChatMode.SourcesOnly,
            SelectedSources = [new(source.SourceId, source.Revision)] };
        var answer = await AdaptiveChat(client, command);
        Assert.Equal(AiChatProvenanceKind.KnowledgeRag, answer.Provenance.Kind); Assert.Single(answer.Citations);
        Assert.Empty(answer.Sources); Assert.Equal(0, factory.AiService.ProviderCallCount); Assert.Equal(1, transport.RagCalls);
        using (var repeat = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions))
        { Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode); Assert.Equal(AiInvocationErrorCodes.Completed, await AdaptiveProblem(repeat)); }
        Assert.Equal(1, transport.RagCalls);
        var id = Guid.Parse(answer.ConversationId!);
        using (var read = await client.GetAsync($"/api/v1/ai/conversations/{id}/history")) read.EnsureSuccessStatusCode();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.EducationalSources.SingleAsync(x => x.Id == source.SourceId)).Archive(factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
        using (var denied = await client.GetAsync($"/api/v1/ai/conversations/{id}/history")) Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict });
        using (var denied = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("continue", id), JsonOptions)) Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict });
        Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Theory]
    [InlineData(AiChatMode.Auto, AiSourceIntent.SourceFocused, false)]
    [InlineData(AiChatMode.Auto, AiSourceIntent.General, true)]
    [InlineData(AiChatMode.SourcesOnly, AiSourceIntent.General, false)]
    public async Task AdaptiveProcessingFallbackRequiresExplicitGeneralIntent(AiChatMode mode, AiSourceIntent intent, bool fallback)
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId)); var source = await PublishedPersonalKnowledge(setup);
        var transport = new KnowledgeTransport(); using var host = AdaptiveHost(transport); using var client = KnowledgeClient(host, setup);
        await IngestPersonalKnowledge(client, source); transport.States[(source.SourceId, source.Revision)] = "Embedding"; factory.AiService.Reset();
        var command = new SendAiChatCommand("question", null) { Mode = mode, SourceIntent = intent, SelectedSources = [new(source.SourceId, source.Revision)] };
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions);
        if (fallback)
        {
            response.EnsureSuccessStatusCode(); var result = (await response.Content.ReadFromJsonAsync<AiChatResult>(JsonOptions))!;
            Assert.Equal(AiChatProvenanceKind.GeneralFallback, result.Provenance.Kind); Assert.Equal(KnowledgeErrorCodes.Processing, result.Provenance.Reason);
            Assert.Empty(result.Sources); Assert.Empty(result.Citations); Assert.Equal(1, factory.AiService.ProviderCallCount);
        }
        else
        { Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal(KnowledgeErrorCodes.Processing, await AdaptiveProblem(response)); Assert.Equal(0, factory.AiService.ProviderCallCount); }
        Assert.Equal(0, transport.RagCalls);
    }

    [Fact]
    public async Task AdaptiveAutoDefaultIsSourceFocusedAndNeverFallsBackOnForeignSource()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner); await PersonalKnowledgeContext(owner);
        using var outsider = factory.CreateClient(); await CreateMessagingUserAsync(outsider); await PersonalKnowledgeContext(outsider);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId)); var source = await PublishedPersonalKnowledge(owner);
        var transport = new KnowledgeTransport(); using var host = AdaptiveHost(transport);
        using var client = KnowledgeClient(host, owner); using var foreign = KnowledgeClient(host, outsider); factory.AiService.Reset();
        using (var pending = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("file question", null) { SelectedSourceIds = [source.SourceId], SelectedSources = [] }, JsonOptions))
        { Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode); Assert.Equal(KnowledgeErrorCodes.Processing, await AdaptiveProblem(pending)); }
        var before = transport.Calls;
        using (var denied = await foreign.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("general", null) { Mode = AiChatMode.Auto,
            SourceIntent = AiSourceIntent.General, SelectedSources = [new(source.SourceId, source.Revision)] }, JsonOptions)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(before, transport.Calls); Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AdaptiveGroundedStreamingRequiresNonStreamAndDoesNotGenerateOrFakeDeltas()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId)); var source = await PublishedPersonalKnowledge(setup);
        var transport = new KnowledgeTransport(); using var host = AdaptiveHost(transport); using var client = KnowledgeClient(host, setup);
        await IngestPersonalKnowledge(client, source); factory.AiService.Reset();
        using var result = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", new SendAiChatCommand("grounded", null)
            { Mode = AiChatMode.SourcesOnly, SelectedSources = [new(source.SourceId, source.Revision)] }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode); Assert.Equal(AiConversationErrorCodes.StreamingNotSupported, await AdaptiveProblem(result));
        Assert.DoesNotContain("event: delta", await result.Content.ReadAsStringAsync()); Assert.Equal(0, transport.RagCalls); Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AdaptiveAutoGeneralFallbackStreamsRealDeltasWithExplicitProvenance()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId)); var source = await PublishedPersonalKnowledge(setup);
        var transport = new KnowledgeTransport(); using var host = AdaptiveHost(transport); using var client = KnowledgeClient(host, setup); factory.AiService.Reset();
        using var result = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", new SendAiChatCommand("general", null)
            { Mode = AiChatMode.Auto, SourceIntent = AiSourceIntent.General, SelectedSources = [new(source.SourceId, source.Revision)] }, JsonOptions);
        result.EnsureSuccessStatusCode(); var wire = await result.Content.ReadAsStringAsync();
        Assert.Contains("GeneralFallback", wire); Assert.Contains("event: delta", wire); Assert.Contains("event: done", wire);
        Assert.Equal(1, factory.AiService.ProviderCallCount); Assert.Equal(0, transport.RagCalls);
    }

    [Theory]
    [InlineData(AiChatMode.Auto, AiSourceIntent.General, true)]
    [InlineData(AiChatMode.Auto, AiSourceIntent.SourceFocused, false)]
    [InlineData(AiChatMode.SourcesOnly, AiSourceIntent.General, false)]
    public async Task AdaptiveInsufficientEvidenceNeverInventsCitationOrHiddenSourcesOnlyFallback(AiChatMode mode, AiSourceIntent intent, bool fallback)
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId)); var source = await PublishedPersonalKnowledge(setup);
        var transport = new KnowledgeTransport { EmptyEvidence = true }; using var host = AdaptiveHost(transport); using var client = KnowledgeClient(host, setup);
        await IngestPersonalKnowledge(client, source); factory.AiService.Reset();
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("insufficient", null)
            { Mode = mode, SourceIntent = intent, SelectedSources = [new(source.SourceId, source.Revision)] }, JsonOptions);
        if (fallback)
        {
            response.EnsureSuccessStatusCode(); var result = (await response.Content.ReadFromJsonAsync<AiChatResult>(JsonOptions))!;
            Assert.Equal(AiChatProvenanceKind.GeneralFallback, result.Provenance.Kind); Assert.Empty(result.Citations); Assert.Empty(result.Sources);
        }
        else { Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal(AiConversationErrorCodes.InsufficientEvidence, await AdaptiveProblem(response)); }
        Assert.Equal(fallback ? 1 : 0, factory.AiService.ProviderCallCount); Assert.Equal(0, transport.RagCalls);
    }
}
