using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class ZibalPaymentEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    [Fact]
    public async Task CallbackSettlesOnceAndReceiptUsesFrozenTitlePriceAndSeparateBankReference()
    {
        using var host = Host(); using var client = Client(host);
        var setup = await SetupAsync(client);
        for (var i = 0; i < 10; i++)
        {
            using var response = await client.GetAsync(Callback(setup.Attempt));
            Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
            Assert.StartsWith("https://client.example.test/payment-return?", response.Headers.Location!.AbsoluteUri);
            Assert.DoesNotContain("success", response.Headers.Location.AbsoluteUri);
            Assert.DoesNotContain("zibal", response.Headers.Location.AbsoluteUri);
        }
        await AssertSettlementAsync(setup.Order.OrderId, 1);
        using var detailsResponse = await client.GetAsync($"/api/v1/commerce/payment-attempts/{setup.Attempt.PaymentAttemptId}");
        var details = await ReadAsync<PaymentAttemptDetailsDto>(detailsResponse);
        Assert.Equal(PaymentAttemptStatus.Succeeded, details.PaymentAttempt.Status);
        Assert.Equal(10, details.History.Count(e => e.Type == PaymentAttemptEventType.CallbackReceived));
        Assert.Contains(details.History, e => e.Type == PaymentAttemptEventType.SettlementReplayed);
        Assert.Contains(details.History, e => !string.IsNullOrWhiteSpace(e.CorrelationId));
        await WithDb(async db =>
        {
            await db.Plans.Where(p => p.Id == setup.PlanId).ExecuteUpdateAsync(p => p.SetProperty(x => x.DisplayName, "عنوان تازه"));
        });
        var receipt = await ReadAsync<PaymentReceiptDto>(await client.GetAsync($"/api/v1/commerce/orders/{setup.Order.OrderId}/receipt"));
        Assert.Equal("عنوان تاریخی خرید", receipt.ProductTitle); Assert.Equal(1000m, receipt.Amount);
        Assert.Equal(setup.Attempt.ProviderReference, receipt.ProviderReference);
        Assert.Equal("777123456", receipt.SettlementReference);
        Assert.NotEqual(receipt.ProviderReference, receipt.SettlementReference);
        Assert.Null(receipt.ProviderPaidAtUtc);
        Assert.Equal(OrderFulfillmentState.SubscriptionActivationPending, receipt.FulfillmentState);
        await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
        Assert.DoesNotContain("private-provider-message", JsonSerializer.Serialize(details, JsonOptions));
    }

    [Fact]
    public async Task Verify201RecoversServerPaidTransactionAfterLocalCrashAndNoCallback()
    {
        var stub = new ZibalStub { AlreadyVerified = true };
        using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client);
        var recovered = await ReadAsync<VerifyPaymentResult>(await client.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(PaymentVerificationStatus.Settled, recovered.Status);
        Assert.Contains(stub.Paths, p => p == "/v1/inquiry");
        Assert.Equal("777123456", recovered.PaymentAttempt.SettlementReference);
        var repeated = await ReadAsync<VerifyPaymentResult>(await client.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(PaymentVerificationStatus.AlreadySettled, repeated.Status);
        Assert.Equal(1, stub.VerifyCount);
        var history = await ReadAsync<PaymentAttemptDetailsDto>(await client.GetAsync(AttemptRoute(setup.Attempt)));
        Assert.Contains(history.History, h => h.Type == PaymentAttemptEventType.InquiryCompleted && h.ProviderResult == 100);
        Assert.Contains(history.History, h => h.ProviderResult == 201);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
    }

    [Fact]
    public async Task ConcurrentCallbackAndOwnerRecoveryProduceOnePaymentAndOneOutbox()
    {
        using var host = Host(); using var client = Client(host);
        var setup = await SetupAsync(client);
        var responses = await Task.WhenAll(client.GetAsync(Callback(setup.Attempt)), client.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(HttpStatusCode.SeeOther, responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        foreach (var response in responses) response.Dispose();
        await AssertSettlementAsync(setup.Order.OrderId, 1);
    }

    [Theory]
    [InlineData("amount")] [InlineData("order")] [InlineData("track")]
    [InlineData("missing-bank")] [InlineData("malformed")] [InlineData("timeout")]
    [InlineData("http500")] [InlineData("unknown201")]
    [InlineData("inquiry500")] [InlineData("empty")]
    public async Task InvalidProviderProofNeverPaysOrActivates(string failure)
    {
        var stub = new ZibalStub { Failure = failure };
        using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client);
        using var response = await client.GetAsync(Callback(setup.Attempt));
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var details = await ReadAsync<PaymentAttemptDetailsDto>(await client.GetAsync(AttemptRoute(setup.Attempt)));
        Assert.Equal(OrderStatus.PendingPayment, details.Order.Status);
        Assert.NotEqual(PaymentAttemptStatus.Succeeded, details.PaymentAttempt.Status);
        await AssertSettlementAsync(setup.Order.OrderId, 0);
        Assert.Equal(0, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
    }

    [Theory]
    [InlineData(3)] [InlineData(-1)] [InlineData(-2)] [InlineData(2)] [InlineData(5)]
    public async Task CancelHintIsNeverAuthorityAndProviderStateIsConservative(int status)
    {
        var stub = new ZibalStub { ProviderStatus = status };
        using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client);
        using var response = await client.GetAsync(Callback(setup.Attempt, success: 1));
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var details = await ReadAsync<PaymentAttemptDetailsDto>(await client.GetAsync(AttemptRoute(setup.Attempt)));
        Assert.Equal(status is 3 or 5 ? PaymentAttemptStatus.Failed : PaymentAttemptStatus.Unknown, details.PaymentAttempt.Status);
        await AssertSettlementAsync(setup.Order.OrderId, 0);
    }

    [Fact]
    public async Task FailedCallbackHintStillVerifiesRealPaidTransaction()
    {
        using var host = Host(); using var client = Client(host);
        var setup = await SetupAsync(client);
        using var response = await client.GetAsync(Callback(setup.Attempt, success: 0));
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
    }

    [Fact]
    public async Task FakeCallbackUnknownAttemptOrWrongTrackNeverCallsProvider()
    {
        var stub = new ZibalStub(); using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client);
        foreach (var path in new[]
        {
            Callback(setup.Attempt).Replace(setup.Attempt.ProviderReference!, "99887766", StringComparison.Ordinal),
            Callback(setup.Attempt).Replace(setup.Attempt.PaymentAttemptId.ToString("N"), Guid.NewGuid().ToString("N"), StringComparison.Ordinal)
        })
        {
            using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, stub.VerifyCount); await AssertSettlementAsync(setup.Order.OrderId, 0);
    }

    [Theory]
    [InlineData("success=garbage&status=2&trackId=1")]
    [InlineData("success=1&status=garbage&trackId=1")]
    [InlineData("success=1&status=2")]
    [InlineData("success=1&status=2&trackId=0")]
    [InlineData("success=1&status=2&trackId=1&trackId=2")]
    public async Task MalformedCallbackIsSafeAndAnonymous(string query)
    {
        using var host = Host(); using var client = Client(host);
        using var response = await client.GetAsync(CallbackPath + "?orderId=" + Guid.NewGuid().ToString("N") + "&" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("garbage", body);
        Assert.Contains("correlationId", body); Assert.DoesNotContain("merchant", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonalOwnerReadRecoveryAndReceiptConcealOtherAccountsAndRequireSession()
    {
        using var host = Host(); using var owner = Client(host); using var stranger = Client(host); using var anonymous = Client(host);
        var setup = await SetupAsync(owner, personal: true); await AuthenticateAsync(stranger);
        foreach (var client in new[] { stranger, anonymous })
        {
            var expected = client == anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound;
            using var get = await client.GetAsync(AttemptRoute(setup.Attempt)); Assert.Equal(expected, get.StatusCode);
            using var verify = await client.PostAsync(VerifyRoute(setup.Attempt), null); Assert.Equal(expected, verify.StatusCode);
            using var receipt = await client.GetAsync($"/api/v1/commerce/personal/orders/{setup.Order.OrderId}/receipt"); Assert.Equal(expected, receipt.StatusCode);
        }
        using var verified = await owner.PostAsJsonAsync(VerifyRoute(setup.Attempt), new { amount = 1, providerReference = "different", callbackUrl = "https://evil.example" });
        var paid = await ReadAsync<VerifyPaymentResult>(verified); Assert.Equal(1000, paid.Order.Amount);
        using var receiptOk = await owner.GetAsync($"/api/v1/commerce/personal/orders/{setup.Order.OrderId}/receipt");
        Assert.Equal(HttpStatusCode.OK, receiptOk.StatusCode);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
    }

    [Fact]
    public async Task OrganizationManagerCannotReadOtherOrganizationAttemptOrRecoverAfterRoleRevoked()
    {
        using var host = Host(); using var client = Client(host); using var other = Client(host);
        var setup = await SetupAsync(client); await SetupAsync(other);
        using var denied = await other.GetAsync(AttemptRoute(setup.Attempt)); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var verify = await other.PostAsync(VerifyRoute(setup.Attempt), null); Assert.Equal(HttpStatusCode.NotFound, verify.StatusCode);
        await WithDb(db => db.RoleAssignments.Where(r => r.MembershipId == setup.MembershipId).ExecuteDeleteAsync());
        using var revoked = await client.PostAsync(VerifyRoute(setup.Attempt), null);
        Assert.False(revoked.IsSuccessStatusCode); await AssertSettlementAsync(setup.Order.OrderId, 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PendingAttemptPreventsNewChargeAndRetryUsesSameAttemptAfterDefaultProviderSwitch(bool legacyHash)
    {
        var stub = new ZibalStub(); using var first = Host(stub: stub); using var client = Client(first);
        var setup = await SetupAsync(client);
        if (legacyHash)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"payment|{setup.Order.OrderId:N}|Zibal"));
            await WithDb(db => db.PaymentAttempts.Where(a => a.Id == setup.Attempt.PaymentAttemptId)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.RequestPayloadHash, hash)));
        }
        using var blocked = await client.PostAsJsonAsync($"/api/v1/commerce/orders/{setup.Order.OrderId}/payments", new InitiatePaymentCommand(Guid.NewGuid()), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var switched = Host(defaultUnavailable: true, stub: stub); using var resumed = Client(switched);
        resumed.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var replay = await ReadAsync<InitiatePaymentResult>(await resumed.PostAsJsonAsync($"/api/v1/commerce/orders/{setup.Order.OrderId}/payments",
            new InitiatePaymentCommand(setup.OperationId), JsonOptions));
        Assert.True(replay.Replayed); Assert.Equal(setup.Attempt.PaymentAttemptId, replay.PaymentAttempt.PaymentAttemptId);
        var recovered = await ReadAsync<VerifyPaymentResult>(await resumed.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(PaymentVerificationStatus.Settled, recovered.Status); Assert.Equal("Zibal", recovered.PaymentAttempt.Provider);
        Assert.Equal(1, stub.RequestCount); await AssertSettlementAsync(setup.Order.OrderId, 1);
    }

    [Fact]
    public async Task UnknownInitiationAndExpiredClaimDoNotBlindlyRepeatRequest()
    {
        var stub = new ZibalStub { InitiationTimeout = true }; using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client);
        Assert.Equal(PaymentAttemptStatus.Unknown, setup.Attempt.Status);
        var replay = await ReadAsync<InitiatePaymentResult>(await client.PostAsJsonAsync($"/api/v1/commerce/orders/{setup.Order.OrderId}/payments",
            new InitiatePaymentCommand(setup.OperationId), JsonOptions));
        Assert.True(replay.Replayed); Assert.Equal(1, stub.RequestCount);
        using var blocked = await client.PostAsJsonAsync($"/api/v1/commerce/orders/{setup.Order.OrderId}/payments", new InitiatePaymentCommand(Guid.NewGuid()), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var proof = await ReadAsync<VerifyPaymentResult>(await client.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(PaymentVerificationStatus.Unknown, proof.Status);
        await AssertSettlementAsync(setup.Order.OrderId, 0);
    }

    [Fact]
    public async Task PaymentAndHistoryAreAppendOnlyAndMigrationIsAdditive()
    {
        using var host = Host(); using var client = Client(host); var setup = await SetupAsync(client);
        using var response = await client.GetAsync(Callback(setup.Attempt)); Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.OrderId == setup.Order.OrderId);
        Assert.Equal("777123456", payment.SettlementReference);
        Assert.NotEmpty((await db.PaymentAttempts.SingleAsync(a => a.Id == setup.Attempt.PaymentAttemptId)).RowVersion);
        db.Payments.Remove(payment); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); var history = await db.PaymentAttemptEvents.FirstAsync(e => e.PaymentAttemptId == setup.Attempt.PaymentAttemptId);
        db.PaymentAttemptEvents.Remove(history); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), name => name.EndsWith("_AddZibalPaymentIntegration", StringComparison.Ordinal));
    }

    private async Task AssertSettlementAsync(Guid orderId, int count)
    {
        Assert.Equal(count, await factory.CountPaymentsAsync(orderId));
        Assert.Equal(count, await factory.CountCommerceActivationOutboxAsync(orderId));
    }

    [Fact]
    public async Task ForgedCallbackCannotBindReferenceToUncertainAttemptAndGenuineCallbackRecoversIt()
    {
        var stub = new ZibalStub { InitiationTimeout = true }; using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client); Assert.Null(setup.Attempt.ProviderReference);
        var path = CallbackPath + $"?orderId={setup.Attempt.PaymentAttemptId:N}&trackId=998877&success=1&status=2";
        using var forged = await client.GetAsync(path); Assert.Equal(HttpStatusCode.SeeOther, forged.StatusCode);
        var unbound = await ReadAsync<PaymentAttemptDetailsDto>(await client.GetAsync(AttemptRoute(setup.Attempt)));
        Assert.Null(unbound.PaymentAttempt.ProviderReference); Assert.Equal(OrderStatus.PendingPayment, unbound.Order.Status);
        using var genuine = await client.GetAsync(path.Replace("998877", stub.LastTrackId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.SeeOther, genuine.StatusCode); await AssertSettlementAsync(setup.Order.OrderId, 1);
        Assert.Equal(1, stub.RequestCount);
    }

    [Fact]
    public async Task PaidOrderSurvivesActivationFailureAndOutboxRetryDoesNotRecharge()
    {
        var stub = new ZibalStub(); using var host = Host(stub: stub, failActivationOnce: true); using var client = Client(host);
        var setup = await SetupAsync(client);
        _activationFailureTarget = setup.Order.OrderId;
        using var callback = await client.GetAsync(Callback(setup.Attempt)); Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
        await using (var scope = host.Services.CreateAsyncScope())
            _ = await scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Outbox.OutboxProcessor>().DispatchBatchAsync(default);
        Assert.Equal(0, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
        var pending = await ReadAsync<PaymentReceiptDto>(await client.GetAsync($"/api/v1/commerce/orders/{setup.Order.OrderId}/receipt"));
        Assert.Equal(OrderFulfillmentState.SubscriptionActivationPending, pending.FulfillmentState);
        var replay = await ReadAsync<VerifyPaymentResult>(await client.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(PaymentVerificationStatus.AlreadySettled, replay.Status);
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(10));
        await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
        Assert.Equal(1, stub.RequestCount); Assert.Equal(1, stub.VerifyCount);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
    }

    [Fact]
    public async Task UnconfiguredCallbackReturnsSafeUnavailableWithoutSettlement()
    {
        using var first = Host(); using var client = Client(first); var setup = await SetupAsync(client);
        using var unavailable = Host(zibalUnavailable: true); using var anonymous = Client(unavailable);
        using var response = await anonymous.GetAsync(Callback(setup.Attempt));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.Contains(CommerceErrorCodes.PaymentProviderUnavailable, body);
        Assert.DoesNotContain("Merchant", body); await AssertSettlementAsync(setup.Order.OrderId, 0);
    }
    public Task InitializeAsync() { factory.Clock.SetUtcNow(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)); return Task.CompletedTask; }
    public Task DisposeAsync() { factory.Clock.UseSystemTime(); return Task.CompletedTask; }

    [Theory]
    [InlineData(null)] [InlineData("malformed")] [InlineData("http500")]
    public async Task MerchantAndPrivateProviderPayloadNeverEnterResponsesHistoryOrLogs(string? failure)
    {
        var marker = "test-only-" + Guid.NewGuid().ToString("N");
        using var host = Host(stub: new ZibalStub { Failure = failure }, merchant: marker); using var client = Client(host);
        var setup = await SetupAsync(client);
        var callbackMarker = "callback-private-" + Guid.NewGuid().ToString("N");
        using var callback = await client.GetAsync(Callback(setup.Attempt) + "&privateDump=" + callbackMarker); Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
        using var details = await client.GetAsync(AttemptRoute(setup.Attempt)); details.EnsureSuccessStatusCode();
        var response = await details.Content.ReadAsStringAsync();
        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        var captured = string.Join("\n", logs.Snapshot()) + JsonSerializer.Serialize(logs.Entries(), JsonOptions);
        foreach (var privateValue in new[] { marker, callbackMarker, client.DefaultRequestHeaders.Authorization!.Parameter!, "must-not-persist", "private-provider-message" })
        {
            Assert.DoesNotContain(privateValue, response, StringComparison.Ordinal);
            Assert.DoesNotContain(privateValue, captured, StringComparison.Ordinal);
        }
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        foreach (var type in new[] { typeof(Payment), typeof(PaymentAttempt), typeof(PaymentAttemptEvent) })
            Assert.DoesNotContain(db.Model.FindEntityType(type)!.GetProperties(), p => p.Name is "Merchant" or "CardNumber" or "HashedCardNumber" or "RawResponse");
    }

    [Fact]
    public async Task OpenApiSeparatesAnonymousCallbackFromAuthenticatedRecoveryAndNeverExposesMerchant()
    {
        using var host = Host(); using var client = Client(host);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var callback = paths.GetProperty(CallbackPath).GetProperty("get");
        Assert.Empty(callback.GetProperty("security")[0].EnumerateObject());
        Assert.True(callback.GetProperty("responses").TryGetProperty("303", out _));
        Assert.Equal(4, callback.GetProperty("parameters").GetArrayLength());
        Assert.DoesNotContain(callback.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "returnUrl");
        foreach (var route in new[] { "/api/v1/commerce/payment-attempts/{attemptId}", "/api/v1/commerce/payment-attempts/{attemptId}/verify",
            "/api/v1/commerce/orders/{orderId}/receipt", "/api/v1/commerce/personal/orders/{orderId}/receipt" })
        {
            var operation = paths.GetProperty(route).GetProperty(route.EndsWith("verify", StringComparison.Ordinal) ? "post" : "get");
            Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
        }
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.False(schemas.TryGetProperty("ZibalOptions", out _));
        Assert.False(schemas.TryGetProperty("PaymentAttemptEvent", out _));
    }
}
