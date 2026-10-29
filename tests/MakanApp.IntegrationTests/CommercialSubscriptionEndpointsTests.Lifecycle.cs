using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task HiddenPlanAndNewVersionPreserveActiveContractAndPublishedTerms()
    {
        var actor = await ActorAsync(); var admin = await AdminAsync();
        var versionId = await PlanAsync(SubscriptionOwnerType.Individual, ai: 10);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), versionId);
        var snapshot = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        var plan = await WithDb(db => db.Plans.SingleAsync(p => db.PlanVersions.Any(v => v.Id == versionId && v.PlanId == p.Id)));
        using var hidden = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plans/{plan.Id}/availability",
            new CommercialAvailabilityCommand(false, false, true, Convert.ToBase64String(plan.RowVersion), "پنهان‌کردن عرضهٔ آزمایشی"), Json);
        hidden.EnsureSuccessStatusCode();
        var catalog = await GetAsync<List<CommercialPlanDto>>(actor.Client, "/api/v1/commerce/catalog?audience=Individual");
        Assert.DoesNotContain(catalog, p => p.PlanId == plan.Id);
        var after = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(snapshot.SubscriptionId, after.SubscriptionId); Assert.Equal(versionId, after.PlanVersionId);
        var version = await WithDb(db => db.PlanVersions.SingleAsync(v => v.Id == versionId));
        using var mutation = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plan-versions/{versionId}/draft",
            new CommercialDraftCommand(200, false, BillingCalendar.Gregorian, "test-changed", null, null, [], [],
                Convert.ToBase64String(version.RowVersion), "تلاش برای تغییر نسخهٔ منتشرشده"), Json);
        Assert.Equal(HttpStatusCode.Conflict, mutation.StatusCode);
        await PlanAsync(SubscriptionOwnerType.Individual, ai: 100);
        Assert.Equal(versionId, (await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription")).PlanVersionId);
        var history = await GetAsync<List<CommercialHistoryDto>>(admin.Client, $"/api/admin/v1/commerce/history/{plan.Id}");
        Assert.Single(history); Assert.NotEqual(history[0].Before, history[0].After);
    }

    [Fact]
    public async Task FeatureNotEntitledAndQuotaExceededAreDifferentAndCrossAccountReceiptsAreHidden()
    {
        var disabled = await ActorAsync(); var exhausted = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(disabled.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 1, aiEntitled: false));
        await SubscribeAsync(SubscriptionOwner.Individual(exhausted.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 0));
        var context = new AiQuotaPreflightCommand(CommerceContextType.Personal, Payer: AiPayerSelection.Individual);
        var feature = await PostAsync<AiQuotaPreflightDto>(disabled.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        var quota = await PostAsync<AiQuotaPreflightDto>(exhausted.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        Assert.Equal(CommerceErrorCodes.FeatureNotEntitled, feature.ErrorCode); Assert.Equal(CommerceErrorCodes.QuotaExceeded, quota.ErrorCode);
        var trial = context with { Payer = AiPayerSelection.FreeTrial };
        var operationId = Guid.NewGuid();
        var first = await ReserveAsync(disabled, trial, operationId); var second = await ReserveAsync(exhausted, trial, operationId);
        Assert.NotEqual(first.ReservationId, second.ReservationId);
        var denial = await Assert.ThrowsAsync<CommerceException>(() => FinishAsync(exhausted, first, UsageReservationStatus.Consumed));
        Assert.Equal(CommerceErrorCodes.ResourceNotFound, denial.Code);
        await FinishAsync(disabled, first, UsageReservationStatus.Unknown);
        var conflictingPayer = await Assert.ThrowsAsync<CommerceException>(() => ReserveAsync(disabled, context, operationId));
        Assert.Equal(CommerceErrorCodes.UsageConflict, conflictingPayer.Code);
    }

    [Fact]
    public async Task ImmediatePaidUpgradeProduces150And60PreservesReservationsAndRepeatedPaymentHasOneEffect()
    {
        var anchor = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var end = CommercialPeriods.MonthlyBoundary(anchor, 1, BillingCalendar.Gregorian);
        factory.Clock.SetUtcNow(anchor.AddTicks((end - anchor).Ticks / 2));
        var actor = await ActorAsync();
        var first = await PlanAsync(SubscriptionOwnerType.Individual, ai: 100, priceIrr: 1000);
        var second = await PlanAsync(SubscriptionOwnerType.Individual, ai: 200, priceIrr: 2000);
        var third = await PlanAsync(SubscriptionOwnerType.Individual, ai: 300, priceIrr: 3000);
        var subscriptionId = await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), first, anchor);
        await SeedUsageAsync(actor, subscriptionId, first, 80, 10);
        var command = new CreateOrderCommand(second, Guid.NewGuid(), CommercialOrderPurpose.Upgrade, subscriptionId);
        var preview = await PostAsync<CommercialQuoteDto>(actor.Client, "/api/v1/commerce/personal/quote", command);
        Assert.Equal(150, preview.EffectiveQuotas.Single(q => q.Key == QuotaKey.AiTextRequests).Limit);
        Assert.Equal(60, preview.EffectiveQuotas.Single(q => q.Key == QuotaKey.AiTextRequests).Remaining);
        var order = await PostAsync<CreateOrderResult>(actor.Client, "/api/v1/commerce/personal/orders", command);
        Assert.Equal(500m, order.Order.Amount);
        var attempt = await PayAsync(actor, order.Order.OrderId);
        await VerifyAsync(attempt.PaymentAttemptId); await VerifyAsync(attempt.PaymentAttemptId);
        await factory.DrainNotificationOutboxAsync();
        var summary = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        var quota = summary.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests);
        Assert.Equal(subscriptionId, summary.SubscriptionId); Assert.Equal(end, summary.PeriodEndsAtUtc);
        Assert.Equal(150, quota.Limit); Assert.Equal(80, quota.Consumed); Assert.Equal(10, quota.Reserved); Assert.Equal(60, quota.Remaining);
        var nextOrder = await PostAsync<CreateOrderResult>(actor.Client, "/api/v1/commerce/personal/orders",
            new CreateOrderCommand(third, Guid.NewGuid(), CommercialOrderPurpose.Upgrade, subscriptionId));
        var nextAttempt = await PayAsync(actor, nextOrder.Order.OrderId); await VerifyAsync(nextAttempt.PaymentAttemptId);
        await factory.DrainNotificationOutboxAsync();
        var upgraded = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(200, upgraded.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Limit);
        Assert.Equal(2, await WithDb(db => db.SubscriptionAmendments.CountAsync(a => a.SubscriptionId == subscriptionId)));
        Assert.Equal(80, await WithDb(db => db.UsageReservations.CountAsync(r => r.SubscriptionId == subscriptionId && r.Status == UsageReservationStatus.Consumed)));
        var receipt = await GetAsync<OrderDto>(actor.Client, $"/api/v1/commerce/personal/orders/{order.Order.OrderId}");
        Assert.Equal(OrderFulfillmentState.SubscriptionActivated, receipt.FulfillmentState);
    }

    [Fact]
    public async Task EarlyPaidRenewalStaysInFutureAndScheduledDowngradeRetainsExistingCapacity()
    {
        var host = await ActorAsync(); var currentVersion = await PlanAsync(SubscriptionOwnerType.Individual, hosting: true, classes: 3);
        var low = await PlanAsync(SubscriptionOwnerType.Individual, hosting: true, classes: 1);
        var id = await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), currentVersion);
        for (var i = 0; i < 2; i++) await PostAsync<PersonalClassDto>(host.Client, "/api/v1/personal-classes", new CreatePersonalClassCommand("کلاس " + i, Guid.NewGuid()));
        var current = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
        var scheduled = await PostAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription/downgrade",
            new ScheduleDowngradeCommand(low, current.Version!, "کاهش آزمایشی برای دورهٔ بعد"));
        Assert.Equal(SubscriptionDisplayState.ScheduledDowngrade, scheduled.State);
        Assert.Equal(2, scheduled.Quotas.Single(q => q.Key == QuotaKey.ActiveClasses).Consumed);
        Assert.Equal(3, scheduled.Quotas.Single(q => q.Key == QuotaKey.ActiveClasses).Limit);
        var order = await PostAsync<CreateOrderResult>(host.Client, "/api/v1/commerce/personal/orders",
            new CreateOrderCommand(low, Guid.NewGuid(), CommercialOrderPurpose.Renewal, id));
        var attempt = await PayAsync(host, order.Order.OrderId); await VerifyAsync(attempt.PaymentAttemptId); await VerifyAsync(attempt.PaymentAttemptId);
        await factory.DrainNotificationOutboxAsync();
        var beforeStart = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(id, beforeStart.SubscriptionId); Assert.Equal(currentVersion, beforeStart.PlanVersionId);
        factory.Clock.SetUtcNow(current.PeriodEndsAtUtc!.Value.AddSeconds(1)); host = await ReauthenticateAsync(host);
        var next = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(low, next.PlanVersionId); Assert.Equal(1, next.Quotas.Single(q => q.Key == QuotaKey.ActiveClasses).Limit);
        Assert.Equal(2, next.Quotas.Single(q => q.Key == QuotaKey.ActiveClasses).Consumed);
        Assert.Equal(2, (await GetAsync<List<PersonalClassDto>>(host.Client, "/api/v1/personal-classes")).Count);
        using var denied = await host.Client.PostAsJsonAsync("/api/v1/personal-classes", new CreatePersonalClassCommand("کلاس مازاد", Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal(CommerceErrorCodes.QuotaExceeded, await ProblemAsync(denied));
        var existing = await GetAsync<List<PersonalClassDto>>(host.Client, "/api/v1/personal-classes");
        await PostAsync<PersonalClassDto>(host.Client, $"/api/v1/personal-classes/{existing[0].Id}/archive", null);
        Assert.Equal(1, (await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription")).Quotas.Single(q => q.Key == QuotaKey.ActiveClasses).Consumed);
    }

    [Fact]
    public async Task AdminRegistrationActivatesLockAfterGraceButReadMessagingAndAccessReductionContinue()
    {
        var manager = await ActorAsync(); var admin = await AdminAsync();
        var org = await factory.CreateOrganizationAsync("سازمان مهلت " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(manager.UserId, org, OrganizationRole.Manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var academic = await factory.CreateAcademicClassAsync(org);
        var people = await factory.CreateOrganizationPersonsAsync(org, 1);
        var enrollmentId = await factory.CreateEnrollmentAsync(org, academic.ClassId, people.Single());
        var version = await PlanAsync(SubscriptionOwnerType.Organization);
        var contract = await PostAsync<CommercialSummaryDto>(admin.Client, $"/api/admin/v1/commerce/organizations/{org}/commercial-contracts",
            new RegisterCommercialContractCommand(version, factory.Clock.GetUtcNow().UtcDateTime, 0, null, "ثبت صریح قرارداد معتبر برای کنترل تجاری"));
        Assert.True(contract.CommercialControlEnabled);
        factory.Clock.SetUtcNow(contract.PeriodEndsAtUtc!.Value.AddDays(1)); manager = await ReauthenticateAsync(manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var grace = await GetAsync<CommercialSummaryDto>(manager.Client, "/api/v1/commerce/organization/subscription");
        Assert.Equal(SubscriptionDisplayState.GracePeriod, grace.State);
        using var capacityInGrace = await manager.Client.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("تعهد تازه"), Json);
        Assert.Equal(HttpStatusCode.Conflict, capacityInGrace.StatusCode);
        factory.Clock.SetUtcNow(contract.PeriodEndsAtUtc.Value.AddDays(8)); manager = await ReauthenticateAsync(manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        using var locked = await manager.Client.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("درس تازه"), Json);
        Assert.Equal(CommerceErrorCodes.CommercialLocked, await ProblemAsync(locked));
        using var readable = await manager.Client.GetAsync("/api/v1/academic/classes"); readable.EnsureSuccessStatusCode();
        using var end = await manager.Client.PostAsJsonAsync($"/api/v1/academic/classes/{academic.ClassId}/enrollments/{enrollmentId}/end",
            new EndEnrollmentCommand(EnrollmentStatus.Withdrawn), Json); end.EnsureSuccessStatusCode();
        using var personal = await manager.Client.PostAsJsonAsync("/api/v1/workspaces/select",
            new MakanApp.Application.Organization.SelectWorkspaceCommand(WorkspaceType.Personal, null, null), Json);
        personal.EnsureSuccessStatusCode();
        var saved = await GetAsync<SavedConversationResult>(manager.Client, "/api/v1/conversations/saved");
        await PostAsync<MessageReceiptResult>(manager.Client, $"/api/v1/conversations/{saved.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام شخصی آزمایشی پس از پایان قرارداد" });
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var renewal = await PostAsync<CommercialQuoteDto>(manager.Client, "/api/v1/commerce/organization/quote",
            new CreateOrderCommand(version, Guid.NewGuid(), CommercialOrderPurpose.Renewal, contract.SubscriptionId));
        Assert.True(renewal.PriceToman > 0);
    }
}
