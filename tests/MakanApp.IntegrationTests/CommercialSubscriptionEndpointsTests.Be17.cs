using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(SubscriptionOwnerType.Individual)]
    [InlineData(SubscriptionOwnerType.Organization)]
    public async Task Be17CatalogExcludesWrongAudienceHiddenUnavailableDraftAndIncompletePublishedTerms(SubscriptionOwnerType audience)
    {
        var actor = await ActorAsync(); var valid = await Be17PlanAsync(audience);
        var opposite = await Be17PlanAsync(audience == SubscriptionOwnerType.Individual ? SubscriptionOwnerType.Organization : SubscriptionOwnerType.Individual);
        var hidden = await Be17PlanAsync(audience); var disabled = await Be17PlanAsync(audience);
        var draft = await Be17PlanAsync(audience, publish: false);
        var incomplete = await Be17PlanAsync(audience, incomplete: true);
        await WithDb(async db =>
        {
            (await db.Plans.SingleAsync(p => p.Id == hidden.PlanId)).SetAvailability(false, true, true, factory.Clock.GetUtcNow().UtcDateTime);
            (await db.Plans.SingleAsync(p => p.Id == disabled.PlanId)).SetAvailability(true, false, true, factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        });
        var catalog = await GetAsync<List<CommercialPlanDto>>(actor.Client, "/api/v1/commerce/catalog?audience=" + audience);
        Assert.Contains(catalog, p => p.PlanVersionId == valid.VersionId);
        Assert.All(catalog, p => { Assert.Equal(audience, p.Audience); Assert.False(p.AutoRenew); Assert.Equal("TOMAN", p.DisplayCurrency); });
        foreach (var excluded in new[] { opposite.VersionId, hidden.VersionId, disabled.VersionId, draft.VersionId, incomplete.VersionId })
            Assert.DoesNotContain(catalog, p => p.PlanVersionId == excluded);
        var purchasePath = "/api/v1/commerce/personal/quote";
        if (audience == SubscriptionOwnerType.Individual)
        {
            var org = await factory.CreateOrganizationAsync("سازمان آزمون جهت Audience");
            var membership = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Manager);
            await SelectAsync(actor, membership.MembershipId, OrganizationRole.Manager);
            purchasePath = "/api/v1/commerce/organization/quote";
        }
        using var crossAudience = await actor.Client.PostAsJsonAsync(purchasePath,
            new CreateOrderCommand(valid.VersionId, Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.Conflict, crossAudience.StatusCode);
        Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ProblemAsync(crossAudience));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Be17AvailabilityBlocksNewIntentWithoutChangingAnExistingSubscription(bool renewal)
    {
        var subscribed = await ActorAsync(); var fresh = await ActorAsync();
        var plan = await Be17PlanAsync(SubscriptionOwnerType.Individual);
        var id = await SubscribeAsync(SubscriptionOwner.Individual(subscribed.UserId), plan.VersionId);
        await WithDb(async db =>
        {
            (await db.Plans.SingleAsync(p => p.Id == plan.PlanId)).SetAvailability(true, renewal, !renewal, factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        });
        var actor = renewal ? subscribed : fresh;
        var command = new CreateOrderCommand(plan.VersionId, Guid.NewGuid(), renewal ? CommercialOrderPurpose.Renewal : CommercialOrderPurpose.Purchase, renewal ? id : null);
        foreach (var route in new[] { "quote", "orders" })
        {
            using var rejected = await actor.Client.PostAsJsonAsync("/api/v1/commerce/personal/" + route, command, Json);
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ProblemAsync(rejected));
        }
        var summary = await GetAsync<CommercialSummaryDto>(subscribed.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(id, summary.SubscriptionId); Assert.Equal(plan.VersionId, summary.PlanVersionId);
        Assert.True(summary.Entitlements[EntitlementKey.Ai]);
    }

    [Fact]
    public async Task Be17CompleteDraftCannotBeScheduledAndKeepsTheActiveContract()
    {
        var actor = await ActorAsync(); var current = await Be17PlanAsync(SubscriptionOwnerType.Individual);
        var draft = await Be17PlanAsync(SubscriptionOwnerType.Individual, publish: false);
        var subscription = await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), current.VersionId);
        var before = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        using var rejected = await actor.Client.PostAsJsonAsync("/api/v1/commerce/personal/subscription/downgrade",
            new ScheduleDowngradeCommand(draft.VersionId, before.Version!, "نسخهٔ Draft کامل هم هنوز قابل عرضه نیست"), Json);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ProblemAsync(rejected));
        var after = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(subscription, after.SubscriptionId); Assert.Equal(current.VersionId, after.PlanVersionId);
        Assert.Null(after.ScheduledPlanVersionId);
    }

    [Fact]
    public async Task Be17ChangingPlanCannotBypassDisabledRenewalOrAdvertiseScheduledUnavailableTerms()
    {
        var actor = await ActorAsync(); var current = await Be17PlanAsync(SubscriptionOwnerType.Individual);
        var target = await Be17PlanAsync(SubscriptionOwnerType.Individual);
        var subscription = await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), current.VersionId);
        await WithDb(async db =>
        {
            (await db.Plans.SingleAsync(p => p.Id == target.PlanId)).SetAvailability(true, true, false, factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        });
        using var quote = await actor.Client.PostAsJsonAsync("/api/v1/commerce/personal/quote",
            new CreateOrderCommand(target.VersionId, Guid.NewGuid(), CommercialOrderPurpose.Renewal, subscription), Json);
        Assert.Equal(HttpStatusCode.Conflict, quote.StatusCode);
        Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ProblemAsync(quote));
        var summary = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        using var downgrade = await actor.Client.PostAsJsonAsync("/api/v1/commerce/personal/subscription/downgrade",
            new ScheduleDowngradeCommand(target.VersionId, summary.Version!, "نباید منع تمدید با تغییر پلن دور زده شود"), Json);
        Assert.Equal(HttpStatusCode.Conflict, downgrade.StatusCode);
        Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ProblemAsync(downgrade));
        var after = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(current.VersionId, after.PlanVersionId); Assert.Null(after.ScheduledPlanVersionId);
        Assert.Equal(current.VersionId, after.RenewalPlanVersionId);
    }

    [Fact]
    public async Task Be17AdminCreateUsesIrrEditUsesTomanAndOldOrderKeepsFrozenTerms()
    {
        Be17CurrentPolicy(); var admin = await AdminAsync(); var buyer = await ActorAsync();
        var plan = await PostAsync<PlatformPlanDetail>(admin.Client, "/api/admin/v1/commerce/plans",
            new CreatePlatformPlanCommand("be17.currency." + Guid.NewGuid().ToString("N"), "قیمت مصنوعی آزمون", "آزمون قرارداد واحد قیمت", SubscriptionOwnerType.Individual));
        var draft = await PostAsync<PlatformPlanVersionItem>(admin.Client, $"/api/admin/v1/commerce/plans/{plan.PlanId}/versions",
            new CreatePlatformPlanVersionCommand(123_450m, CurrencyCode.Irr, [new(EntitlementKey.Ai, true)], [new(QuotaKey.AiTextRequests, 2)], "مبلغ ریالی فقط در fixture"));
        Assert.Equal(123_450m, draft.PriceAmount);
        var validation = await GetAsync<CommercialValidationDto>(admin.Client, $"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/validation");
        using var edited = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/draft",
            new CommercialDraftCommand(4_321m, false, BillingCalendar.Gregorian, "be17-test-only", null, null,
                [new(EntitlementKey.Ai, true)], [new(QuotaKey.AiTextRequests, 2)], validation.Version, "مبلغ تومانی فقط در fixture"), Json);
        edited.EnsureSuccessStatusCode();
        var ready = (await edited.Content.ReadFromJsonAsync<CommercialValidationDto>(Json))!;
        await PostAsync<PlatformPlanVersionItem>(admin.Client, $"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/publish",
            new PublishPlatformPlanVersionCommand(ready.Version, "انتشار برای آزمون قیمت"));
        var entity = await WithDb(db => db.Plans.SingleAsync(p => p.Id == plan.PlanId));
        using var availability = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plans/{plan.PlanId}/availability",
            new CommercialAvailabilityCommand(true, true, true, Convert.ToBase64String(entity.RowVersion), "عرضهٔ آزمون"), Json);
        availability.EnsureSuccessStatusCode();
        var quote = await PostAsync<CommercialQuoteDto>(buyer.Client, "/api/v1/commerce/personal/quote", new CreateOrderCommand(draft.PlanVersionId, Guid.NewGuid()));
        Assert.Equal(4_321m, quote.PriceToman);
        var operation = Guid.NewGuid();
        var order = await PostAsync<CreateOrderResult>(buyer.Client, "/api/v1/commerce/personal/orders", new
        { PlanVersionId = draft.PlanVersionId, ClientOperationId = operation, Amount = 1, PriceToman = 1, Currency = "USD" });
        Assert.Equal(43_210m, order.Order.Amount); Assert.Equal("IRR", order.Order.Currency);
        var second = await WithDb(async db =>
        {
            var v = PlanVersion.CreateDraft(plan.PlanId, 2, factory.Clock.GetUtcNow().UtcDateTime);
            v.SetPrice(99_990m, CurrencyCode.Irr); v.SetMonthlyTerms(BillingCalendar.Gregorian, "be17-new-test-terms", null, null);
            v.SetEntitlement(EntitlementKey.Ai, true); v.SetQuota(QuotaKey.AiTextRequests, 9); v.Publish(factory.Clock.GetUtcNow().UtcDateTime);
            db.PlanVersions.Add(v); await db.SaveChangesAsync(); return v.Id;
        });
        var replay = await PostAsync<CreateOrderResult>(buyer.Client, "/api/v1/commerce/personal/orders", new CreateOrderCommand(draft.PlanVersionId, operation));
        Assert.Equal(order.Order.OrderId, replay.Order.OrderId); Assert.Equal(43_210m, replay.Order.Amount);
        var currentQuote = await PostAsync<CommercialQuoteDto>(buyer.Client, "/api/v1/commerce/personal/quote", new CreateOrderCommand(second, Guid.NewGuid()));
        Assert.Equal(9_999m, currentQuote.PriceToman);
        var payment = await PayAsync(buyer, order.Order.OrderId); await VerifyAsync(payment.PaymentAttemptId); await factory.DrainNotificationOutboxAsync();
        var summary = await GetAsync<CommercialSummaryDto>(buyer.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(draft.PlanVersionId, summary.PlanVersionId); Assert.Equal(4_321m, summary.MonthlyPriceToman);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(null)]
    public async Task Be17ZeroOneAndUnlimitedAreEnforcedByTheSameReservationEngine(long? limit)
    {
        var actor = await ActorAsync(); var plan = await Be17PlanAsync(SubscriptionOwnerType.Individual, aiLimit: limit);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), plan.VersionId);
        var context = new AiQuotaPreflightCommand(CommerceContextType.Personal);
        var before = await PostAsync<AiQuotaPreflightDto>(actor.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        Assert.Equal(limit, before.Remaining); Assert.Equal(limit != 0, before.Allowed);
        if (limit == 0)
        { Assert.Equal(CommerceErrorCodes.QuotaExceeded, await TryReserveAsync(actor, context)); return; }
        var operation = Guid.NewGuid(); var reservation = await ReserveAsync(actor, context, operation);
        Assert.Equal(reservation.ReservationId, (await ReserveAsync(actor, context, operation)).ReservationId);
        await FinishAsync(actor, reservation, UsageReservationStatus.Consumed);
        var after = await PostAsync<AiQuotaPreflightDto>(actor.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        Assert.Equal(limit.HasValue ? limit - 1 : null, after.Remaining);
        if (limit == 1) Assert.Equal(CommerceErrorCodes.QuotaExceeded, await TryReserveAsync(actor, context));
        else Assert.NotEqual(reservation.ReservationId, (await ReserveAsync(actor, context, Guid.NewGuid())).ReservationId);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("payer")]
    [InlineData("context")]
    [InlineData("trial")]
    public async Task Be17OperationCannotBeReusedWithAnotherCommercialIntent(string change)
    {
        var actor = await ActorAsync(); var plan = await Be17PlanAsync(SubscriptionOwnerType.Individual);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), plan.VersionId);
        var org = await factory.CreateOrganizationAsync("زمینهٔ رزرو آزمون");
        var membership = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Manager);
        await SelectAsync(actor, membership.MembershipId, OrganizationRole.Manager);
        var context = new AiQuotaPreflightCommand(CommerceContextType.Organization, org, AiPayerSelection.Individual);
        var operation = Guid.NewGuid(); var first = await ReserveAsync(actor, context, operation);
        var changed = change switch
        {
            "payer" => context with { Payer = AiPayerSelection.Organization },
            "context" => new AiQuotaPreflightCommand(CommerceContextType.Personal),
            "trial" => context with { Payer = AiPayerSelection.FreeTrial },
            _ => context
        };
        var hash = new byte[32]; if (change == "payload") hash[0] = 1;
        var error = await Assert.ThrowsAsync<CommerceException>(() => ReserveAsync(actor, changed, operation, hash));
        Assert.Equal(CommerceErrorCodes.UsageConflict, error.Code);
        Assert.Equal(1, await WithDb(db => db.UsageReservations.CountAsync(r => r.ConsumerUserId == actor.UserId && r.OperationId == operation)));
        Assert.Equal(UsageReservationStatus.Reserved, (await ReserveAsync(actor, context, operation)).Status);
        Assert.Equal(first.ReservationId, (await ReserveAsync(actor, context, operation)).ReservationId);
    }

    private void Be17CurrentPolicy()
    {
        var gates = factory.Services.GetRequiredService<MakanApp.Application.Assessment.PersonalClassAcademicFeatures>();
        gates.AssignmentsEnabled = false; gates.ExamsEnabled = false;
    }

    private async Task<(Guid PlanId, Guid VersionId)> Be17PlanAsync(SubscriptionOwnerType audience, long? aiLimit = 2, bool publish = true, bool incomplete = false)
    {
        Be17CurrentPolicy();
        return await WithDb(async db =>
        {
            var now = factory.Clock.GetUtcNow().UtcDateTime;
            var plan = Plan.Create("be17." + Guid.NewGuid().ToString("N"), "پلن مصنوعی BE17", now.AddDays(-1), audience);
            plan.SetAvailability(true, true, true, now.AddDays(-1));
            var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-1));
            version.SetPrice(12_500m, CurrencyCode.Irr);
            foreach (var key in Enum.GetValues<EntitlementKey>()) version.SetEntitlement(key,
                audience == SubscriptionOwnerType.Individual || key is not (EntitlementKey.PersonalClassHosting or EntitlementKey.PersonalSources));
            foreach (var key in Enum.GetValues<QuotaKey>()) version.SetQuota(key, key == QuotaKey.AiTextRequests ? aiLimit : key is QuotaKey.StorageBytes or QuotaKey.AssignmentFileBytes ? 10_000 : 10);
            if (!incomplete) version.SetMonthlyTerms(BillingCalendar.Gregorian, "be17-test-only-not-for-sale", 80, 95);
            db.Plans.Add(plan); db.PlanVersions.Add(version);
            if (publish && !incomplete) version.Publish(now.AddHours(-1), audience);
            else if (publish)
            {
                // فقط دادهٔ ناسازگار fixture؛ Catalog نباید به برچسب Published به‌تنهایی اعتماد کند.
                db.Entry(version).Property(v => v.Status).CurrentValue = PlanVersionStatus.Published;
                db.Entry(version).Property(v => v.PublishedAtUtc).CurrentValue = now.AddHours(-1);
            }
            await db.SaveChangesAsync(); return (plan.Id, version.Id);
        });
    }
}
