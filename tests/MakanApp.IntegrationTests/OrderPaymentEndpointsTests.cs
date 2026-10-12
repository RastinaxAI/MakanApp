using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OrderPaymentEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 8_000_000;

    [Fact]
    public async Task ManagerCreatesServerPricedOrderAndIdempotentRetryPreservesFrozenTerms()
    {
        var nowUtc = new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Order Price"));
        var membership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync("پلن قیمت‌دار");
        var firstVersion = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            price: 1_000_000m);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var operationId = Guid.NewGuid();

        using var createdResponse = await client.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new
            {
                planVersionId = firstVersion,
                clientOperationId = operationId,
                amount = 1,
                currency = "USD"
            },
            JsonOptions);
        var created = await ReadAsync<CreateOrderResult>(createdResponse);
        _ = await factory.CreatePublishedPlanVersionAsync(planId, 2, price: 1_500_000m);
        using var replayResponse = await client.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new CreateOrderCommand(firstVersion, operationId),
            JsonOptions);
        var replay = await ReadAsync<CreateOrderResult>(replayResponse);
        using var detailsResponse = await client.GetAsync(
            $"/api/v1/commerce/orders/{created.Order.OrderId}");
        var details = await ReadAsync<OrderDto>(detailsResponse);
        using var listResponse = await client.GetAsync("/api/v1/commerce/orders?limit=20");
        var list = await ReadAsync<OrderListResult>(listResponse);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.False(created.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(created.Order.OrderId, replay.Order.OrderId);
        Assert.Equal(1_000_000m, details.Amount);
        Assert.Equal("IRR", details.Currency);
        Assert.Equal(firstVersion, details.PlanVersionId);
        Assert.Equal(organizationId, details.OrganizationId);
        Assert.Equal(OrderStatus.PendingPayment, details.Status);
        Assert.Contains(list.Items, item => item.OrderId == details.OrderId);
    }

    [Fact]
    public async Task OrderRejectsDraftPlanAndIdempotencyKeyWithDifferentPayload()
    {
        factory.Clock.SetUtcNow(new DateTime(2026, 10, 12, 15, 10, 0, DateTimeKind.Utc));
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Order Validation"));
        var membership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync();
        var first = await factory.CreatePublishedPlanVersionAsync(planId, 1, price: 100m);
        var second = await factory.CreatePublishedPlanVersionAsync(planId, 2, price: 200m);
        var draft = await factory.CreateDraftPricedPlanVersionAsync(planId, 3, 300m);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var operationId = Guid.NewGuid();
        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new CreateOrderCommand(first, operationId),
            JsonOptions);
        using var conflict = await client.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new CreateOrderCommand(second, operationId),
            JsonOptions);
        using var notPurchasable = await client.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new CreateOrderCommand(draft, Guid.NewGuid()),
            JsonOptions);

        firstResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(CommerceErrorCodes.OrderIdempotencyConflict, await ReadProblemCodeAsync(conflict));
        Assert.Equal(HttpStatusCode.Conflict, notPurchasable.StatusCode);
        Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ReadProblemCodeAsync(notPurchasable));
    }

    [Fact]
    public async Task CurrentOrganizationAndManagerRoleProtectOrderReadsAndWrites()
    {
        factory.Clock.SetUtcNow(new DateTime(2026, 10, 12, 15, 20, 0, DateTimeKind.Utc));
        using var managerClient = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        var organizationA = await factory.CreateOrganizationAsync(NewName("Buyer A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("Buyer B"));
        var managerA = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationA,
            OrganizationRole.Manager);
        var managerB = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationB,
            OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync();
        var versionId = await factory.CreatePublishedPlanVersionAsync(planId, 1, price: 100m);
        UseBearerToken(managerClient, manager.AccessToken);
        await SelectWorkspaceAsync(managerClient, managerA.MembershipId, OrganizationRole.Manager);
        var order = await CreateOrderAsync(managerClient, versionId);

        await SelectWorkspaceAsync(managerClient, managerB.MembershipId, OrganizationRole.Manager);
        using var concealed = await managerClient.GetAsync($"/api/v1/commerce/orders/{order.Order.OrderId}");
        Assert.Equal(HttpStatusCode.NotFound, concealed.StatusCode);
        foreach (var role in new[]
                 {
                     OrganizationRole.Teacher,
                     OrganizationRole.Student,
                     OrganizationRole.Parent
                 })
        {
            using var client = factory.CreateClient();
            var user = await CreateAuthenticatedUserAsync(client);
            var membership = await factory.CreateMembershipAsync(user.User.Id, organizationA, role);
            UseBearerToken(client, user.AccessToken);
            await SelectWorkspaceAsync(client, membership.MembershipId, role);
            using var forbidden = await client.PostAsJsonAsync(
                "/api/v1/commerce/orders",
                new CreateOrderCommand(versionId, Guid.NewGuid()),
                JsonOptions);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Equal(
                CommerceErrorCodes.OrderManagementNotAllowed,
                await ReadProblemCodeAsync(forbidden));
        }
    }

    [Fact]
    public async Task PaymentInitiationUsesServerValuesAndSameOperationCreatesOneAttempt()
    {
        var setup = await CreateManagerOrderAsync(250_000m, 30);
        var operationId = Guid.NewGuid();

        using var firstResponse = await setup.Client.PostAsJsonAsync(
            $"/api/v1/commerce/orders/{setup.Order.Order.OrderId}/payments",
            new InitiatePaymentCommand(operationId),
            JsonOptions);
        var first = await ReadAsync<InitiatePaymentResult>(firstResponse);
        using var replayResponse = await setup.Client.PostAsJsonAsync(
            $"/api/v1/commerce/orders/{setup.Order.Order.OrderId}/payments",
            new InitiatePaymentCommand(operationId),
            JsonOptions);
        var replay = await ReadAsync<InitiatePaymentResult>(replayResponse);
        var gatewayRequest = Assert.Single(factory.PaymentGateway.CreationRequests);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(first.PaymentAttempt.PaymentAttemptId, replay.PaymentAttempt.PaymentAttemptId);
        Assert.Equal(PaymentAttemptStatus.PendingVerification, first.PaymentAttempt.Status);
        Assert.Equal(250_000m, gatewayRequest.Amount);
        Assert.Equal(CurrencyCode.Irr, gatewayRequest.Currency);
        Assert.Equal(setup.Order.Order.OrderId, gatewayRequest.OrderId);
        Assert.StartsWith("https://payments.test/", first.PaymentAttempt.PaymentUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(first, JsonOptions), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await factory.CountPaymentAttemptsAsync(setup.Order.Order.OrderId));
    }

    [Fact]
    public async Task ProviderFailureAndTimeoutPreserveAttemptsWithoutPayingOrder()
    {
        var setup = await CreateManagerOrderAsync(300_000m, 40);
        factory.PaymentGateway.EnqueueCreation(PaymentCreationBehavior.Failed);
        factory.PaymentGateway.EnqueueCreation(PaymentCreationBehavior.Timeout);

        var failed = await InitiateAsync(setup.Client, setup.Order.Order.OrderId, Guid.NewGuid());
        var unknown = await InitiateAsync(setup.Client, setup.Order.Order.OrderId, Guid.NewGuid());
        using var detailsResponse = await setup.Client.GetAsync(
            $"/api/v1/commerce/orders/{setup.Order.Order.OrderId}");
        var details = await ReadAsync<OrderDto>(detailsResponse);

        Assert.Equal(PaymentAttemptStatus.Failed, failed.PaymentAttempt.Status);
        Assert.Equal(PaymentAttemptStatus.Unknown, unknown.PaymentAttempt.Status);
        Assert.Equal(OrderStatus.PendingPayment, details.Status);
        Assert.Equal(2, await factory.CountPaymentAttemptsAsync(details.OrderId));
        Assert.Equal(0, await factory.CountPaymentsAsync(details.OrderId));
    }

    [Fact]
    public async Task ProviderReferenceCannotBeReusedAcrossAttempts()
    {
        var setup = await CreateManagerOrderAsync(350_000m, 45);
        factory.PaymentGateway.CreationProviderReference = "shared-provider-reference";
        var secondOrder = await CreateOrderAsync(setup.Client, setup.Order.Order.PlanVersionId);

        _ = await InitiateAsync(setup.Client, setup.Order.Order.OrderId, Guid.NewGuid());
        using var conflict = await setup.Client.PostAsJsonAsync(
            $"/api/v1/commerce/orders/{secondOrder.Order.OrderId}/payments",
            new InitiatePaymentCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(CommerceErrorCodes.PaymentReferenceConflict, await ReadProblemCodeAsync(conflict));
        Assert.Equal(1, await factory.CountPaymentAttemptsAsync(secondOrder.Order.OrderId));
        Assert.Equal(0, await factory.CountPaymentsAsync(secondOrder.Order.OrderId));
    }

    [Fact]
    public async Task VerificationChecksReferenceAmountCurrencyAndProviderOutcome()
    {
        var amountMismatch = await CreateInitiatedAttemptAsync(500m, 50);
        factory.PaymentGateway.SetVerificationResult(new PaymentGatewayVerificationResult(
            PaymentGatewayVerificationOutcome.Succeeded,
            amountMismatch.Attempt.ProviderReference,
            499m,
            CurrencyCode.Irr,
            null));
        var amountResult = await VerifyAsync(amountMismatch.Attempt.PaymentAttemptId);

        var currencyMismatch = await CreateInitiatedAttemptAsync(600m, 60);
        factory.PaymentGateway.SetVerificationResult(new PaymentGatewayVerificationResult(
            PaymentGatewayVerificationOutcome.Succeeded,
            currencyMismatch.Attempt.ProviderReference,
            600m,
            (CurrencyCode)999,
            null));
        var currencyResult = await VerifyAsync(currencyMismatch.Attempt.PaymentAttemptId);

        var providerFailed = await CreateInitiatedAttemptAsync(700m, 70);
        factory.PaymentGateway.SetVerificationResult(new PaymentGatewayVerificationResult(
            PaymentGatewayVerificationOutcome.Failed,
            providerFailed.Attempt.ProviderReference,
            null,
            null,
            "PAYMENT_PROVIDER_DECLINED"));
        var failedResult = await VerifyAsync(providerFailed.Attempt.PaymentAttemptId);

        var providerUnknown = await CreateInitiatedAttemptAsync(800m, 80);
        factory.PaymentGateway.SetVerificationResult(new PaymentGatewayVerificationResult(
            PaymentGatewayVerificationOutcome.Unknown,
            providerUnknown.Attempt.ProviderReference,
            null,
            null,
            CommerceErrorCodes.PaymentPendingVerification));
        var unknownResult = await VerifyAsync(providerUnknown.Attempt.PaymentAttemptId);

        Assert.Equal(PaymentVerificationStatus.AmountMismatch, amountResult.Status);
        Assert.Equal(PaymentVerificationStatus.CurrencyMismatch, currencyResult.Status);
        Assert.Equal(PaymentVerificationStatus.Failed, failedResult.Status);
        Assert.Equal(PaymentVerificationStatus.Unknown, unknownResult.Status);
        Assert.All(
            new[] { amountResult, currencyResult, failedResult, unknownResult },
            result => Assert.Equal(OrderStatus.PendingPayment, result.Order.Status));
    }

    [Fact]
    public async Task InvalidObservedReferenceIsRejectedBeforeProviderVerification()
    {
        var setup = await CreateInitiatedAttemptAsync(900m, 90);

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPaymentVerificationService>();
        var exception = await Assert.ThrowsAsync<CommerceException>(() =>
            service.VerifyPaymentAsync(
                new VerifyPaymentCommand(setup.Attempt.PaymentAttemptId, "different-reference"),
                default));

        Assert.Equal(CommerceErrorCodes.PaymentReferenceInvalid, exception.Code);
        Assert.Empty(factory.PaymentGateway.VerificationRequests);
        Assert.Equal(0, await factory.CountPaymentsAsync(setup.Order.Order.OrderId));
    }

    [Fact]
    public async Task TenDuplicateVerificationsCreateOneSettlementOutboxAndSubscription()
    {
        var setup = await CreateInitiatedAttemptAsync(1_000m, 100);

        var first = await VerifyAsync(setup.Attempt.PaymentAttemptId);
        var repeats = new List<VerifyPaymentResult>();
        for (var index = 0; index < 9; index++)
        {
            repeats.Add(await VerifyAsync(setup.Attempt.PaymentAttemptId));
        }

        Assert.Equal(PaymentVerificationStatus.Settled, first.Status);
        Assert.All(repeats, result => Assert.Equal(PaymentVerificationStatus.AlreadySettled, result.Status));
        Assert.Equal(1, await factory.CountPaymentsAsync(setup.Order.Order.OrderId));
        Assert.Equal(1, await factory.CountCommerceActivationOutboxAsync(setup.Order.Order.OrderId));

        _ = await factory.DrainNotificationOutboxAsync();

        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.Order.OrderId));
        using var response = await setup.Client.GetAsync(
            $"/api/v1/commerce/orders/{setup.Order.Order.OrderId}");
        var details = await ReadAsync<OrderDto>(response);
        Assert.Equal(OrderFulfillmentState.SubscriptionActivated, details.FulfillmentState);
    }

    [Fact]
    public async Task ConcurrentSuccessfulVerificationSettlesExactlyOnce()
    {
        var setup = await CreateInitiatedAttemptAsync(1_100m, 110);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = VerifyAfterGateAsync(setup.Attempt.PaymentAttemptId, gate.Task);
        var second = VerifyAfterGateAsync(setup.Attempt.PaymentAttemptId, gate.Task);
        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Status == PaymentVerificationStatus.Settled);
        Assert.Single(results, result => result.Status == PaymentVerificationStatus.AlreadySettled);
        Assert.Equal(1, await factory.CountPaymentsAsync(setup.Order.Order.OrderId));
        Assert.Equal(1, await factory.CountCommerceActivationOutboxAsync(setup.Order.Order.OrderId));
        _ = await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.Order.OrderId));
    }

    [Fact]
    public async Task FailedAttemptThenSuccessfulAttemptRetainsHistoryAndChangesSubscriptionOnce()
    {
        var setup = await CreateManagerOrderAsync(1_200m, 120);
        factory.PaymentGateway.EnqueueCreation(PaymentCreationBehavior.Failed);
        factory.PaymentGateway.EnqueueCreation(PaymentCreationBehavior.Pending);
        var first = await InitiateAsync(setup.Client, setup.Order.Order.OrderId, Guid.NewGuid());
        var second = await InitiateAsync(setup.Client, setup.Order.Order.OrderId, Guid.NewGuid());

        var verified = await VerifyAsync(second.PaymentAttempt.PaymentAttemptId);
        _ = await factory.DrainNotificationOutboxAsync();

        Assert.Equal(PaymentAttemptStatus.Failed, first.PaymentAttempt.Status);
        Assert.Equal(PaymentVerificationStatus.Settled, verified.Status);
        Assert.Equal(OrderStatus.Paid, verified.Order.Status);
        Assert.Equal(2, await factory.CountPaymentAttemptsAsync(setup.Order.Order.OrderId));
        Assert.Equal(1, await factory.CountPaymentsAsync(setup.Order.Order.OrderId));
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.Order.OrderId));
    }

    [Fact]
    public async Task PaidDowngradePreservesExistingLearnersAndExposesOverQuota()
    {
        factory.PaymentGateway.Reset();
        var nowUtc = new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Paid Downgrade"));
        var membership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        var academicClass = await factory.CreateAcademicClassAsync(organizationId, capacity: 10);
        var learners = await factory.CreateOrganizationPersonsAsync(organizationId, 2);
        foreach (var learner in learners)
        {
            _ = await factory.CreateEnrollmentAsync(organizationId, academicClass.ClassId, learner);
        }

        var planId = await factory.CreateCommercePlanAsync("Paid downgrade plan");
        var highVersion = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ActiveLearners] = 10 },
            price: 2_000m);
        _ = await factory.CreateSubscriptionAsync(
            organizationId,
            highVersion,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-1),
            nowUtc.AddDays(30));
        var lowVersion = await factory.CreatePublishedPlanVersionAsync(
            planId,
            2,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ActiveLearners] = 1 },
            price: 1_000m);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var order = await CreateOrderAsync(client, lowVersion);
        var attempt = await InitiateAsync(client, order.Order.OrderId, Guid.NewGuid());

        _ = await VerifyAsync(attempt.PaymentAttempt.PaymentAttemptId);
        _ = await factory.DrainNotificationOutboxAsync();
        using var summaryResponse = await client.GetAsync("/api/v1/commerce/summary");
        var summary = await ReadAsync<ManagerCommerceSummaryDto>(summaryResponse);

        Assert.Equal(lowVersion, summary.Subscription!.PlanVersionId);
        Assert.True(Assert.Single(summary.Quotas).IsOverQuota);
        Assert.Equal(2, await factory.CountActiveEnrollmentsAsync(organizationId, academicClass.ClassId));
    }

    [Fact]
    public async Task ManagerCannotManuallyVerifyOrActivatePayment()
    {
        var setup = await CreateManagerOrderAsync(1_300m, 130);

        using var verify = await setup.Client.PostAsJsonAsync(
            "/api/v1/payments/Recording/callback",
            new { status = "success", amount = 1_300m },
            JsonOptions);
        using var activate = await setup.Client.PostAsJsonAsync(
            $"/api/v1/commerce/orders/{setup.Order.Order.OrderId}/activate",
            new { },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, verify.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, activate.StatusCode);
        Assert.Equal(0, await factory.CountPaymentsAsync(setup.Order.Order.OrderId));
    }

    private async Task<(HttpClient Client, CreateOrderResult Order)> CreateManagerOrderAsync(
        decimal price,
        int clockMinute)
    {
        factory.PaymentGateway.Reset();
        factory.Clock.SetUtcNow(new DateTime(2026, 10, 12, 16, clockMinute % 60, 0, DateTimeKind.Utc));
        var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Payment"));
        var membership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync();
        var versionId = await factory.CreatePublishedPlanVersionAsync(planId, 1, price: price);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var order = await CreateOrderAsync(client, versionId);
        return (client, order);
    }

    private async Task<(HttpClient Client, CreateOrderResult Order, PaymentAttemptDto Attempt)>
        CreateInitiatedAttemptAsync(decimal price, int clockMinute)
    {
        var setup = await CreateManagerOrderAsync(price, clockMinute);
        var initiation = await InitiateAsync(
            setup.Client,
            setup.Order.Order.OrderId,
            Guid.NewGuid());
        return (setup.Client, setup.Order, initiation.PaymentAttempt);
    }

    private static async Task<CreateOrderResult> CreateOrderAsync(
        HttpClient client,
        Guid planVersionId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new CreateOrderCommand(planVersionId, Guid.NewGuid()),
            JsonOptions);
        return await ReadAsync<CreateOrderResult>(response);
    }

    private static async Task<InitiatePaymentResult> InitiateAsync(
        HttpClient client,
        Guid orderId,
        Guid clientOperationId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/commerce/orders/{orderId}/payments",
            new InitiatePaymentCommand(clientOperationId),
            JsonOptions);
        return await ReadAsync<InitiatePaymentResult>(response);
    }

    private async Task<VerifyPaymentResult> VerifyAsync(Guid attemptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPaymentVerificationService>()
            .VerifyPaymentAsync(new VerifyPaymentCommand(attemptId, null), default);
    }

    private async Task<VerifyPaymentResult> VerifyAfterGateAsync(Guid attemptId, Task gate)
    {
        await gate;
        return await VerifyAsync(attemptId);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98936{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await ReadAsync<RequestOtpResult>(challengeResponse);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        return await ReadAsync<VerifyOtpResult>(verifyResponse);
    }

    private static async Task SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Task InitializeAsync()
    {
        factory.PaymentGateway.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        factory.PaymentGateway.Reset();
        factory.Clock.UseSystemTime();
        return Task.CompletedTask;
    }
}
