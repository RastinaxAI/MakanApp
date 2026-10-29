using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private sealed record TestActor(HttpClient Client, Guid UserId, Guid SessionId, string Phone);
    private sealed class RequestCounter(Action count) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { count(); return base.SendAsync(request, ct); }
    }

    private async Task<TestActor> ActorAsync()
    {
        var client = factory.CreateDefaultClient(new RequestCounter(() => Interlocked.Increment(ref _requestCount)));
        _clients.Add(client);
        return await AuthenticateAsync(client, $"+98939{Interlocked.Increment(ref _phone):D7}");
    }
    private Task<TestActor> ReauthenticateAsync(TestActor actor) => AuthenticateAsync(actor.Client, actor.Phone);
    private async Task<TestActor> AnotherDeviceAsync(TestActor actor)
    {
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(1));
        var client = factory.CreateDefaultClient(new RequestCounter(() => Interlocked.Increment(ref _requestCount)));
        _clients.Add(client); return await AuthenticateAsync(client, actor.Phone);
    }
    private async Task<TestActor> AuthenticateAsync(HttpClient client, string phone)
    {
        var challenge = await PostAsync<RequestOtpResult>(client, "/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        var auth = await PostAsync<VerifyOtpResult>(client, "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, factory.GetLatestOtpCode(phone)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var session = await WithDb(db => db.UserSessions.Where(s => s.UserId == auth.User.Id).OrderByDescending(s => s.CreatedAtUtc).Select(s => s.Id).FirstAsync());
        return new TestActor(client, auth.User.Id, session, phone);
    }
    private async Task<TestActor> AdminAsync()
    {
        await factory.ResetPlatformIamAsync(); var actor = await ActorAsync();
        await factory.BootstrapPlatformOwnerAsync(actor.UserId); return actor;
    }
    private async Task<Guid> PlanAsync(SubscriptionOwnerType audience, long? ai = 10, bool hosting = false, long classes = 5, long learners = 5,
        decimal priceIrr = 1000, bool aiEntitled = true, bool reports = false, long exports = 10) => await WithDb(async db =>
    {
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var plan = Plan.Create("product-test." + Guid.NewGuid().ToString("N"), "پلن ساختگی آزمون", now.AddDays(-90), audience);
        plan.SetAvailability(true, true, true, now.AddDays(-90));
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-90));
        version.SetMonthlyTerms(BillingCalendar.Gregorian, "test-only-not-for-sale", 80, 95);
        version.SetPrice(priceIrr, CurrencyCode.Irr);
        version.SetEntitlement(EntitlementKey.Ai, aiEntitled);
        version.SetEntitlement(EntitlementKey.PersonalClassHosting, hosting);
        version.SetEntitlement(EntitlementKey.Academic, audience == SubscriptionOwnerType.Organization);
        version.SetEntitlement(EntitlementKey.Assignments, audience == SubscriptionOwnerType.Organization);
        version.SetEntitlement(EntitlementKey.Exams, audience == SubscriptionOwnerType.Organization);
        version.SetEntitlement(EntitlementKey.Reports, reports);
        version.SetQuota(QuotaKey.AiTextRequests, ai); version.SetQuota(QuotaKey.ActiveClasses, classes);
        version.SetQuota(QuotaKey.ActiveLearners, learners); version.SetQuota(QuotaKey.StorageBytes, 1_000_000);
        version.SetQuota(QuotaKey.AssignmentFileBytes, 1_000_000);
        version.SetQuota(QuotaKey.ReportExports, exports);
        version.Publish(now.AddDays(-89)); db.Plans.Add(plan); db.PlanVersions.Add(version); await db.SaveChangesAsync(); return version.Id;
    });

    private async Task<Guid> SubscribeAsync(SubscriptionOwner owner, Guid versionId, DateTime? anchor = null) => await WithDb(async db =>
    {
        var version = await db.PlanVersions.Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions).SingleAsync(v => v.Id == versionId);
        var start = anchor ?? factory.Clock.GetUtcNow().UtcDateTime;
        var subscription = Subscription.CreateMonthly(owner, version, start, 0, start, null, true);
        db.Subscriptions.Add(subscription); await db.SaveChangesAsync(); return subscription.Id;
    });

    private async Task SeedUsageAsync(TestActor actor, Guid subscriptionId, Guid versionId, int consumed, int reserved) => await WithDb(async db =>
    {
        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        for (var i = 0; i < consumed + reserved; i++)
        {
            var r = UsageReservation.Create(SubscriptionOwner.Individual(actor.UserId), subscriptionId, versionId, actor.UserId,
                CommerceContextType.Personal, actor.UserId, QuotaKey.AiTextRequests, subscription.StartsAtUtc, subscription.EndsAtUtc!.Value,
                1, Guid.NewGuid(), new byte[32], false, now);
            db.UsageReservations.Add(r); db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(r, now, "fixture_reserved"));
            if (i < consumed)
            {
                r.Finalize(UsageReservationStatus.Consumed, "fixture_success", now);
                db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(r, now, "fixture_success"));
            }
        }
        await db.SaveChangesAsync();
    });

    private async Task<UsageReservationDto> ReserveAsync(TestActor actor, AiQuotaPreflightCommand context, Guid operationId, byte[]? hash = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommercialSubscriptionService>().ReserveSimpleTextAsync(actor.UserId, actor.SessionId,
            new ReserveUsageCommand(context, operationId, hash ?? new byte[32]), default);
    }
    private async Task<string?> TryReserveAsync(TestActor actor, AiQuotaPreflightCommand context)
    {
        try { await ReserveAsync(actor, context, Guid.NewGuid()); return null; }
        catch (CommerceException error) { return error.Code; }
    }
    private async Task<UsageReservationDto> FinishAsync(TestActor actor, UsageReservationDto reservation, UsageReservationStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommercialSubscriptionService>().FinalizeAsync(actor.UserId, reservation.ReservationId,
            status, status == UsageReservationStatus.Unknown ? "provider_timeout" : "provider_result", default);
    }
    private Task<MakanApp.Application.Academic.PersonalClassInvitationDto> InviteAsync(TestActor host, TestActor student, Guid classId) =>
        PostAsync<MakanApp.Application.Academic.PersonalClassInvitationDto>(host.Client, $"/api/v1/personal-classes/{classId}/invitations",
            new MakanApp.Application.Academic.InvitePersonalClassStudentCommand(student.UserId, Guid.NewGuid()));
    private Task<InitiatePaymentResult> InitiateAsync(TestActor actor, Guid orderId) =>
        PostAsync<InitiatePaymentResult>(actor.Client, $"/api/v1/commerce/personal/orders/{orderId}/payments", new InitiatePaymentCommand(Guid.NewGuid()));
    private async Task<PaymentAttemptDto> PayAsync(TestActor actor, Guid orderId) => (await InitiateAsync(actor, orderId)).PaymentAttempt;
    private async Task<VerifyPaymentResult> VerifyAsync(Guid attemptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPaymentVerificationService>().VerifyPaymentAsync(new VerifyPaymentCommand(attemptId, null), default);
    }
    private static async Task SelectAsync(TestActor actor, Guid membershipId, OrganizationRole role)
    {
        using var response = await actor.Client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role), Json);
        response.EnsureSuccessStatusCode();
    }
    private static async Task<T> PostAsync<T>(HttpClient client, string path, object? body)
    { using var response = await client.PostAsJsonAsync(path, body, Json); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>(Json))!; }
    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    { using var response = await client.GetAsync(path); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>(Json))!; }
    private static async Task<string?> ProblemAsync(HttpResponseMessage response)
    { using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.GetProperty("code").GetString(); }
    private async Task<T> WithDb<T>(Func<MakanDbContext, Task<T>> action)
    { await using var scope = factory.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<MakanDbContext>()); }
    private async Task WithDb(Func<MakanDbContext, Task> action)
    { await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<MakanDbContext>()); }
    private static JsonSerializerOptions MakeJson()
    { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); options.Converters.Add(new JsonStringEnumConverter()); return options; }
    public Task InitializeAsync()
    { factory.Clock.SetUtcNow(new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)); factory.PaymentGateway.Reset(); return Task.CompletedTask; }
    public Task DisposeAsync()
    {
        output.WriteLine("SUBSCRIPTION_HTTP_REQUESTS=" + _requestCount);
        foreach (var client in _clients) client.Dispose(); factory.Clock.UseSystemTime(); return Task.CompletedTask;
    }
}
