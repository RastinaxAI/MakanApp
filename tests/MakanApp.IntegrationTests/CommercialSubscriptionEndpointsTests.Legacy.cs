using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task IncompleteLegacyContractPreservesCompatibilityWithoutNewActiveContractOrAiQuota()
    {
        var legacy = await LegacyOrganizationAsync(incomplete: true);
        var summary = await GetAsync<CommercialSummaryDto>(legacy.Manager.Client, "/api/v1/commerce/organization/subscription");
        Assert.Equal(SubscriptionDisplayState.LegacyPendingContract, summary.State);
        Assert.Equal(CommercialMigrationStatus.AwaitingValidContract, summary.MigrationStatus);
        Assert.False(summary.CommercialControlEnabled); Assert.False(summary.HasVerifiedCommercialContract);
        Assert.Equal(legacy.SubscriptionId, summary.LegacySubscriptionId);
        Assert.Null(summary.SubscriptionId); Assert.Null(summary.PlanVersionId);
        Assert.Null(summary.MonthlyPriceToman); Assert.Null(summary.PeriodStartsAtUtc); Assert.Null(summary.PeriodEndsAtUtc);
        Assert.Null(summary.CommercialEnforcementFromUtc); Assert.Empty(summary.Entitlements); Assert.Empty(summary.Quotas);
        var oldSummary = await GetAsync<ManagerCommerceSummaryDto>(legacy.Manager.Client, "/api/v1/commerce/summary");
        Assert.Equal(CommerceSummaryState.Active, oldSummary.State);
        Assert.Equal(CommercialMigrationStatus.AwaitingValidContract, oldSummary.MigrationStatus);
        Assert.False(oldSummary.HasVerifiedCommercialContract); Assert.False(oldSummary.CommercialControlEnabled);
        Assert.Null(oldSummary.Subscription!.EndsAtUtc);
        Assert.True(oldSummary.Entitlements.Single(e => e.Key == EntitlementKey.Academic).Enabled);
        Assert.DoesNotContain(oldSummary.Entitlements, e => e.Key == EntitlementKey.Ai);
        using var write = await legacy.Manager.Client.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("تداوم مشتری قدیمی"), Json);
        write.EnsureSuccessStatusCode();
        var context = new AiQuotaPreflightCommand(CommerceContextType.Organization, legacy.OrganizationId);
        var preflight = await PostAsync<AiQuotaPreflightDto>(legacy.Manager.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        Assert.False(preflight.Allowed); Assert.Equal(CommerceErrorCodes.SubscriptionInactive, preflight.ErrorCode);
        var denied = await Assert.ThrowsAsync<CommerceException>(() => ReserveAsync(legacy.Manager, context, Guid.NewGuid()));
        Assert.Equal(CommerceErrorCodes.SubscriptionInactive, denied.Code);
        var admin = await AdminAsync();
        using var invalid = await admin.Client.PostAsJsonAsync($"/api/admin/v1/commerce/organizations/{legacy.OrganizationId}/commercial-contracts",
            new RegisterCommercialContractCommand(legacy.PlanVersionId, factory.Clock.GetUtcNow().UtcDateTime, 0, summary.Version, "نسخهٔ تاریخی ناقص قرارداد معتبر نیست"), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(CommerceErrorCodes.TermsIncomplete, await ProblemAsync(invalid));
        await WithDb(async db =>
        {
            var saved = await db.Subscriptions.SingleAsync(s => s.Id == legacy.SubscriptionId);
            Assert.Equal(SubscriptionStatus.Active, saved.Status); Assert.Null(saved.EndsAtUtc); Assert.Null(saved.BillingAnchorUtc);
            Assert.Equal(legacy.StartsAtUtc, saved.StartsAtUtc); Assert.False(saved.CommercialControlEnabled);
            var version = await db.PlanVersions.Include(v => v.Price).Include(v => v.QuotaDefinitions).SingleAsync(v => v.Id == legacy.PlanVersionId);
            Assert.Null(version.Price); Assert.Null(version.Calendar); Assert.Empty(version.QuotaDefinitions);
            Assert.Equal(1, await db.Subscriptions.CountAsync(s => s.OrganizationId == legacy.OrganizationId));
            Assert.False(await db.UsageReservations.AnyAsync(r => r.OwnerId == legacy.OrganizationId));
            Assert.Equal(2, (await db.UsageLedgerEntries.SingleAsync(e => e.Id == legacy.LedgerEntryId)).Amount);
        });
    }

    [Fact]
    public async Task OnlyValidAdminContractCompletesLegacyMigrationWithoutRewritingHistoricalDatesOrUsage()
    {
        var legacy = await LegacyOrganizationAsync(incomplete: false);
        var before = await GetAsync<CommercialSummaryDto>(legacy.Manager.Client, "/api/v1/commerce/organization/subscription");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var compatibility = scope.ServiceProvider.GetRequiredService<IEntitlementService>();
            Assert.True(await compatibility.IsEnabledAsync(legacy.OrganizationId, EntitlementKey.Academic, default));
            Assert.NotNull(await compatibility.GetQuotaAsync(legacy.OrganizationId, QuotaKey.ActiveClasses, default));
            Assert.False(await compatibility.IsEnabledAsync(legacy.OrganizationId, EntitlementKey.Ai, default));
            Assert.Null(await compatibility.GetQuotaAsync(legacy.OrganizationId, QuotaKey.AiTextRequests, default));
            var capacity = await compatibility.CheckCapacityAsync(legacy.OrganizationId, QuotaKey.AiTextRequests, 1, default);
            Assert.False(capacity.Allowed); Assert.Equal(CommerceErrorCodes.TermsIncomplete, capacity.DenialCode);
            await Assert.ThrowsAsync<CommerceException>(() => compatibility.RecordUsageAsync(legacy.OrganizationId,
                QuotaKey.AiTextRequests, 1, Guid.NewGuid(), default));
        }
        var admin = await AdminAsync(); var version = await PlanAsync(SubscriptionOwnerType.Organization, ai: 5);
        var registeredAt = factory.Clock.GetUtcNow().UtcDateTime;
        var contract = await PostAsync<CommercialSummaryDto>(admin.Client, $"/api/admin/v1/commerce/organizations/{legacy.OrganizationId}/commercial-contracts",
            new RegisterCommercialContractCommand(version, registeredAt.AddDays(-3), 0, before.Version, "ثبت قرارداد معتبر با تاریخ‌های صریح و حفظ سابقهٔ قدیمی"));
        Assert.Equal(CommercialMigrationStatus.Completed, contract.MigrationStatus);
        Assert.True(contract.CommercialControlEnabled); Assert.True(contract.HasVerifiedCommercialContract);
        Assert.Equal(SubscriptionDisplayState.Active, contract.State);
        Assert.NotEqual(legacy.SubscriptionId, contract.SubscriptionId); Assert.Null(contract.LegacySubscriptionId);
        Assert.Equal(version, contract.PlanVersionId); Assert.Equal(registeredAt.AddDays(-3), contract.PeriodStartsAtUtc);
        Assert.Equal(registeredAt, contract.CommercialEnforcementFromUtc);
        var quota = contract.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests);
        Assert.Equal(5, quota.Limit); Assert.Equal(0, quota.Consumed); Assert.Equal(5, quota.Remaining);
        var context = new AiQuotaPreflightCommand(CommerceContextType.Organization, legacy.OrganizationId);
        await FinishAsync(legacy.Manager, await ReserveAsync(legacy.Manager, context, Guid.NewGuid()), UsageReservationStatus.Consumed);
        var after = await GetAsync<CommercialSummaryDto>(legacy.Manager.Client, "/api/v1/commerce/organization/subscription");
        Assert.Equal(1, after.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Consumed);
        await WithDb(async db =>
        {
            var old = await db.Subscriptions.SingleAsync(s => s.Id == legacy.SubscriptionId);
            Assert.Equal(SubscriptionStatus.Cancelled, old.Status); Assert.Equal(registeredAt, old.CancelledAtUtc);
            Assert.Equal(legacy.StartsAtUtc, old.StartsAtUtc); Assert.Null(old.EndsAtUtc); Assert.Null(old.BillingAnchorUtc);
            Assert.Equal(legacy.PlanVersionId, old.PlanVersionId); Assert.False(old.CommercialControlEnabled);
            var historical = await db.UsageLedgerEntries.SingleAsync(e => e.Id == legacy.LedgerEntryId);
            Assert.Equal(2, historical.Amount); Assert.Equal(legacy.SubscriptionId, historical.SubscriptionId);
            Assert.Null(historical.PeriodEndsAtUtc); Assert.Null(historical.ConsumerUserId);
            Assert.Equal(2, await db.Subscriptions.CountAsync(s => s.OrganizationId == legacy.OrganizationId));
        });
        var history = await GetAsync<List<CommercialHistoryDto>>(admin.Client, $"/api/admin/v1/commerce/history/{contract.SubscriptionId}");
        var audit = Assert.Single(history);
        Assert.Contains(legacy.SubscriptionId.ToString(), audit.Before!);
        Assert.Contains("MigrationStatus=Completed", audit.After!);
        Assert.Contains("EnforcementFrom=", audit.After!);
    }

    [Fact]
    public async Task OrganizationWithoutSubscriptionAwaitsExplicitContractWithoutSyntheticSubscriptionOrWriteLock()
    {
        var manager = await ActorAsync(); var organizationId = await factory.CreateOrganizationAsync("سازمان بدون قرارداد " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(manager.UserId, organizationId, OrganizationRole.Manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var summary = await GetAsync<CommercialSummaryDto>(manager.Client, "/api/v1/commerce/organization/subscription");
        Assert.Equal(SubscriptionDisplayState.NoSubscription, summary.State);
        Assert.Equal(CommercialMigrationStatus.AwaitingValidContract, summary.MigrationStatus);
        Assert.False(summary.HasVerifiedCommercialContract); Assert.Null(summary.LegacySubscriptionId);
        Assert.Null(summary.SubscriptionId); Assert.Null(summary.PlanVersionId); Assert.Null(summary.PeriodEndsAtUtc);
        Assert.Empty(summary.Entitlements); Assert.Empty(summary.Quotas);
        using var write = await manager.Client.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("درس مشتری بدون قرارداد"), Json);
        write.EnsureSuccessStatusCode();
        Assert.Equal(0, await WithDb(db => db.Subscriptions.CountAsync(s => s.OrganizationId == organizationId)));
    }

    private sealed record LegacyOrganizationFixture(TestActor Manager, Guid OrganizationId, Guid PlanVersionId,
        Guid SubscriptionId, Guid LedgerEntryId, DateTime StartsAtUtc);

    private async Task<LegacyOrganizationFixture> LegacyOrganizationAsync(bool incomplete)
    {
        var manager = await ActorAsync(); var organizationId = await factory.CreateOrganizationAsync("سازمان قدیمی " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(manager.UserId, organizationId, OrganizationRole.Manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var versionId = incomplete ? await WithDb(async db =>
        {
            var plan = Plan.Create("legacy-test." + Guid.NewGuid().ToString("N"), "پلن تاریخی ناقص", now.AddDays(-90));
            var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-90));
            version.SetEntitlement(EntitlementKey.Academic, true); version.SetEntitlement(EntitlementKey.Ai, true);
            db.Plans.Add(plan); db.PlanVersions.Add(version); await db.SaveChangesAsync();
            // شبیه‌سازی دادهٔ منتشرشدهٔ تاریخی، پیش از وجود Guard فعلی انتشار.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [commerce].[PlanVersions] SET [Status]=2,[PublishedAtUtc]={now.AddDays(-89)} WHERE [Id]={version.Id}");
            return version.Id;
        }) : await PlanAsync(SubscriptionOwnerType.Organization, ai: null);
        return await WithDb(async db =>
        {
            var version = await db.PlanVersions.SingleAsync(v => v.Id == versionId);
            var startsAt = now.AddDays(-60);
            var subscription = Subscription.CreatePending(organizationId, version, startsAt, null, startsAt);
            subscription.Activate(startsAt);
            var entry = UsageLedgerEntry.Create(organizationId, subscription.Id, QuotaKey.AiTextRequests, 2, Guid.NewGuid(), now.AddDays(-1));
            db.Subscriptions.Add(subscription); db.UsageLedgerEntries.Add(entry); await db.SaveChangesAsync();
            return new LegacyOrganizationFixture(manager, organizationId, versionId, subscription.Id, entry.Id, startsAt);
        });
    }
}
