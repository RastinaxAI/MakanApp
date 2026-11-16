using System.Globalization;
using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class Be17CommercialPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(EntitlementKey.Academic, QuotaKey.ActiveClasses)]
    [InlineData(EntitlementKey.Academic, QuotaKey.ActiveLearners)]
    [InlineData(EntitlementKey.Ai, QuotaKey.AiTextRequests)]
    [InlineData(EntitlementKey.Reports, QuotaKey.ReportExports)]
    [InlineData(EntitlementKey.Storage, QuotaKey.StorageBytes)]
    [InlineData(EntitlementKey.PersonalSources, QuotaKey.SourceCount)]
    [InlineData(EntitlementKey.PersonalSources, QuotaKey.StorageBytes)]
    [InlineData(EntitlementKey.PersonalClassHosting, QuotaKey.AssignmentFileBytes)]
    public void OmittedRequiredQuotaBlocksPublicationButExplicitZeroAndUnlimitedAreDifferent(EntitlementKey feature, QuotaKey omitted)
    {
        var version = PlanVersion.CreateDraft(Guid.NewGuid(), 1, Now);
        version.SetFreePrice();
        version.SetMonthlyTerms(BillingCalendar.Gregorian, "be17-unit-only", null, null);
        version.SetEntitlement(feature, true);
        foreach (var key in Enum.GetValues<QuotaKey>().Where(k => k != omitted)) version.SetQuota(key, 1);
        Assert.Contains("Quota:" + omitted, version.ValidatePublication(SubscriptionOwnerType.Individual));
        Assert.Throws<InvalidOperationException>(() => version.Publish(Now, SubscriptionOwnerType.Individual));
        version.SetQuota(omitted, 0);
        Assert.Empty(version.ValidatePublication(SubscriptionOwnerType.Individual));
        Assert.Equal(0, version.QuotaDefinitions.Single(q => q.Key == omitted).Limit);
        version.SetQuota(omitted, null);
        Assert.Null(version.QuotaDefinitions.Single(q => q.Key == omitted).Limit);
        version.Publish(Now, SubscriptionOwnerType.Individual);
    }

    [Theory]
    [InlineData("Price")]
    [InlineData("FreePrice")]
    [InlineData("Entitlement")]
    [InlineData("Quota")]
    [InlineData("MonthlyTerms")]
    public void EveryPublishedCommercialTermRequiresANewVersion(string field)
    {
        var version = PlanVersion.CreateDraft(Guid.NewGuid(), 1, Now);
        version.SetFreePrice(); version.SetMonthlyTerms(BillingCalendar.Persian, "be17-unit-only", null, null);
        version.SetEntitlement(EntitlementKey.Messaging, true); version.SetQuota(QuotaKey.AiTextRequests, 0);
        version.Publish(Now);
        Assert.Throws<InvalidOperationException>(() =>
        {
            switch (field)
            {
                case "Price": version.SetPrice(10, CurrencyCode.Irr); break;
                case "FreePrice": version.SetFreePrice(); break;
                case "Entitlement": version.SetEntitlement(EntitlementKey.Messaging, false); break;
                case "Quota": version.SetQuota(QuotaKey.AiTextRequests, null); break;
                case "MonthlyTerms": version.SetMonthlyTerms(BillingCalendar.Gregorian, "changed", 80, 95); break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        });
        Assert.True(version.ExplicitlyFree); Assert.Equal(0, version.Price!.Amount);
        Assert.Equal(BillingCalendar.Persian, version.Calendar);
        Assert.True(Assert.Single(version.Entitlements).Enabled);
        Assert.Equal(0, Assert.Single(version.QuotaDefinitions).Limit);
    }

    [Theory]
    [InlineData(1404, 6, 31, 1404, 7, 30)]
    [InlineData(1403, 12, 30, 1404, 1, 30)]
    public void PersianMonthlyPeriodsPreserveTehranTimeAndOriginalAnchor(int year, int month, int day, int nextYear, int nextMonth, int nextDay)
    {
        var calendar = new PersianCalendar();
        var local = calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        var anchor = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), CommercialPeriods.Tehran);
        var next = TimeZoneInfo.ConvertTimeFromUtc(CommercialPeriods.MonthlyBoundary(anchor, 1, BillingCalendar.Persian), CommercialPeriods.Tehran);
        Assert.Equal(nextYear, calendar.GetYear(next)); Assert.Equal(nextMonth, calendar.GetMonth(next));
        Assert.Equal(nextDay, calendar.GetDayOfMonth(next)); Assert.Equal(TimeSpan.Zero, next.TimeOfDay);
        var recovered = TimeZoneInfo.ConvertTimeFromUtc(CommercialPeriods.MonthlyBoundary(anchor, 12, BillingCalendar.Persian), CommercialPeriods.Tehran);
        Assert.Equal(month, calendar.GetMonth(recovered));
        Assert.Equal(Math.Min(day, calendar.GetDaysInMonth(year + 1, month)), calendar.GetDayOfMonth(recovered));
    }
}
