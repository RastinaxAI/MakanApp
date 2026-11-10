using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MakanApp.IntegrationTests;

public sealed partial class ZibalPaymentEndpointsTests
{
    private const string CallbackPath = "/api/integrations/v1/commerce/payments/zibal/callback";
    private static int _phoneSequence = 9_100_000;
    private Guid _activationFailureTarget;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private sealed record Setup(OrderDto Order, PaymentAttemptDto Attempt, Guid PlanId, Guid OperationId, Guid? MembershipId);

    private WebApplicationFactory<Program> Host(bool defaultUnavailable = false, ZibalStub? stub = null,
        bool failActivationOnce = false, bool zibalUnavailable = false, string? merchant = null)
    {
        stub ??= new ZibalStub();
        var failures = failActivationOnce ? 1 : 0;
        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            // میزبان‌های یک fixture باید همان کلید تصادفی تست را برای اعتبارسنجی نشست مشترک داشته باشند.
            services.RemoveAll<IIdentitySecurity>();
            services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IOptions<ZibalOptions>>();
            services.AddSingleton(Options.Create(new ZibalOptions
            {
                Merchant = zibalUnavailable ? "" : merchant ?? "zibal", CallbackUrl = "https://backend.example.test" + CallbackPath,
                ClientReturnUrl = "https://client.example.test/payment-return", AllowedClientReturnOrigins = ["https://client.example.test"]
            }));
            services.AddHttpClient<ZibalPaymentGateway>().ConfigurePrimaryHttpMessageHandler(() => new ZibalHandler(stub));
            services.RemoveAll<IPaymentGateway>();
            services.AddScoped<IPaymentGateway>(sp => defaultUnavailable ? sp.GetRequiredService<UnavailablePaymentGateway>() : sp.GetRequiredService<ZibalPaymentGateway>());
            if (failActivationOnce)
            {
                services.AddScoped<EfSubscriptionActivationHandler>();
                services.RemoveAll<ISubscriptionActivationHandler>();
                services.AddScoped<ISubscriptionActivationHandler>(sp => new FaultingActivation(
                    sp.GetRequiredService<EfSubscriptionActivationHandler>(), orderId => orderId == _activationFailureTarget && Interlocked.Exchange(ref failures, 0) == 1));
            }
        }));
    }

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private async Task<Guid> AuthenticateAsync(HttpClient client)
    {
        using var authentication = factory.CreateClient();
        var phone = $"+98936{Interlocked.Increment(ref _phoneSequence):D7}";
        var challenge = await ReadAsync<RequestOtpResult>(await authentication.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone), JsonOptions));
        var auth = await ReadAsync<VerifyOtpResult>(await authentication.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, factory.GetLatestOtpCode(phone)), JsonOptions));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth.User.Id;
    }

    private async Task<Setup> SetupAsync(HttpClient client, bool personal = false)
    {
        var userId = await AuthenticateAsync(client); Guid? membershipId = null;
        if (!personal)
        {
            var organization = await factory.CreateOrganizationAsync("پرداخت زیبال " + Guid.NewGuid().ToString("N"));
            var membership = await factory.CreateMembershipAsync(userId, organization, OrganizationRole.Manager); membershipId = membership.MembershipId;
            using var select = await client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, membership.MembershipId, OrganizationRole.Manager), JsonOptions);
            select.EnsureSuccessStatusCode();
        }
        Guid planId;
        if (personal)
        {
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var plan = Plan.Create("zibal-test." + Guid.NewGuid().ToString("N"), "عنوان تاریخی خرید", factory.Clock.GetUtcNow().UtcDateTime.AddHours(-3), SubscriptionOwnerType.Individual);
            plan.SetAvailability(true, true, true, factory.Clock.GetUtcNow().UtcDateTime.AddHours(-3));
            db.Plans.Add(plan); await db.SaveChangesAsync(); planId = plan.Id;
        }
        else planId = await factory.CreateCommercePlanAsync("عنوان تاریخی خرید");
        var versionId = await factory.CreatePublishedPlanVersionAsync(planId, 1, price: 1000m);
        var root = personal ? "/api/v1/commerce/personal/orders" : "/api/v1/commerce/orders";
        var order = await ReadAsync<CreateOrderResult>(await client.PostAsJsonAsync(root, new CreateOrderCommand(versionId, Guid.NewGuid()), JsonOptions));
        var operationId = Guid.NewGuid();
        var attempt = await ReadAsync<InitiatePaymentResult>(await client.PostAsJsonAsync(root + $"/{order.Order.OrderId}/payments", new InitiatePaymentCommand(operationId), JsonOptions));
        return new(order.Order, attempt.PaymentAttempt, planId, operationId, membershipId);
    }

    private async Task WithDb(Func<MakanDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<MakanDbContext>());
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response) { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!; }
    }
    private static string Callback(PaymentAttemptDto attempt, int success = 1) =>
        CallbackPath + $"?orderId={attempt.PaymentAttemptId:N}&trackId={attempt.ProviderReference}&success={success}&status=2";
    private static string AttemptRoute(PaymentAttemptDto attempt) => $"/api/v1/commerce/payment-attempts/{attempt.PaymentAttemptId}";
    private static string VerifyRoute(PaymentAttemptDto attempt) => AttemptRoute(attempt) + "/verify";

    private sealed class ZibalStub
    {
        private static long _nextTrack = 90000000000;
        private long _lastTrack;
        private int _requestCount;
        private int _verifyCount;
        private readonly ConcurrentDictionary<long, (string OrderId, long Amount)> _payments = new();
        public string? Failure { get; init; }
        public bool InitiationTimeout { get; init; }
        public bool AlreadyVerified { get; init; }
        public int ProviderStatus { get; init; } = 1;
        public int RequestCount => _requestCount;
        public long LastTrackId => Interlocked.Read(ref _lastTrack);
        public int VerifyCount => _verifyCount;
        public ConcurrentBag<string> Paths { get; } = [];
        public HttpResponseMessage Reply(string path, JsonElement request)
        {
            Paths.Add(path);
            if (path == "/v1/request")
            {
                Interlocked.Increment(ref _requestCount);
                var track = Interlocked.Increment(ref _nextTrack);
                Interlocked.Exchange(ref _lastTrack, track);
                _payments[track] = (request.GetProperty("orderId").GetString()!, request.GetProperty("amount").GetInt64());
                if (InitiationTimeout) throw new TaskCanceledException();
                return Json(new { result = 100, trackId = track });
            }
            if (path == "/v1/verify") Interlocked.Increment(ref _verifyCount);
            if (Failure == "timeout") throw new TaskCanceledException();
            if (Failure == "http500") return new(HttpStatusCode.InternalServerError);
            if (Failure == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("private-provider-message") };
            if (path == "/v1/verify" && (AlreadyVerified || Failure == "unknown201" || ProviderStatus != 1)) return Json(new { result = 201 });
            if (Failure == "unknown201") return Json(new { result = 203 });
            var reference = request.GetProperty("trackId").GetInt64();
            if (!_payments.TryGetValue(reference, out var payment)) return Json(new { result = 203 });
            return Json(new
            {
                result = 100, status = ProviderStatus, trackId = Failure == "track" ? reference + 1 : reference,
                amount = Failure == "amount" ? payment.Amount + 1 : payment.Amount,
                orderId = Failure == "order" ? Guid.NewGuid().ToString("N") : payment.OrderId,
                refNumber = Failure == "missing-bank" ? (long?)null : 777123456,
                paidAt = "2026-11-10T12:00:00", cardNumber = "must-not-persist", hashedCardNumber = "must-not-persist",
                message = "private-provider-message"
            });
        }
        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }
    private sealed class ZibalHandler(ZibalStub stub) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return stub.Reply(request.RequestUri!.AbsolutePath, json.RootElement);
        }
    }
    private sealed class FaultingActivation(ISubscriptionActivationHandler inner, Func<Guid, bool> shouldFail) : ISubscriptionActivationHandler
    {
        public Task HandleAsync(SubscriptionActivationRequested payload, CancellationToken cancellationToken) =>
            shouldFail(payload.OrderId) ? throw new InvalidOperationException("خطای کنترل‌شدهٔ تحویل در آزمون") : inner.HandleAsync(payload, cancellationToken);
    }
}
