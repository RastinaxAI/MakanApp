using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class CommercialProductRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IncompletePublicationDistinguishesUnsetZeroAndUnlimited()
    {
        var v = PlanVersion.CreateDraft(Guid.NewGuid(), 1, Now);
        Assert.Contains("Price", v.ValidatePublication());
        Assert.Contains("MonthlyCalendarAndSaleTerms", v.ValidatePublication());
        Assert.Throws<InvalidOperationException>(() => v.Publish(Now));
        v.SetFreePrice(); v.SetMonthlyTerms(BillingCalendar.Persian, "approved-test-terms", null, null);
        v.SetEntitlement(EntitlementKey.Ai, true);
        Assert.Contains("Quota:AiTextRequests", v.ValidatePublication());
        v.SetQuota(QuotaKey.AiTextRequests, 0);
        Assert.Empty(v.ValidatePublication());
        v.SetQuota(QuotaKey.AiTextRequests, null);
        Assert.Null(Assert.Single(v.QuotaDefinitions).Limit);
        v.SetEntitlement(EntitlementKey.PersonalClassHosting, true);
        foreach (var key in new[] { QuotaKey.ActiveClasses, QuotaKey.ActiveLearners, QuotaKey.StorageBytes, QuotaKey.AssignmentFileBytes })
            v.SetQuota(key, 0);
        Assert.Contains("OwnerFeatureMismatch:PersonalClassHosting", v.ValidatePublication(SubscriptionOwnerType.Organization));
        Assert.Empty(v.ValidatePublication(SubscriptionOwnerType.Individual));
        v.Publish(Now, SubscriptionOwnerType.Individual);
        Assert.Throws<InvalidOperationException>(() => v.SetMonthlyTerms(BillingCalendar.Gregorian, "changed", 50, 90));
        Assert.Throws<InvalidOperationException>(() => v.SetFreePrice());
    }

    [Fact]
    public void UpgradePreservesFractionAndMatchesDirectUpgrade()
    {
        Assert.Equal(150, CommercialProration.EffectiveLimit(100, [new(100, 1, 2)]));
        var staged = CommercialProration.EffectiveLimit(100, [new(100, 1, 2), new(100, 1, 2)]);
        Assert.Equal(CommercialProration.EffectiveLimit(100, [new(200, 1, 2)]), staged);
        Assert.Equal(60, 150 - 80 - 10);
        Assert.Equal(1, CommercialProration.EffectiveLimit(0, [new(1, 1, 3), new(2, 1, 3)]));
    }

    [Fact]
    public void PriceRoundingAggregatesExactFractionsBeforeRounding()
    {
        Assert.Equal(1m, CommercialProration.CumulativePriceToman([(1m, 1L, 6L), (1m, 1L, 3L)]));
        Assert.Equal(CommercialProration.CumulativePriceToman([(2m, 1L, 2L)]),
            CommercialProration.CumulativePriceToman([(1m, 1L, 2L), (1m, 1L, 2L)]));
    }

    [Fact]
    public void MonthlyAnchorRecoversAfterShortGregorianMonth()
    {
        var anchor = new DateTime(2027, 1, 31, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2027, 2, 28, 8, 0, 0, DateTimeKind.Utc), CommercialPeriods.MonthlyBoundary(anchor, 1, BillingCalendar.Gregorian));
        Assert.Equal(new DateTime(2027, 3, 31, 8, 0, 0, DateTimeKind.Utc), CommercialPeriods.MonthlyBoundary(anchor, 2, BillingCalendar.Gregorian));
    }

    [Fact]
    public void TrialResetUsesTehranMidnightNotUtcDate()
    {
        var before = new DateTime(2026, 10, 29, 20, 29, 59, DateTimeKind.Utc);
        var after = before.AddSeconds(1);
        var first = CommercialPeriods.TrialDay(before); var next = CommercialPeriods.TrialDay(after);
        Assert.Equal(after, first.End); Assert.Equal(first.End, next.Start);
        Assert.Equal(TimeSpan.FromDays(1), next.End - next.Start);
    }

    [Fact]
    public void OrganizationGraceDoesNotExtendIndividualAndNeverBlocksSecurityOrMessaging()
    {
        var version = Version();
        var organization = Subscription.CreateMonthly(SubscriptionOwner.Organization(Guid.NewGuid()), version, Now, 0, Now, null, true);
        var individual = Subscription.CreateMonthly(SubscriptionOwner.Individual(Guid.NewGuid()), version, Now, 0, Now, null, true);
        var end = organization.EndsAtUtc!.Value;
        Assert.True(organization.IsInOrganizationGrace(end));
        Assert.False(individual.IsInOrganizationGrace(end));
        Assert.True(OrganizationCommercialAccess.IsAllowed(organization, CommercialEducationalAction.ExistingWrite, end.AddDays(6)));
        Assert.False(OrganizationCommercialAccess.IsAllowed(organization, CommercialEducationalAction.NewCapacity, end));
        Assert.False(OrganizationCommercialAccess.IsAllowed(organization, CommercialEducationalAction.ExistingWrite, end.AddDays(7)));
        Assert.True(OrganizationCommercialAccess.IsAllowed(organization, CommercialEducationalAction.AccessReduction, end.AddDays(8)));
        Assert.True(OrganizationCommercialAccess.IsAllowed(organization, CommercialEducationalAction.Messaging, end.AddDays(8)));
        Assert.True(OrganizationCommercialAccess.IsAllowed(null, CommercialEducationalAction.ExistingWrite, end.AddDays(8)));
    }

    [Fact]
    public void FuturePaidPeriodDoesNotReleaseQuotaBeforeStart()
    {
        var current = Subscription.CreateMonthly(SubscriptionOwner.Individual(Guid.NewGuid()), Version(), Now, 1, Now, Guid.NewGuid(), true);
        Assert.Equal(SubscriptionStatus.Scheduled, current.Status);
        Assert.False(current.IsEffectiveAt(current.StartsAtUtc.AddTicks(-1)));
        Assert.True(current.IsEffectiveAt(current.StartsAtUtc));
    }

    [Fact]
    public void QuotaJsonRequiresExplicitLimitAndDistinguishesZeroFromUnlimited()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<MakanApp.Application.Platform.PlatformPlanQuotaItem>("{\"Key\":5}"));
        var zero = System.Text.Json.JsonSerializer.Deserialize<MakanApp.Application.Platform.PlatformPlanQuotaItem>("{\"Key\":5,\"Limit\":0}")!;
        var unlimited = System.Text.Json.JsonSerializer.Deserialize<MakanApp.Application.Platform.PlatformPlanQuotaItem>("{\"Key\":5,\"Limit\":null}")!;
        Assert.Equal(0, zero.Limit); Assert.Null(unlimited.Limit);
    }

    [Fact]
    public void LegacyMigrationStatusDoesNotInventDatesAndManagedEnforcementStartsWhenRegistered()
    {
        var version = Version();
        var legacy = Subscription.CreatePending(Guid.NewGuid(), version, Now.AddDays(-20), null, Now.AddDays(-20));
        legacy.Activate(Now.AddDays(-20));
        Assert.Equal(CommercialMigrationStatus.AwaitingValidContract, legacy.MigrationStatus);
        Assert.False(legacy.CommercialControlEnabled); Assert.Null(legacy.EndsAtUtc);
        Assert.Null(legacy.BillingAnchorUtc); Assert.Null(legacy.CommercialEnforcementFromUtc);
        var managed = Subscription.CreateMonthly(SubscriptionOwner.Organization(legacy.OrganizationId!.Value), version,
            Now.AddDays(-3), 0, Now, null, true);
        Assert.Equal(CommercialMigrationStatus.Completed, managed.MigrationStatus);
        Assert.Equal(Now.AddDays(-3), managed.StartsAtUtc);
        Assert.Equal(Now, managed.CommercialEnforcementFromUtc);
        Assert.Null(legacy.EndsAtUtc); Assert.Null(legacy.CommercialEnforcementFromUtc);
        var personal = Subscription.CreateMonthly(SubscriptionOwner.Individual(Guid.NewGuid()), version, Now, 0, Now, null, true);
        Assert.Equal(CommercialMigrationStatus.NotApplicable, personal.MigrationStatus);
    }

    [Fact]
    public void UnknownReservationHoldsOriginalPeriodAndCannotRewriteFinalResult()
    {
        var user = Guid.NewGuid();
        var reservation = UsageReservation.Create(SubscriptionOwner.Individual(user), null, null, user, CommerceContextType.Personal,
            user, QuotaKey.AiTextRequests, Now, Now.AddDays(1), 1, Guid.NewGuid(), new byte[32], true, Now);
        Assert.True(reservation.Finalize(UsageReservationStatus.Unknown, "provider_timeout", Now.AddMinutes(1)));
        Assert.Null(reservation.FinalizedAtUtc);
        Assert.True(reservation.Finalize(UsageReservationStatus.Consumed, "late_success", Now.AddDays(2)));
        Assert.Equal(Now, reservation.PeriodStartsAtUtc);
        Assert.False(reservation.Finalize(UsageReservationStatus.Consumed, "late_success", Now.AddDays(2)));
        Assert.Throws<InvalidOperationException>(() => reservation.Finalize(UsageReservationStatus.Released, "changed", Now.AddDays(2)));
    }

    [Theory]
    [InlineData(QuotaKey.ActiveClasses, QuotaKind.CurrentCapacity)]
    [InlineData(QuotaKey.ActiveLearners, QuotaKind.CurrentCapacity)]
    [InlineData(QuotaKey.StorageBytes, QuotaKind.CurrentCapacity)]
    [InlineData(QuotaKey.SourceCount, QuotaKind.CurrentCapacity)]
    [InlineData(QuotaKey.AiTextRequests, QuotaKind.PeriodicConsumption)]
    [InlineData(QuotaKey.ReportExports, QuotaKind.PeriodicConsumption)]
    public void QuotaKindIsExplicit(QuotaKey key, QuotaKind kind) => Assert.Equal(kind, QuotaKinds.For(key));

    private static PlanVersion Version()
    {
        var version = PlanVersion.CreateDraft(Guid.NewGuid(), 1, Now);
        version.SetPrice(10_000, CurrencyCode.Irr); version.SetEntitlement(EntitlementKey.Ai, true);
        version.SetQuota(QuotaKey.AiTextRequests, 10); version.SetMonthlyTerms(BillingCalendar.Gregorian, "unit-test-terms", null, null);
        version.Publish(Now); return version;
    }
}
