using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        using var deniedClient = factory.CreateClient();
        var deniedActor = await CreateAuthenticatedUserAsync(deniedClient);
        UseBearerToken(deniedClient, deniedActor.AccessToken);
        using var denied = await deniedClient.PostAsJsonAsync(
            $"/api/admin/v1/commerce/organizations/{organizationId}/subscription-override",
            new OverridePlatformSubscriptionCommand(
                secondVersion.PlanVersionId,
                factory.Clock.GetUtcNow().UtcDateTime.AddDays(60),
                "ارتقای کنترل‌شده اشتراک"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

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
        Assert.Equal("Disabled", health.AiIntegrationStatus);
        Assert.Contains(health.Dependencies, item => item.Name == "Api" && item.Status == "Healthy");
        Assert.Contains(health.Dependencies, item => item.Name == "SqlServer" && item.Status == "Reachable");
        Assert.Contains(health.Dependencies, item => item.Name == "Storage");
        Assert.Contains(health.Dependencies, item => item.Name == "PaymentProvider");
        Assert.Contains(health.Dependencies, item => item.Name == "SignalR");
        Assert.Contains(health.Dependencies, item => item.Name == "Ai" && item.Status == "Disabled");
        Assert.Contains(health.Dependencies, item => item.Name == "Push");
        Assert.Contains(health.Dependencies, item => item.Name == "FileCleanup" && item.Status == "Disabled");
        Assert.NotNull(jobs);
        Assert.Contains(jobs.Counts, item => item.JobType == "IntegrationOutbox");
        Assert.Contains(jobs.Counts, item => item.JobType == "MessagingRealtimeOutbox");
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ai.StatusCode);
        Assert.Equal(
            NotificationDeliveryStatus.Failed,
            await factory.GetNotificationDeliveryStatusAsync(attemptId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JobsSeparatePendingActiveLeaseAndExhaustedOutboxWithoutExposingPayload(bool realtime)
    {
        // dispatcher والد روی DB خودش زنده می‌ماند؛ این آزمون DB و worker scope واقعاً مستقل دارد.
        await using var isolated = await CreateIsolatedOperationsScopeAsync();
        var host = isolated.Factory;
        using var client = host.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(client, host);
        await host.BootstrapPlatformOwnerAsync(owner.User.Id);
        UseBearerToken(client, owner.AccessToken);
        var name = realtime ? "MessagingRealtimeOutbox" : "IntegrationOutbox";
        var before = await client.GetFromJsonAsync<PlatformJobOperations>("/api/admin/v1/operations/jobs", JsonOptions);
        var baseline = Assert.Single(before!.Queues, item => item.JobType == name);
        var now = host.Clock.GetUtcNow().UtcDateTime;
        var oldest = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ids = new List<Guid>();
        var changes = new List<Guid>();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var conversation = Conversation.CreateUserManaged(ConversationType.Group, ConversationScope.Personal, null,
            "صف مصنوعی عملیات", null, owner.User.Id, Guid.NewGuid(), new byte[32], now);
        if (realtime) db.Conversations.Add(conversation);
        var cap = realtime ? MessagingRealtimeOutboxProcessor.MaximumAttempts : OutboxProcessor.MaximumAttempts;
        var cases = new[] { (Attempts: 1, Lease: (DateTime?)now.AddMinutes(-1)), (Attempts: cap, Lease: (DateTime?)now.AddMinutes(1)),
            (Attempts: cap, Lease: (DateTime?)now.AddMinutes(-1)), (Attempts: cap, Lease: (DateTime?)null) };
        try
        {
            for (var index = 0; index < cases.Length; index++)
            {
                object entity;
                if (realtime)
                {
                    var change = MessagingChangeEvent.Create(conversation.Id, conversation.AllocateNextChangeSequence(),
                        MessagingChangeType.MessageCreated, Guid.NewGuid(), null, oldest.AddDays(-index));
                    db.MessagingChangeEvents.Add(change);
                    changes.Add(change.Id);
                    var message = MessagingRealtimeOutboxMessage.Create(change, change.OccurredAtUtc);
                    ids.Add(message.Id);
                    entity = message;
                }
                else
                {
                    var message = OutboxMessage.Create(Guid.NewGuid(), "synthetic.operations",
                        "{\"private\":\"queue-payload-secret-marker\"}", oldest.AddDays(-index));
                    ids.Add(message.Id);
                    entity = message;
                }
                db.Add(entity);
                db.Entry(entity).Property("AttemptCount").CurrentValue = cases[index].Attempts;
                db.Entry(entity).Property("ClaimedUntilUtc").CurrentValue = cases[index].Lease;
                if (index == 0) db.Entry(entity).Property("NextAttemptAtUtc").CurrentValue = now.AddHours(1);
                if (!realtime && cases[index].Lease.HasValue)
                    db.Entry(entity).Property("ClaimToken").CurrentValue = Guid.NewGuid();
            }
            await db.SaveChangesAsync();
            using var response = await client.GetAsync("/api/admin/v1/operations/jobs");
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<PlatformJobOperations>(json, JsonOptions)!;
            var queue = Assert.Single(result.Queues, item => item.JobType == name);
            Assert.Equal(baseline.PendingCount + 1, queue.PendingCount);
            Assert.Equal(baseline.ProcessingCount + 1, queue.ProcessingCount);
            Assert.Equal(baseline.FailedCount + 2, queue.FailedCount);
            Assert.Equal(oldest, queue.OldestPendingAtUtc);
            Assert.True(queue.OldestPendingAgeSeconds > 0);
            Assert.DoesNotContain("queue-payload-secret-marker", json, StringComparison.Ordinal);
            Assert.All(ids, id => Assert.DoesNotContain(id.ToString(), json, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (realtime)
            {
                await db.MessagingRealtimeOutboxMessages.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
                await db.MessagingChangeEvents.Where(item => changes.Contains(item.Id)).ExecuteDeleteAsync();
                await db.Conversations.Where(item => item.Id == conversation.Id).ExecuteDeleteAsync();
            }
            else await db.OutboxMessages.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ScheduledBacklogExcludesFutureIntentAndUsesServerDeadlineForOldestAge()
    {
        await factory.ResetPlatformIamAsync();
        using var original = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(original);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IIdentitySecurity>();
            services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
        }));
        using var client = host.CreateClient();
        UseBearerToken(client, owner.AccessToken);
        var before = await client.GetFromJsonAsync<PlatformJobOperations>("/api/admin/v1/operations/jobs", JsonOptions);
        var baseline = Assert.Single(before!.Queues, item => item.JobType == "ScheduledMessage");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var dueAt = now.AddMinutes(-5);
        var conversation = Conversation.CreateUserManaged(ConversationType.Group, ConversationScope.Personal, null,
            "قصد مصنوعی زمان‌بندی", null, owner.User.Id, Guid.NewGuid(), new byte[32], now.AddMinutes(-10));
        var authority = new ScheduledMessageAuthority(WorkspaceType.Personal, null, null, null, null);
        var content = new ScheduledMessageContent(MessageKind.Text, "private-scheduled-marker", [], null, [], null, null, false);
        var due = ScheduledMessage.Create(conversation.Id, owner.User.Id, Guid.NewGuid(), authority, content, new byte[32], dueAt, now.AddMinutes(-10));
        var future = ScheduledMessage.Create(conversation.Id, owner.User.Id, Guid.NewGuid(), authority, content, new byte[32], now.AddDays(1), now.AddMinutes(-10));
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        try
        {
            db.Conversations.Add(conversation);
            db.ScheduledMessages.AddRange(due, future);
            await db.SaveChangesAsync();
            var result = await client.GetFromJsonAsync<PlatformJobOperations>("/api/admin/v1/operations/jobs", JsonOptions);
            var queue = Assert.Single(result!.Queues, item => item.JobType == "ScheduledMessage");
            Assert.Equal(baseline.PendingCount + 1, queue.PendingCount);
            Assert.Equal(new[] { baseline.OldestPendingAtUtc, dueAt }.Min(), queue.OldestPendingAtUtc);
            Assert.Equal(before.Counts.Where(item => item.JobType == "ScheduledMessage" && item.Status == "Scheduled").Sum(item => item.Count) + 2,
                result.Counts.Where(item => item.JobType == "ScheduledMessage" && item.Status == "Scheduled").Sum(item => item.Count));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.ScheduledMessages.Where(item => item.Id == due.Id || item.Id == future.Id).ExecuteDeleteAsync();
            await db.Conversations.Where(item => item.Id == conversation.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ProtectedHealthShowsConfiguredOptionalAiAndSanitizesStorageProbeFailure()
    {
        await factory.ResetPlatformIamAsync();
        using var original = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(original);
        await factory.BootstrapPlatformOwnerAsync(owner.User.Id);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IIdentitySecurity>();
            services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<AiServiceOptions>();
            services.AddSingleton(new AiServiceOptions { Enabled = true, BaseUrl = "http://private-ai-address-marker" });
            services.RemoveAll<IStorageReadinessProbe>();
            services.AddSingleton<IStorageReadinessProbe>(new ThrowingOperationsStorageProbe());
        }));
        using var client = host.CreateClient();
        UseBearerToken(client, owner.AccessToken);
        using var response = await client.GetAsync("/api/admin/v1/operations/health");
        var json = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var result = JsonSerializer.Deserialize<PlatformSystemHealth>(json, JsonOptions)!;
        Assert.Equal("Configured/RuntimeNotProbed", result.AiIntegrationStatus);
        Assert.Contains(result.Dependencies, item => item.Name == "Storage" && item.Status == "Unavailable");
        Assert.DoesNotContain("private-ai-address-marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("storage-probe-secret-marker", json, StringComparison.Ordinal);
    }

    private sealed class ThrowingOperationsStorageProbe : IStorageReadinessProbe
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken) =>
            throw new IOException("storage-probe-secret-marker");
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
                reason, BillingCalendar.Gregorian, "integration-test-terms"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadAsync<PlatformPlanVersionItem>(createResponse);
        using var publishResponse = await ownerClient.PostAsJsonAsync(
            $"/api/admin/v1/commerce/plan-versions/{created.PlanVersionId:D}/publish",
            new PublishPlatformPlanVersionCommand(created.Version, reason),
            JsonOptions);
        return await ReadAsync<PlatformPlanVersionItem>(publishResponse);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client, MakanAppWebApplicationFactory? testFactory = null)
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
                (testFactory ?? factory).GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        return await ReadAsync<VerifyOtpResult>(verifyResponse);
    }

    private static async Task<OperationsQueueScope> CreateIsolatedOperationsScopeAsync()
    {
        var runId = Guid.NewGuid().ToString("N");
        var isolated = new MakanAppWebApplicationFactory("MakanApp_Recovery_PH2_" + runId,
            Path.Combine(Path.GetTempPath(), "MakanApp-PH2-Queues", runId),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        try
        {
            await ((IAsyncLifetime)isolated).InitializeAsync();
            return new OperationsQueueScope(isolated);
        }
        catch
        {
            await ((IAsyncLifetime)isolated).DisposeAsync();
            throw;
        }
    }

    private sealed class OperationsQueueScope(MakanAppWebApplicationFactory isolated) : IAsyncDisposable
    {
        public MakanAppWebApplicationFactory Factory => isolated;
        public async ValueTask DisposeAsync() => await ((IAsyncLifetime)isolated).DisposeAsync();
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
