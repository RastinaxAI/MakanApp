using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(300)]
    [InlineData(1000)]
    [InlineData(3000)]
    [InlineData(10000)]
    public async Task ErrorAuditSameIntentAtObservedPendingStateNeverDuplicatesOrSilentlyReplays(int delayMs)
    {
        for (var repetition = 1; repetition <= 10; repetition++)
        {
            using var setup = factory.CreateClient();
            var actor = await CreateMessagingUserAsync(setup);
            await PersonalKnowledgeContext(setup);
            using var host = AdaptiveHost();
            using var client = KnowledgeClient(host, setup);
            factory.AiService.Reset();
            // خط پایهٔ سالم قبل و بعد، با intentهای مستقل از عملیات race است.
            await AdaptiveChat(client, new("audit healthy before", null));
            factory.AiService.Reset();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            factory.AiService.StreamGate = async ct => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
            var operation = Guid.NewGuid();
            var command = new SendAiChatCommand("audit one logical intent", null) { OperationId = operation };
            var started = DateTimeOffset.UtcNow;
            var clock = Stopwatch.StartNew();
            var first = client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions);
            var timeline = new List<object>();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await using (var scope = host.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                    var reservation = await db.UsageReservations.SingleAsync(x => x.OperationId == operation);
                    Assert.True(reservation.IsUnmetered);
                    Assert.Equal(UsageReservationStatus.Reserved, reservation.Status);
                    Assert.False(await db.AiConversationTurns.AnyAsync(x => x.OperationId == operation && x.IsCompleted));
                }
                timeline.Add(new { stage = "OBSERVED_SQL_RESERVED_AND_PROVIDER_BLOCKED", elapsedMs = clock.ElapsedMilliseconds });
                // این تأخیر فقط زمان درخواست دوم است؛ اثبات Pending از SQL و barrier می‌آید.
                if (delayMs > 0) await Task.Delay(delayMs);
                using (var duplicate = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions))
                {
                    var code = await AdaptiveProblem(duplicate);
                    Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
                    Assert.Equal(AiInvocationErrorCodes.InProgress, code);
                    timeline.Add(new { stage = "DUPLICATE_WHILE_PENDING", elapsedMs = clock.ElapsedMilliseconds, httpStatus = 409, code });
                }
                release.TrySetResult();
                using (var completed = await first.WaitAsync(TimeSpan.FromSeconds(30)))
                {
                    Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
                    var stream = await completed.Content.ReadAsStringAsync();
                    Assert.Contains("event: done", stream);
                    Assert.DoesNotContain("event: error", stream);
                    timeline.Add(new { stage = "SSE_DONE", elapsedMs = clock.ElapsedMilliseconds, httpStatus = 200 });
                }
                using (var replay = await client.PostAsJsonAsync("/api/v1/ai/chat/stream", command, JsonOptions))
                {
                    var code = await AdaptiveProblem(replay);
                    Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
                    Assert.Equal(AiInvocationErrorCodes.Completed, code);
                    timeline.Add(new { stage = "COMPLETED_REPLAY", elapsedMs = clock.ElapsedMilliseconds, httpStatus = 409, code });
                }
                await using (var scope = host.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                    Assert.Equal(1, await db.UsageReservations.CountAsync(x => x.OperationId == operation));
                    Assert.Equal(UsageReservationStatus.Consumed, (await db.UsageReservations.SingleAsync(x => x.OperationId == operation)).Status);
                    Assert.Equal(2, await db.AiConversationTurns.CountAsync(x => x.OperationId == operation && x.IsCompleted));
                    Assert.Equal(1, await db.AiConversationTurns.Where(x => x.OperationId == operation).Select(x => x.ConversationId).Distinct().CountAsync());
                }
                Assert.Equal(1, factory.AiService.ProviderCallCount);
                factory.AiService.StreamGate = null;
                await AdaptiveChat(client, new("audit healthy after", null));
                Assert.Equal(2, factory.AiService.ProviderCallCount);
                timeline.Add(new { stage = "HEALTHY_NEW_INTENT_AFTER", elapsedMs = clock.ElapsedMilliseconds, httpStatus = 200 });
                await WriteRaceEvidence(delayMs, repetition, started, clock.ElapsedMilliseconds, timeline);
            }
            finally
            {
                release.TrySetResult();
                // اولین درخواست نیز باید تمام شود تا کنترل مشترک وسط stream عملیات بعد Reset نشود.
                try { using var pending = await first.WaitAsync(TimeSpan.FromSeconds(30)); }
                finally { factory.AiService.Reset(); }
            }
        }
    }

    private static async Task WriteRaceEvidence(int delayMs, int repetition, DateTimeOffset started, long elapsedMs, IReadOnlyList<object> timeline)
    {
        var directory = Environment.GetEnvironmentVariable("MAKAN_ERROR_AUDIT_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var evidence = new
        {
            runId = "20261125-api-errors-ac9a239-01", caseId = "I03", variantId = "observed-pending-" + delayMs,
            repetition, startedAtUtc = started, elapsedMs, evidenceKind = "LOCAL_INTEGRATION_FAULT",
            operationId = $"fixture-operation-{delayMs}-{repetition}", userAlias = $"fixture-user-{delayMs}-{repetition}",
            endpoint = "/api/v1/ai/chat/stream", method = "POST", httpStatus = 409,
            businessCode = AiInvocationErrorCodes.InProgress, timeline,
            stateBefore = "SQL_RESERVED_PROVIDER_BLOCKED", stateAfter = "ONE_CONSUMED_RESERVATION_ONE_COMPLETED_PAIR",
            transportResult = "HTTP_RESPONSE", businessResult = "IDEMPOTENT_CONFLICT_THEN_COMPLETION",
            userExperienceResult = "NOT_RUN_NO_BROWSER", verdict = "PASS",
            retryCount = 0, retrySource = "CONTROLLED_DUPLICATE_INTENT_NOT_TRANSPORT_RETRY", backoffMs = delayMs,
            expected = "Only one invocation; duplicate 409 pending, completed replay 409; healthy new intent succeeds.",
            requestPayload = new { alias = "synthetic-intent-no-private-content" },
            proposedOwner = "backend", causeConfidence = "CONFIRMED_LOCAL_ONLY", healthyBaselineBefore = true, healthyBaselineAfter = true
        };
        await File.WriteAllTextAsync(Path.Combine(directory, $"race-{delayMs}-{repetition}.json"), JsonSerializer.Serialize(evidence));
    }
}
