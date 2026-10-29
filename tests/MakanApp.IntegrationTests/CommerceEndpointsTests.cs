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
public sealed class CommerceEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 6_000_000;

    [Fact]
    public async Task ManagerWithoutSubscriptionReceivesExplicitNoSubscriptionState()
    {
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Commerce Empty"));
        var membership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);

        var summary = await client.GetFromJsonAsync<ManagerCommerceSummaryDto>(
            "/api/v1/commerce/summary",
            JsonOptions);

        Assert.NotNull(summary);
        Assert.Equal(organizationId, summary.OrganizationId);
        Assert.Equal(CommerceSummaryState.NoSubscription, summary.State);
        Assert.Null(summary.Subscription);
        Assert.Empty(summary.Entitlements);
        Assert.Empty(summary.Quotas);
    }

    [Fact]
    public async Task ManagerSummaryUsesPublishedTermsAndAuthoritativeUsageWithoutLeakingSecrets()
    {
        var nowUtc = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Commerce Summary"));
        var membership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync("ظرفیت سازمان");
        var versionId = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            new Dictionary<EntitlementKey, bool>
            {
                [EntitlementKey.Academic] = true,
                [EntitlementKey.Messaging] = false,
                [EntitlementKey.Reports] = true
            },
            new Dictionary<QuotaKey, long?>
            {
                [QuotaKey.ActiveLearners] = 1,
                [QuotaKey.ActiveClasses] = 3,
                [QuotaKey.StorageBytes] = null,
                [QuotaKey.ReportExports] = 5
            });
        var subscriptionId = await factory.CreateSubscriptionAsync(
            organizationId,
            versionId,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-1),
            nowUtc.AddDays(30));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var learners = await factory.CreateOrganizationPersonsAsync(organizationId, 2);
        foreach (var learner in learners)
        {
            _ = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learner);
        }

        await RecordUsageAsync(organizationId, 2, Guid.NewGuid());
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);

        using var response = await client.GetAsync("/api/v1/commerce/summary");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var summary = JsonSerializer.Deserialize<ManagerCommerceSummaryDto>(json, JsonOptions)!;

        Assert.Equal(CommerceSummaryState.Active, summary.State);
        Assert.Equal(CommercialMigrationStatus.AwaitingValidContract, summary.MigrationStatus);
        Assert.False(summary.CommercialControlEnabled);
        Assert.False(summary.HasVerifiedCommercialContract);
        Assert.Null(summary.CommercialEnforcementFromUtc);
        Assert.Equal(subscriptionId, summary.Subscription!.SubscriptionId);
        Assert.Equal(1, summary.Subscription.PlanVersionNumber);
        Assert.True(summary.Entitlements.Single(item => item.Key == EntitlementKey.Academic).Enabled);
        Assert.False(summary.Entitlements.Single(item => item.Key == EntitlementKey.Messaging).Enabled);
        var learnersQuota = summary.Quotas.Single(item => item.Key == QuotaKey.ActiveLearners);
        Assert.Equal(2, learnersQuota.CurrentUsage);
        Assert.True(learnersQuota.IsOverQuota);
        Assert.Equal(QuotaUsageSource.AuthoritativeModule, learnersQuota.UsageSource);
        var reportQuota = summary.Quotas.Single(item => item.Key == QuotaKey.ReportExports);
        Assert.Equal(2, reportQuota.CurrentUsage);
        Assert.Equal(3, reportQuota.Remaining);
        Assert.Equal(QuotaUsageSource.UsageLedger, reportQuota.UsageSource);
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurrentAccessContextIsolatesOrganizationsAndRejectsNonManagerRoles()
    {
        var nowUtc = DateTime.UtcNow.AddDays(2);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var teacher = await CreateAuthenticatedUserAsync(teacherClient);
        var organizationA = await factory.CreateOrganizationAsync(NewName("Commerce A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("Commerce B"));
        var managerA = await factory.CreateMembershipAsync(user.User.Id, organizationA, OrganizationRole.Manager);
        var managerB = await factory.CreateMembershipAsync(user.User.Id, organizationB, OrganizationRole.Manager);
        var teacherA = await factory.CreateMembershipAsync(
            teacher.User.Id,
            organizationA,
            OrganizationRole.Teacher);
        await CreateActiveSubscriptionAsync(organizationA, "Plan A", 1, nowUtc);
        await CreateActiveSubscriptionAsync(organizationB, "Plan B", 2, nowUtc);
        UseBearerToken(client, user.AccessToken);

        await SelectWorkspaceAsync(client, managerA.MembershipId, OrganizationRole.Manager);
        var summaryA = await client.GetFromJsonAsync<ManagerCommerceSummaryDto>(
            "/api/v1/commerce/summary",
            JsonOptions);
        await SelectWorkspaceAsync(client, managerB.MembershipId, OrganizationRole.Manager);
        var summaryB = await client.GetFromJsonAsync<ManagerCommerceSummaryDto>(
            "/api/v1/commerce/summary",
            JsonOptions);
        UseBearerToken(teacherClient, teacher.AccessToken);
        await SelectWorkspaceAsync(teacherClient, teacherA.MembershipId, OrganizationRole.Teacher);
        using var forbidden = await teacherClient.GetAsync("/api/v1/commerce/summary");

        Assert.Equal(organizationA, summaryA!.OrganizationId);
        Assert.Equal("Plan A", summaryA.Subscription!.PlanDisplayName);
        Assert.Equal(organizationB, summaryB!.OrganizationId);
        Assert.Equal("Plan B", summaryB.Subscription!.PlanDisplayName);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(CommerceErrorCodes.SummaryNotAllowed, await ReadProblemCodeAsync(forbidden));
    }

    [Fact]
    public async Task PublishedVersionFreezeAndDatabaseUniquenessPreserveHistory()
    {
        var nowUtc = DateTime.UtcNow.AddDays(3);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Version Freeze"));
        var membership = await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        var planId = await factory.CreateCommercePlanAsync("Versioned Plan");
        var versionOne = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ReportExports] = 10 });
        _ = await factory.CreateSubscriptionAsync(
            organizationId,
            versionOne,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-1),
            nowUtc.AddDays(30));
        _ = await factory.CreatePublishedPlanVersionAsync(
            planId,
            2,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ReportExports] = 100 });
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);

        var summary = await client.GetFromJsonAsync<ManagerCommerceSummaryDto>(
            "/api/v1/commerce/summary",
            JsonOptions);

        Assert.Equal(1, summary!.Subscription!.PlanVersionNumber);
        Assert.Equal(10, Assert.Single(summary.Quotas).Limit);
        Assert.True(await factory.DuplicatePlanVersionIsRejectedAsync(planId, 2));
        Assert.True(await factory.SecondActiveSubscriptionIsRejectedAsync(organizationId, versionOne));
    }

    [Fact]
    public async Task ExpiredSubscriptionHasNoEffectiveEntitlementOrQuota()
    {
        var nowUtc = DateTime.UtcNow.AddDays(4);
        factory.Clock.SetUtcNow(nowUtc);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Expired Commerce"));
        var planId = await factory.CreateCommercePlanAsync();
        var versionId = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            new Dictionary<EntitlementKey, bool> { [EntitlementKey.Reports] = true },
            new Dictionary<QuotaKey, long?> { [QuotaKey.ReportExports] = 1 });
        _ = await factory.CreateSubscriptionAsync(
            organizationId,
            versionId,
            SubscriptionStatus.Expired,
            nowUtc.AddDays(-10),
            nowUtc.AddDays(-1));

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntitlementService>();

        Assert.Null(await service.GetEffectiveSubscriptionAsync(organizationId, default));
        Assert.False(await service.IsEnabledAsync(organizationId, EntitlementKey.Reports, default));
        Assert.Null(await service.GetQuotaAsync(organizationId, QuotaKey.ReportExports, default));
        var error = await Assert.ThrowsAsync<CommerceException>(() =>
            service.RecordUsageAsync(
                organizationId,
                QuotaKey.ReportExports,
                1,
                Guid.NewGuid(),
                default));
        Assert.Equal(CommerceErrorCodes.SubscriptionInactive, error.Code);
    }

    [Fact]
    public async Task CapacityCheckHandlesBelowExactOverUnlimitedAndMissingQuota()
    {
        var nowUtc = DateTime.UtcNow.AddDays(5);
        factory.Clock.SetUtcNow(nowUtc);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Capacity"));
        var planId = await factory.CreateCommercePlanAsync();
        var versionId = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            quotas: new Dictionary<QuotaKey, long?>
            {
                [QuotaKey.ReportExports] = 2,
                [QuotaKey.StorageBytes] = null
            });
        _ = await factory.CreateSubscriptionAsync(
            organizationId,
            versionId,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-1),
            nowUtc.AddDays(30));
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntitlementService>();

        var exact = await service.CheckCapacityAsync(
            organizationId, QuotaKey.ReportExports, 2, default);
        _ = await service.RecordUsageAsync(
            organizationId, QuotaKey.ReportExports, 2, Guid.NewGuid(), default);
        var exhausted = await service.CheckCapacityAsync(
            organizationId, QuotaKey.ReportExports, 1, default);
        var unlimited = await service.CheckCapacityAsync(
            organizationId, QuotaKey.StorageBytes, long.MaxValue, default);
        var missing = await service.CheckCapacityAsync(
            organizationId, QuotaKey.ActiveClasses, 1, default);

        Assert.True(exact.Allowed);
        Assert.Equal(0, exact.RemainingAfterOperation);
        Assert.False(exhausted.Allowed);
        Assert.Equal(CommerceErrorCodes.QuotaExceeded, exhausted.DenialCode);
        Assert.True(unlimited.Allowed);
        Assert.True(unlimited.IsUnlimited);
        Assert.False(missing.Allowed);
        Assert.Equal(CommerceErrorCodes.QuotaRequired, missing.DenialCode);
    }

    [Fact]
    public async Task DowngradeMarksOrganizationOverQuotaWithoutDeletingLearners()
    {
        var nowUtc = DateTime.UtcNow.AddDays(6);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Downgrade"));
        var membership = await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        var academic = await factory.CreateAcademicClassAsync(organizationId, capacity: 10);
        var learners = await factory.CreateOrganizationPersonsAsync(organizationId, 2);
        foreach (var learner in learners)
        {
            _ = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learner);
        }

        var planId = await factory.CreateCommercePlanAsync("Downgrade Plan");
        var versionOne = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ActiveLearners] = 10 });
        var oldSubscription = await factory.CreateSubscriptionAsync(
            organizationId,
            versionOne,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-10),
            nowUtc.AddDays(10));
        await factory.CancelSubscriptionAsync(oldSubscription, nowUtc);
        var versionTwo = await factory.CreatePublishedPlanVersionAsync(
            planId,
            2,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ActiveLearners] = 1 });
        _ = await factory.CreateSubscriptionAsync(
            organizationId,
            versionTwo,
            SubscriptionStatus.Active,
            nowUtc,
            nowUtc.AddDays(30));
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);

        var summary = await client.GetFromJsonAsync<ManagerCommerceSummaryDto>(
            "/api/v1/commerce/summary",
            JsonOptions);

        var quota = Assert.Single(summary!.Quotas);
        Assert.Equal(2, quota.CurrentUsage);
        Assert.True(quota.IsOverQuota);
        Assert.Equal(2, await factory.CountActiveEnrollmentsAsync(organizationId, academic.ClassId));
    }

    [Fact]
    public async Task ConcurrentCapacityConsumptionAllowsOnlyTheFinalAvailableUnit()
    {
        var nowUtc = DateTime.UtcNow.AddDays(7);
        factory.Clock.SetUtcNow(nowUtc);
        var organizationId = await CreateMeteredOrganizationAsync(nowUtc, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = ConsumeAfterGateAsync(organizationId, Guid.NewGuid(), gate.Task);
        var second = ConsumeAfterGateAsync(organizationId, Guid.NewGuid(), gate.Task);
        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item == null);
        Assert.Single(results, item => item == CommerceErrorCodes.QuotaExceeded);
        var subscription = await GetEffectiveSubscriptionAsync(organizationId);
        Assert.Equal(1, await factory.CountUsageEntriesAsync(subscription.SubscriptionId, QuotaKey.ReportExports));
        Assert.Equal(1, await factory.SumUsageAsync(subscription.SubscriptionId, QuotaKey.ReportExports));
    }

    [Fact]
    public async Task ConcurrentRetryWithSameSourceOperationIsRecordedOnce()
    {
        var nowUtc = DateTime.UtcNow.AddDays(8);
        factory.Clock.SetUtcNow(nowUtc);
        var organizationId = await CreateMeteredOrganizationAsync(nowUtc, 10);
        var sourceOperationId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = RecordAfterGateAsync(organizationId, sourceOperationId, 3, gate.Task);
        var second = RecordAfterGateAsync(organizationId, sourceOperationId, 3, gate.Task);
        gate.SetResult();

        var results = await Task.WhenAll(first, second);
        var subscription = await GetEffectiveSubscriptionAsync(organizationId);

        Assert.Single(results, result => !result.AlreadyRecorded);
        Assert.Single(results, result => result.AlreadyRecorded);
        Assert.Equal(1, await factory.CountUsageEntriesAsync(
            subscription.SubscriptionId,
            QuotaKey.ReportExports,
            sourceOperationId));
        Assert.Equal(3, await factory.SumUsageAsync(subscription.SubscriptionId, QuotaKey.ReportExports));
        var entry = await factory.GetUsageEntryAsync(
            subscription.SubscriptionId,
            QuotaKey.ReportExports,
            sourceOperationId);
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(organizationId, entry.OrganizationId);
        Assert.Equal(subscription.SubscriptionId, entry.SubscriptionId);
        Assert.Equal(QuotaKey.ReportExports, entry.QuotaKey);
        Assert.Equal(3, entry.Amount);
        Assert.Equal(sourceOperationId, entry.SourceOperationId);
        Assert.Equal(nowUtc, entry.OccurredAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntitlementService>();
        var conflict = await Assert.ThrowsAsync<CommerceException>(() =>
            service.RecordUsageAsync(
                organizationId,
                QuotaKey.ReportExports,
                4,
                sourceOperationId,
                default));
        Assert.Equal(CommerceErrorCodes.UsageConflict, conflict.Code);
    }

    [Fact]
    public async Task CommerceRequiresAuthenticationAndExposesNoManagerMutationApi()
    {
        using var anonymousClient = factory.CreateClient();
        using var unauthenticated = await anonymousClient.GetAsync("/api/v1/commerce/summary");

        using var managerClient = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        var organizationId = await factory.CreateOrganizationAsync(NewName("No Admin"));
        var membership = await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(managerClient, manager.AccessToken);
        await SelectWorkspaceAsync(managerClient, membership.MembershipId, OrganizationRole.Manager);
        using var mutation = await managerClient.PostAsJsonAsync(
            "/api/v1/commerce/plans",
            new { code = "forbidden", displayName = "Forbidden" });

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mutation.StatusCode);
    }

    private async Task<Guid> CreateMeteredOrganizationAsync(DateTime nowUtc, long limit)
    {
        var organizationId = await factory.CreateOrganizationAsync(NewName("Metered"));
        var planId = await factory.CreateCommercePlanAsync();
        var versionId = await factory.CreatePublishedPlanVersionAsync(
            planId,
            1,
            quotas: new Dictionary<QuotaKey, long?> { [QuotaKey.ReportExports] = limit });
        _ = await factory.CreateSubscriptionAsync(
            organizationId,
            versionId,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-1),
            nowUtc.AddDays(30));
        return organizationId;
    }

    private async Task<(Guid SubscriptionId, int Version)> CreateActiveSubscriptionAsync(
        Guid organizationId,
        string displayName,
        int versionNumber,
        DateTime nowUtc)
    {
        var planId = await factory.CreateCommercePlanAsync(displayName);
        var versionId = await factory.CreatePublishedPlanVersionAsync(planId, versionNumber);
        var subscriptionId = await factory.CreateSubscriptionAsync(
            organizationId,
            versionId,
            SubscriptionStatus.Active,
            nowUtc.AddDays(-1),
            nowUtc.AddDays(30));
        return (subscriptionId, versionNumber);
    }

    private async Task RecordUsageAsync(Guid organizationId, long amount, Guid sourceOperationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntitlementService>();
        _ = await service.RecordUsageAsync(
            organizationId,
            QuotaKey.ReportExports,
            amount,
            sourceOperationId,
            default);
    }

    private async Task<string?> ConsumeAfterGateAsync(
        Guid organizationId,
        Guid sourceOperationId,
        Task gate)
    {
        await gate;
        try
        {
            await RecordUsageAsync(organizationId, 1, sourceOperationId);
            return null;
        }
        catch (CommerceException exception)
        {
            return exception.Code;
        }
    }

    private async Task<UsageRecordResult> RecordAfterGateAsync(
        Guid organizationId,
        Guid sourceOperationId,
        long amount,
        Task gate)
    {
        await gate;
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEntitlementService>()
            .RecordUsageAsync(
                organizationId,
                QuotaKey.ReportExports,
                amount,
                sourceOperationId,
                default);
    }

    private async Task<EffectiveSubscriptionResult> GetEffectiveSubscriptionAsync(Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IEntitlementService>()
            .GetEffectiveSubscriptionAsync(organizationId, default))!;
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98935{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
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

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        factory.Clock.UseSystemTime();
        return Task.CompletedTask;
    }
}
