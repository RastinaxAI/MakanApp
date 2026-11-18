using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AdaptiveClassSwitchDuringStreamStopsFollowingDeltasAndHistoryCommit()
    {
        using var scenario = await CreateOfficialScenarioAsync(false);
        var otherClass = await factory.CreateAcademicClassAsync(scenario.OrganizationId); Guid membership;
        await using (var scope = factory.Services.CreateAsyncScope()) membership = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Memberships
            .Where(x => x.OrganizationId == scenario.OrganizationId && x.UserId == scenario.TeacherId).Select(x => x.Id).SingleAsync();
        await factory.CreateTeacherAssignmentAsync(scenario.OrganizationId, otherClass.ClassId, membership);
        using var host = AdaptiveHost(); using var client = KnowledgeClient(host, scenario.Teacher);
        using (var selected = await client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.OrganizationClass, Guid.NewGuid(), membership,
                OrganizationRole.Teacher, ClassId: scenario.ClassId), JsonOptions)) selected.EnsureSuccessStatusCode();
        factory.AiService.Reset();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.AiService.StreamGate = async ct => { reached.TrySetResult(); await release.Task.WaitAsync(ct); };
        var id = Guid.NewGuid(); var operation = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat/stream") {
            Content = JsonContent.Create(new SendAiChatCommand("class A context", id) { OperationId = operation }, options: JsonOptions) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(); using var reader = new StreamReader(body);
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotNull(line);
                if (line == "event: delta") break;
            }
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var currentResponse = await client.GetAsync("/api/v1/ai/assistant-context"); currentResponse.EnsureSuccessStatusCode();
            var current = (await currentResponse.Content.ReadFromJsonAsync<AssistantContextResult>(JsonOptions))!;
            using (var changed = await client.PutAsJsonAsync("/api/v1/ai/assistant-context",
                new SelectAssistantContextCommand(AssistantDataContextKind.OrganizationClass, Guid.NewGuid(), membership,
                    OrganizationRole.Teacher, ClassId: otherClass.ClassId, ExpectedVersion: current.Version), JsonOptions)) changed.EnsureSuccessStatusCode();
            release.TrySetResult(); var remaining = await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Contains("event: error", remaining); Assert.Contains(AiServiceErrorCodes.ContextNotAllowed, remaining);
            Assert.DoesNotContain("\"sequence\":2", remaining); Assert.DoesNotContain("event: done", remaining);
            Assert.Equal(1, factory.AiService.ProviderCallCount);
            using var wrongContext = await client.GetAsync($"/api/v1/ai/conversations/{id}/history"); Assert.Equal(HttpStatusCode.NotFound, wrongContext.StatusCode);
            await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.False(await db.AiConversationTurns.AnyAsync(x => x.ConversationId == id && x.IsCompleted));
            Assert.Equal(UsageReservationStatus.Unknown, (await db.UsageReservations.SingleAsync(x => x.OperationId == operation)).Status);
        }
        finally { release.TrySetResult(); factory.AiService.Reset(); }
    }
}
