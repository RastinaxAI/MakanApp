using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Intelligence;
using MakanApp.Infrastructure.Persistence;
using MakanApp.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.AiIntegrationProbe;

// سناریو از HTTP واقعی Backend و ledger همان SQL ایزوله عبور می‌کند؛ فقط AI وابستگی Production است.
internal sealed class LivePersonalAiScenario(MakanAppWebApplicationFactory factory, WebApplicationFactory<Program> application,
    LiveAiObservation observation, JsonSerializerOptions json)
{
    public async Task RunAsync()
    {
        using var client = application.CreateClient();
        Stage("personal_fixture_authentication");
        const string phone = "+989399900021";
        using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone), json);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(json))!;
        var otp = application.Services.GetRequiredService<OtpProviderTestControl>().GetLatestCode(phone);
        using var verification = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, phone, otp), json);
        verification.EnsureSuccessStatusCode();
        var user = (await verification.Content.ReadFromJsonAsync<VerifyOtpResult>(json))!;
        Stage("personal_fixture_authenticated");
        observation.Forbid(phone, otp, user.AccessToken, user.RefreshToken);
        observation.ExpectedUserId = user.User.Id; observation.ExpectedRole = "Personal";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var selection = await client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Personal, Guid.NewGuid()), json);
        selection.EnsureSuccessStatusCode();
        var selected = (await selection.Content.ReadFromJsonAsync<AssistantContextResult>(json))!;
        Require(selected.UpstreamContextSupported && selected.Limitation is null, "PERSONAL_CONTEXT_NOT_SUPPORTED");
        Stage("personal_context_supported");
        await CreateTestContractAsync(user.User.Id);
        Stage("personal_test_contract_ready");
        var operation = Guid.NewGuid(); var correlation = "personal-live-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlation);
        client.DefaultRequestHeaders.Add("X-AI-Operation-Id", operation.ToString("D"));
        var command = new SendAiChatCommand("برای یادگیری برنامه‌نویسی یک راهکار کوتاه و کاربردی پیشنهاد بده.", null) { OperationId = operation };
        var started = Stopwatch.GetTimestamp();
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", command, json);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        string? backendError = null; var valid = false;
        if (response.IsSuccessStatusCode)
        {
            var answer = (await response.Content.ReadFromJsonAsync<AiChatResult>(json))!;
            valid = !string.IsNullOrWhiteSpace(answer.Answer.Text) && answer.RequestId == observation.RequestId && answer.CorrelationId == correlation;
        }
        else
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var code = problem.RootElement.TryGetProperty("code", out var value) ? value.GetString() : null;
            backendError = code is AiServiceErrorCodes.InvalidRequest or AiServiceErrorCodes.ContextNotAllowed or AiServiceErrorCodes.ResponseInvalid or
                AiServiceErrorCodes.ServiceTimeout or AiServiceErrorCodes.ServiceUnavailable or AiServiceErrorCodes.RateLimited ? code : "OTHER_BACKEND_FAILURE";
        }
        LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = "personal_backend_live", passed = valid,
            backendStatus = (int)response.StatusCode, backendError, aiStatus = observation.AiHttpStatus, aiError = observation.AiErrorCode,
            requestId = observation.RequestId, correlationId = observation.CorrelationId, elapsedMs = Math.Round(elapsed, 1), modelAlias = observation.ModelAlias,
            calls = observation.OutgoingCalls, personalScopeSafe = observation.PayloadsSafe, serviceAuthEnabled = observation.UseServiceAuthentication }));
        Require(valid && observation.AiHttpStatus == 200 && observation.OutgoingCalls == 1 && observation.PayloadsSafe, "LIVE_PERSONAL_CHAT_NOT_ACCEPTED");
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/chat", command, json);
        using var repeated = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Require(replay.StatusCode == HttpStatusCode.Conflict && repeated.RootElement.GetProperty("code").GetString() == AiInvocationErrorCodes.Completed &&
            observation.OutgoingCalls == 1, "LIVE_PERSONAL_REPLAY_NOT_SAFE");
        await using var scope = application.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == operation);
        var consumes = await db.UsageLedgerEntries.CountAsync(l => l.ReservationId == reservation.Id && l.Status == UsageReservationStatus.Consumed);
        var reserves = await db.UsageLedgerEntries.CountAsync(l => l.ReservationId == reservation.Id && l.Status == UsageReservationStatus.Reserved);
        Require(reservation.Status == UsageReservationStatus.Consumed && reservation.OwnerType == SubscriptionOwnerType.Individual &&
            reservation.OwnerId == user.User.Id && consumes == 1 && reserves == 1 && !reservation.IsTrial, "LIVE_PERSONAL_LEDGER_NOT_SAFE");
        Require(observation.MetricsSafe, "LIVE_PERSONAL_METRICS_NOT_SAFE");
        LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = "personal_backend_acceptance", passed = true,
            reservationCount = 1, consumptionCount = consumes, reserveLedgerCount = reserves, replayStatus = (int)replay.StatusCode,
            aiCalls = observation.OutgoingCalls, payer = "Individual", ordinaryAccount = true, metricsSafe = observation.MetricsSafe }));
    }

    private async Task CreateTestContractAsync(Guid user)
    {
        await using var scope = application.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Require(db.Database.GetDbConnection().Database.StartsWith(MakanAppWebApplicationFactory.KnowledgeProbeDatabasePrefix, StringComparison.Ordinal), "ISOLATED_PERSONAL_DATABASE_REQUIRED");
        Require(!await db.Memberships.AnyAsync(m => m.UserId == user), "ORDINARY_PERSONAL_ACCOUNT_REQUIRED");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var plan = Plan.Create("personal-live." + Guid.NewGuid().ToString("N"), "قرارداد ساختگی پذیرش؛ غیرقابل فروش", now.AddDays(-2), SubscriptionOwnerType.Individual);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-2));
        version.SetMonthlyTerms(BillingCalendar.Gregorian, "isolated-personal-acceptance-only", 80, 95);
        version.SetPrice(1000, CurrencyCode.Irr); version.SetEntitlement(EntitlementKey.Ai, true); version.SetQuota(QuotaKey.AiTextRequests, 3);
        version.Publish(now.AddDays(-1)); db.Plans.Add(plan); db.PlanVersions.Add(version);
        db.Subscriptions.Add(Subscription.CreateMonthly(SubscriptionOwner.Individual(user), version, now.AddHours(-1), 0, now, null, true));
        await db.SaveChangesAsync();
    }

    private static void Require(bool condition, string code)
    { if (!condition) throw new InvalidOperationException(code); }

    private void Stage(string stage) => LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage, aiCalls = observation.OutgoingCalls }));
}
