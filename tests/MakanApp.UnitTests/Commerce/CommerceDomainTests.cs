using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class CommerceDomainTests
{
    private static readonly DateTime NowUtc =
        new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PlanCanExistWithStableCodeAndDisplayName()
    {
        var plan = Plan.Create("school.standard", "پلن استاندارد", NowUtc);

        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("school.standard", plan.Code);
        Assert.Equal("پلن استاندارد", plan.DisplayName);
    }

    [Fact]
    public void PlanVersionRejectsInvalidVersionNumber()
    {
        var plan = Plan.Create("invalid.version", "نسخه نامعتبر", NowUtc);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlanVersion.CreateDraft(plan.Id, 0, NowUtc));
    }

    [Fact]
    public void PublishedPlanVersionTermsAreImmutable()
    {
        var version = CreateDraftVersion(1);
        version.SetEntitlement(EntitlementKey.Messaging, true);
        version.SetQuota(QuotaKey.ActiveLearners, 100);
        version.Publish(NowUtc.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            version.SetEntitlement(EntitlementKey.Messaging, false));
        Assert.Throws<InvalidOperationException>(() =>
            version.SetQuota(QuotaKey.ActiveLearners, 50));
        Assert.Throws<InvalidOperationException>(() =>
            version.Publish(NowUtc.AddMinutes(2)));
    }

    [Fact]
    public void NewPlanVersionDoesNotChangePublishedVersion()
    {
        var plan = Plan.Create("version.freeze", "تثبیت نسخه", NowUtc);
        var first = PlanVersion.CreateDraft(plan.Id, 1, NowUtc);
        first.SetEntitlement(EntitlementKey.Reports, true);
        first.SetQuota(QuotaKey.ReportExports, 10);
        first.Publish(NowUtc.AddMinutes(1));
        var second = PlanVersion.CreateDraft(plan.Id, 2, NowUtc.AddMinutes(2));
        second.SetEntitlement(EntitlementKey.Reports, false);
        second.SetQuota(QuotaKey.ReportExports, 3);
        second.Publish(NowUtc.AddMinutes(3));

        Assert.True(Assert.Single(first.Entitlements).Enabled);
        Assert.Equal(10, Assert.Single(first.QuotaDefinitions).Limit);
        Assert.False(Assert.Single(second.Entitlements).Enabled);
        Assert.Equal(3, Assert.Single(second.QuotaDefinitions).Limit);
    }

    [Fact]
    public void SubscriptionUsesInclusiveStartAndExclusiveEndBoundary()
    {
        var version = CreatePublishedVersion();
        var startsAtUtc = NowUtc.AddHours(1);
        var endsAtUtc = startsAtUtc.AddDays(30);
        var subscription = Subscription.CreatePending(
            Guid.NewGuid(),
            version,
            startsAtUtc,
            endsAtUtc,
            NowUtc);
        subscription.Activate(startsAtUtc);

        Assert.False(subscription.IsEffectiveAt(startsAtUtc.AddTicks(-1)));
        Assert.True(subscription.IsEffectiveAt(startsAtUtc));
        Assert.True(subscription.IsEffectiveAt(endsAtUtc.AddTicks(-1)));
        Assert.False(subscription.IsEffectiveAt(endsAtUtc));
    }

    [Fact]
    public void SubscriptionCannotUseDraftPlanVersion()
    {
        var version = CreateDraftVersion(1);

        Assert.Throws<InvalidOperationException>(() =>
            Subscription.CreatePending(
                Guid.NewGuid(),
                version,
                NowUtc,
                NowUtc.AddDays(1),
                NowUtc));
    }

    [Fact]
    public void CancelledSubscriptionIsNeverEffective()
    {
        var version = CreatePublishedVersion();
        var subscription = Subscription.CreatePending(
            Guid.NewGuid(),
            version,
            NowUtc,
            NowUtc.AddDays(30),
            NowUtc.AddMinutes(-1));
        subscription.Activate(NowUtc);
        subscription.Cancel(NowUtc.AddMinutes(1));

        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Equal(NowUtc.AddMinutes(1), subscription.CancelledAtUtc);
        Assert.False(subscription.IsEffectiveAt(NowUtc.AddMinutes(2)));
    }

    [Fact]
    public void QuotaSupportsExplicitUnlimitedLimitAndRejectsNegativeLimit()
    {
        var version = CreateDraftVersion(1);

        version.SetQuota(QuotaKey.StorageBytes, null);

        Assert.Null(Assert.Single(version.QuotaDefinitions).Limit);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            version.SetQuota(QuotaKey.StorageBytes, -1));
    }

    [Fact]
    public void UsageLedgerRequiresPositiveRetryIdentity()
    {
        Assert.Throws<ArgumentException>(() => UsageLedgerEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            QuotaKey.ReportExports,
            0,
            Guid.NewGuid(),
            NowUtc));
        Assert.Throws<ArgumentException>(() => UsageLedgerEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            QuotaKey.ReportExports,
            1,
            Guid.Empty,
            NowUtc));
    }

    [Fact]
    public void OnlyReportExportsUsesLedgerAccountingInThisStep()
    {
        Assert.True(CommercePolicy.IsLedgerMetered(QuotaKey.ReportExports));
        Assert.False(CommercePolicy.IsLedgerMetered(QuotaKey.ActiveLearners));
        Assert.Equal(
            QuotaUsageSource.AuthoritativeModule,
            CommercePolicy.GetUsageSource(QuotaKey.StorageBytes));
    }

    private static PlanVersion CreateDraftVersion(int number)
    {
        var plan = Plan.Create($"plan.{Guid.NewGuid():N}", "پلن تست", NowUtc);
        return PlanVersion.CreateDraft(plan.Id, number, NowUtc);
    }

    private static PlanVersion CreatePublishedVersion()
    {
        var version = CreateDraftVersion(1);
        version.Publish(NowUtc);
        return version;
    }
}
