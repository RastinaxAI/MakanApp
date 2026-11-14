using MakanApp.Application.Identity;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AccountDeletionAcceptanceTests
{
    private async Task<Guid> DeletionPlanAsync(SubscriptionOwnerType audience = SubscriptionOwnerType.Individual) => await Db(async db =>
    {
        var plan = Plan.Create("deletion." + Guid.NewGuid().ToString("N"), "قرارداد ساختگی آزمون حذف", Now, audience);
        plan.SetAvailability(true, true, true, Now);
        var version = PlanVersion.CreateDraft(plan.Id, 1, Now); version.SetMonthlyTerms(BillingCalendar.Gregorian, "synthetic-only", 80, 95);
        version.SetPrice(1000, CurrencyCode.Irr);
        foreach (var key in new[] { EntitlementKey.Ai, EntitlementKey.PersonalClassHosting, EntitlementKey.Academic, EntitlementKey.Assignments, EntitlementKey.Exams, EntitlementKey.Reports })
            version.SetEntitlement(key, true);
        foreach (var key in new[] { QuotaKey.AiTextRequests, QuotaKey.ActiveClasses, QuotaKey.ActiveLearners, QuotaKey.StorageBytes, QuotaKey.AssignmentFileBytes, QuotaKey.ReportExports })
            version.SetQuota(key, 1000);
        version.Publish(Now); db.Plans.Add(plan); db.PlanVersions.Add(version); await db.SaveChangesAsync(); return version.Id;
    });

    [Fact]
    public async Task DeletionCancelsIndividualSubscriptionButKeepsPaidOrderReceiptAndReservedUnknownUsage()
    {
        var owner = await ActorAsync(); var versionId = await DeletionPlanAsync();
        var orderId = await Db(async db =>
        {
            var version = await db.PlanVersions.Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions).SingleAsync(v => v.Id == versionId);
            var order = Order.Create(owner.Id, null, version, Guid.NewGuid(), new byte[32], Now);
            var attempt = PaymentAttempt.Create(order, "Synthetic", Guid.NewGuid(), new byte[32], Now); var claim = Guid.NewGuid();
            Assert.True(attempt.TryClaimInitiation(claim, Now, Now.AddMinutes(1)));
            attempt.MarkPendingVerification(claim, "synthetic-reference", "https://gateway.example.test/pay", Now);
            attempt.MarkSucceeded("synthetic-reference", Now); order.MarkPaid(Now);
            var subscription = Subscription.CreateMonthly(SubscriptionOwner.Individual(owner.Id), version, Now, 0, Now, order.Id, true);
            db.Orders.Add(order); db.PaymentAttempts.Add(attempt); db.Payments.Add(Payment.Create(attempt, Now)); db.Subscriptions.Add(subscription);
            foreach (var status in new[] { UsageReservationStatus.Reserved, UsageReservationStatus.Unknown, UsageReservationStatus.Consumed })
            {
                var reservation = UsageReservation.Create(SubscriptionOwner.Individual(owner.Id), subscription.Id, versionId, owner.Id,
                    CommerceContextType.Personal, owner.Id, QuotaKey.AiTextRequests, subscription.StartsAtUtc, subscription.EndsAtUtc!.Value,
                    1, Guid.NewGuid(), new byte[32], false, Now);
                if (status != UsageReservationStatus.Reserved) reservation.Finalize(status, "synthetic_result", Now);
                db.UsageReservations.Add(reservation); db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(reservation, Now, "synthetic_history"));
            }
            await db.SaveChangesAsync(); return order.Id;
        });
        var before = await Db(db => db.Orders.Where(o => o.Id == orderId).Select(o => o.RowVersion).SingleAsync());
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Db(async db =>
        {
            Assert.Equal(before, (await db.Orders.SingleAsync(o => o.Id == orderId)).RowVersion);
            Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status); Assert.Equal(PaymentAttemptStatus.Succeeded, (await db.PaymentAttempts.SingleAsync()).Status);
            Assert.Single(await db.Payments.ToArrayAsync()); Assert.Equal(SubscriptionStatus.Cancelled, (await db.Subscriptions.SingleAsync()).Status);
            Assert.Equal(new[] { UsageReservationStatus.Reserved, UsageReservationStatus.Consumed, UsageReservationStatus.Unknown }.OrderBy(v => v),
                await db.UsageReservations.OrderBy(r => r.Status).Select(r => r.Status).ToArrayAsync());
            Assert.Equal(3, await db.UsageLedgerEntries.CountAsync());
        });
    }

    [Fact]
    public async Task DeletionPaidSchoolApplicationRequiresAnOnboardedReplacementManager()
    {
        var manager = await ActorAsync(); var applicant = await ActorAsync(); var plan = await DeletionPlanAsync(SubscriptionOwnerType.Organization);
        var applicationId = await Db(async db =>
        {
            var app = SchoolApplication.Create(applicant.Id, SchoolApplicationKind.LegalOrganization,
                new("آموزشگاه ساختگی", "شخص حقوقی ساختگی", null, null, null, 20, 3), Now);
            app.Submit("synthetic-approved-policy", Now, ["License"]); app.StartReview(Now); app.Decide(SchoolApplicationStatus.Approved, Now, manager.Id, applicant.Id);
            var version = await db.PlanVersions.Include(v => v.Price).SingleAsync(v => v.Id == plan);
            var order = Order.Create(applicant.Id, null, version, Guid.NewGuid(), new byte[32], Now); order.BindSchoolApplication(app.Id); order.MarkPaid(Now);
            db.SchoolApplications.Add(app); db.Orders.Add(order); await db.SaveChangesAsync(); return app.Id;
        });
        var request = await RequestAsync(manager); await DrainAsync(); Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id));
        Assert.Equal(AccountDeletionErrorCodes.SchoolManagerDependency, await Db(db => db.AccountDeletionRequests.Select(r => r.FailureCode).SingleAsync()));
        Assert.Equal(SchoolApplicationStatus.Approved, await Db(db => db.SchoolApplications.Where(a => a.Id == applicationId).Select(a => a.Status).SingleAsync()));
        Assert.Equal(OrderStatus.Paid, await Db(db => db.Orders.Select(o => o.Status).SingleAsync()));
        Assert.Equal(UserAccountStatus.Active, await Db(db => db.Users.Where(u => u.Id == manager.Id).Select(u => u.AccountStatus).SingleAsync()));
    }

    [Fact]
    public async Task DeletionUncommittedSchoolDraftIsCancelledWithHistory()
    {
        var owner = await ActorAsync();
        var id = await Db(async db =>
        {
            var draft = SchoolApplication.Create(owner.Id, SchoolApplicationKind.IndividualTeacher, new("پیش‌نویس", null, null, null, null, null, null), Now);
            db.SchoolApplications.Add(draft); await db.SaveChangesAsync(); return draft.Id;
        });
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        Assert.Equal(SchoolApplicationStatus.Cancelled, await Db(db => db.SchoolApplications.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));
        Assert.Equal(1, await Db(db => db.SchoolApplicationHistories.CountAsync(h => h.ApplicationId == id)));
    }

    [Fact]
    public async Task DeletionCannotRemoveTheLastPlatformOwnerOrRegrantAfterFreeze()
    {
        var owner = await ActorAsync(); await _factory.BootstrapPlatformOwnerAsync(owner.Id);
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id));
        Assert.Equal(AccountDeletionErrorCodes.LastPlatformOwner, await Db(db => db.AccountDeletionRequests.Select(r => r.FailureCode).SingleAsync()));
        Assert.Null(await Db(db => db.PlatformRoleAssignments.Where(a => a.UserId == owner.Id && a.RoleId == PlatformCatalogIds.PlatformOwnerRole).Select(a => a.EndedAtUtc).SingleAsync()));
    }

    [Fact]
    public async Task DeletionEndsPlatformRolesWhilePreservingTheirAssignmentHistory()
    {
        var owner = await ActorAsync(); var admin = await ActorAsync(); await _factory.BootstrapPlatformOwnerAsync(admin.Id);
        await Db(async db =>
        {
            db.PlatformRoleAssignments.Add(PlatformRoleAssignment.CreateActive(owner.Id, PlatformCatalogIds.SupportRole, admin.Id, Now));
            await db.SaveChangesAsync();
        });
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        Assert.NotNull(await Db(db => db.PlatformRoleAssignments.Where(a => a.UserId == owner.Id).Select(a => a.EndedAtUtc).SingleAsync()));
        Assert.Null(await Db(db => db.PlatformRoleAssignments.Where(a => a.UserId == admin.Id).Select(a => a.EndedAtUtc).SingleAsync()));
    }
}
