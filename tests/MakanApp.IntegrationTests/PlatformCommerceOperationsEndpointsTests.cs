using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PlatformCommerceOperationsEndpointsTests(
    MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 7_500_000;

    [Fact]
    public async Task OwnerVersionsPlansAndSubscriptionOverridePreservesHistory()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var organizationId = await factory.CreateOrganizationAsync($"Commerce {Guid.NewGuid():N}");

        using var createPlanResponse = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/commerce/plans",
            new CreatePlatformPlanCommand(
                $"platform.{Guid.NewGuid():N}",
                "پلن عملیاتی",
                "ایجاد پلن برای آزمون نسخه‌بندی"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createPlanResponse.StatusCode);
        var plan = await ReadAsync<PlatformPlanDetail>(createPlanResponse);
        var firstVersion = await CreateAndPublishVersionAsync(
            ownerClient,
            plan.PlanId,
            1_000_000m,
            100,
            "انتشار نسخه اول");
        var firstSubscriptionId = await factory.CreateSubscriptionAsync(
            organizationId,
            firstVersion.PlanVersionId,
            SubscriptionStatus.Active,
            factory.Clock.GetUtcNow().UtcDateTime,
            factory.Clock.GetUtcNow().UtcDateTime.AddDays(30));
        var secondVersion = await CreateAndPublishVersionAsync(
            ownerClient,
            plan.PlanId,
            1_500_000m,
            200,
            "انتشار نسخه دوم");

        factory.PlatformStepUp.SetSatisfied(false);
        using var denied = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/commerce/organizations/{organizationId}/subscription-override",
            new OverridePlatformSubscriptionCommand(
                secondVersion.PlanVersionId,
                factory.Clock.GetUtcNow().UtcDateTime.AddDays(60),
                "ارتقای کنترل‌شده اشتراک"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        factory.PlatformStepUp.SetSatisfied(true);
        using var overrideResponse = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/commerce/organizations/{organizationId}/subscription-override",
            new OverridePlatformSubscriptionCommand(
                secondVersion.PlanVersionId,
                factory.Clock.GetUtcNow().UtcDateTime.AddDays(60),
                "ارتقای کنترل‌شده اشتراک"),
            JsonOptions);
        var changed = await ReadAsync<PlatformSubscriptionOverrideResult>(overrideResponse);
        var subscriptions = await ownerClient.GetFromJsonAsync<PlatformSubscriptionPage>(
            $"/api/admin/v1/commerce/subscriptions?organizationId={organizationId:D}&page=1&pageSize=10",
            JsonOptions);
        var detail = await ownerClient.GetFromJsonAsync<PlatformPlanDetail>(
            $"/api/admin/v1/commerce/plans/{plan.PlanId:D}",
            JsonOptions);

        Assert.Equal(firstSubscriptionId, changed.ReplacedSubscriptionId);
        Assert.Equal(secondVersion.PlanVersionId, changed.Subscription.PlanVersionId);
        Assert.Equal(2, subscriptions!.TotalCount);
        Assert.Contains(subscriptions.Items, item =>
            item.SubscriptionId == firstSubscriptionId &&
            item.PlanVersionId == firstVersion.PlanVersionId &&
            item.Status == SubscriptionStatus.Cancelled);
        Assert.Contains(subscriptions.Items, item =>
            item.SubscriptionId == changed.Subscription.SubscriptionId &&
            item.PlanVersionId == secondVersion.PlanVersionId &&
            item.Status == SubscriptionStatus.Active);
        Assert.Contains(detail!.Versions, item =>
            item.PlanVersionId == firstVersion.PlanVersionId &&
            item.PriceAmount == 1_000_000m &&
            item.SubscriptionCount == 1);
        Assert.Contains(detail.Versions, item =>
            item.PlanVersionId == secondVersion.PlanVersionId &&
            item.PriceAmount == 1_500_000m &&
            item.SubscriptionCount == 1);
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.commerce.plan.create"));
        Assert.Equal(2, await factory.CountAdminAuditLogsAsync("platform.commerce.plan-version.create"));
        Assert.Equal(2, await factory.CountAdminAuditLogsAsync("platform.commerce.plan-version.publish"));
        Assert.Equal(1, await factory.CountAdminAuditLogsAsync("platform.commerce.subscription.override"));
    }

    [Fact]
    public async Task FinanceCanManageCommerceWhileOperationsIsReadOnly()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var financeClient = factory.CreateClient();
        using var operationsClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var finance = await CreateAuthenticatedUserAsync(financeClient);
        var operations = await CreateAuthenticatedUserAsync(operationsClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        await GrantRoleAsync(ownerClient, finance.User.Id, PlatformRoleKeys.Finance);
        await GrantRoleAsync(ownerClient, operations.User.Id, PlatformRoleKeys.Operations);
        UseBearerToken(financeClient, finance.AccessToken);
        UseBearerToken(operationsClient, operations.AccessToken);

        using var financeCreate = await financeClient.PostAsJsonAsync(
            "/api/admin/v1/commerce/plans",
            new CreatePlatformPlanCommand(
                $"finance.{Guid.NewGuid():N}",
                "پلن مالی",
                "ایجاد پلن توسط نقش مالی"),
            JsonOptions);
        using var financeRead = await financeClient.GetAsync("/api/admin/v1/finance/orders");
        using var operationsCommerceRead = await operationsClient.GetAsync(
            "/api/admin/v1/commerce/plans");
        using var operationsFinanceRead = await operationsClient.GetAsync(
            "/api/admin/v1/finance/orders");
        using var operationsCreate = await operationsClient.PostAsJsonAsync(
            "/api/admin/v1/commerce/plans",
            new CreatePlatformPlanCommand(
                $"operations.{Guid.NewGuid():N}",
                "نباید ایجاد شود",
                "عملیات نباید مجوز مدیریت تجاری داشته باشد"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, financeCreate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, financeRead.StatusCode);
        Assert.Equal(HttpStatusCode.OK, operationsCommerceRead.StatusCode);
        Assert.Equal(HttpStatusCode.OK, operationsFinanceRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, operationsCreate.StatusCode);
    }

    [Fact]
    public async Task FinanceReadModelMasksProviderReferenceAndCannotForgePaymentSuccess()
    {
        await factory.ResetPlatformIamAsync();
        factory.PaymentGateway.CreationProviderReference = "provider-secret-reference-123456";
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        var organizationId = await factory.CreateOrganizationAsync($"Payment {Guid.NewGuid():N}");
        var membership = await factory.CreateMembershipAsync(
            owner.User.Id,
            organizationId,
            OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync();
        var planVersionId = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            price: 2_000_000m);
        await SelectWorkspaceAsync(ownerClient, membership.MembershipId, OrganizationRole.Manager);

        using var orderResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/commerce/orders",
            new CreateOrderCommand(planVersionId, Guid.NewGuid()),
            JsonOptions);
        var order = await ReadAsync<CreateOrderResult>(orderResponse);
        using var paymentResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/commerce/orders/{order.Order.OrderId:D}/payments",
            new InitiatePaymentCommand(Guid.NewGuid()),
            JsonOptions);
        paymentResponse.EnsureSuccessStatusCode();

        using var adminResponse = await ownerClient.GetAsync(
            $"/api/admin/v1/finance/orders/{order.Order.OrderId:D}");
        var adminJson = await adminResponse.Content.ReadAsStringAsync();
        var adminOrder = JsonSerializer.Deserialize<PlatformOrderItem>(adminJson, JsonOptions);
        using var forged = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/finance/orders/{order.Order.OrderId:D}/mark-successful",
            new { reason = "جعل موفقیت پرداخت" },
            JsonOptions);

        adminResponse.EnsureSuccessStatusCode();
        Assert.NotNull(adminOrder);
        var attempt = Assert.Single(adminOrder.PaymentAttempts);
        Assert.EndsWith("3456", attempt.MaskedProviderReference, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-secret-reference-123456", adminJson, StringComparison.Ordinal);
        Assert.DoesNotContain("paymentUrl", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
        Assert.Equal(OrderStatus.PendingPayment, adminOrder.Status);
        Assert.Equal(0, await factory.CountPaymentsAsync(order.Order.OrderId));
    }

    [Fact]
    public async Task OperationsExposeSafeNotificationAndHonestHealthStateWithoutAiEndpoint()
    {
        await factory.ResetPlatformIamAsync();
        using var ownerClient = factory.CreateClient();
        using var supportClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var support = await CreateAuthenticatedUserAsync(supportClient);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(ownerClient, owner.AccessToken);
        await GrantRoleAsync(ownerClient, support.User.Id, PlatformRoleKeys.Support);
        UseBearerToken(supportClient, support.AccessToken);
        const string destinationSecret = "+989121234567";
        const string titleSecret = "private-notification-title";
        const string bodySecret = "private-notification-body";
        var attemptId = await factory.CreateFailedNotificationDeliveryAttemptAsync(
            support.User.Id,
            destinationSecret,
            titleSecret,
            bodySecret);

        using var notificationResponse = await supportClient.GetAsync(
            "/api/admin/v1/operations/notifications");
        var notificationJson = await notificationResponse.Content.ReadAsStringAsync();
        var operations = JsonSerializer.Deserialize<PlatformNotificationOperations>(
            notificationJson,
            JsonOptions);
        var health = await supportClient.GetFromJsonAsync<PlatformSystemHealth>(
            "/api/admin/v1/operations/health",
            JsonOptions);
        var jobs = await supportClient.GetFromJsonAsync<PlatformJobOperations>(
            "/api/admin/v1/operations/jobs",
            JsonOptions);
        using var retry = await supportClient.PostAsJsonAsync(
            $"/api/admin/v1/operations/notifications/{attemptId:D}/retry",
            new { reason = "تلاش مجدد دستی" },
            JsonOptions);
        using var ai = await supportClient.GetAsync("/api/admin/v1/operations/ai");

        notificationResponse.EnsureSuccessStatusCode();
        Assert.NotNull(operations);
        Assert.Contains(operations.RecentFailures, item =>
            item.DeliveryAttemptId == attemptId &&
            item.Status == NotificationDeliveryStatus.Failed);
        Assert.DoesNotContain(destinationSecret, notificationJson, StringComparison.Ordinal);
        Assert.DoesNotContain(titleSecret, notificationJson, StringComparison.Ordinal);
        Assert.DoesNotContain(bodySecret, notificationJson, StringComparison.Ordinal);
        Assert.NotNull(health);
        Assert.Equal("NotIntegrated/DependencyPending", health.AiIntegrationStatus);
        Assert.Contains(health.Dependencies, item => item.Name == "Api" && item.Status == "Healthy");
        Assert.Contains(health.Dependencies, item => item.Name == "SqlServer" && item.Status == "Reachable");
        Assert.Contains(health.Dependencies, item => item.Name == "Storage");
        Assert.Contains(health.Dependencies, item => item.Name == "PaymentProvider");
        Assert.Contains(health.Dependencies, item => item.Name == "SignalR");
        Assert.NotNull(jobs);
        Assert.Contains(jobs.Counts, item => item.JobType == "IntegrationOutbox");
        Assert.Contains(jobs.Counts, item => item.JobType == "MessagingRealtimeOutbox");
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ai.StatusCode);
        Assert.Equal(
            NotificationDeliveryStatus.Failed,
            await factory.GetNotificationDeliveryStatusAsync(attemptId));
    }

    private static async Task<PlatformPlanVersionItem> CreateAndPublishVersionAsync(
        HttpClient ownerClient,
        Guid planId,
        decimal price,
        long learnerLimit,
        string reason)
    {
        using var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/commerce/plans/{planId:D}/versions",
            new CreatePlatformPlanVersionCommand(
                price,
                CurrencyCode.Irr,
                [new PlatformPlanEntitlementItem(EntitlementKey.Messaging, true)],
                [new PlatformPlanQuotaItem(QuotaKey.ActiveLearners, learnerLimit)],
                reason),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<PlatformPlanVersionItem>(createResponse);
        using var publishResponse = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/commerce/plan-versions/{created.PlanVersionId:D}/publish",
            new PublishPlatformPlanVersionCommand(created.Version, reason),
            JsonOptions);
        return await ReadAsync<PlatformPlanVersionItem>(publishResponse);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98918{sequence:D7}";
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

    private static async Task GrantRoleAsync(
        HttpClient ownerClient,
        Guid userId,
        string roleKey)
    {
        using var response = await ownerClient.PostAsJsonAsync(
            "/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(userId, roleKey, "دسترسی عملیاتی مرحله ۱۵D"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
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

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Task InitializeAsync()
    {
        factory.PaymentGateway.Reset();
        factory.Clock.UseSystemTime();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        factory.PaymentGateway.Reset();
        factory.Clock.UseSystemTime();
        return Task.CompletedTask;
    }
}
