using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task Be17ManagedOrganizationRenewalNeedsCurrentManagerAndActivatesOnlyTheOrganizationsPaidPeriod()
    {
        Be17CurrentPolicy(); var admin = await AdminAsync(); var manager = await ActorAsync(); var teacher = await ActorAsync();
        var org = await factory.CreateOrganizationAsync("تمدید تجاری سازمان آزمون");
        var membership = await factory.CreateMembershipAsync(manager.UserId, org, OrganizationRole.Manager);
        var otherRole = await factory.CreateMembershipAsync(teacher.UserId, org, OrganizationRole.Teacher);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        await SelectAsync(teacher, otherRole.MembershipId, OrganizationRole.Teacher);
        var plan = await Be17PlanAsync(SubscriptionOwnerType.Organization);
        var current = await PostAsync<CommercialSummaryDto>(admin.Client, $"/api/admin/v1/commerce/organizations/{org}/commercial-contracts",
            new RegisterCommercialContractCommand(plan.VersionId, factory.Clock.GetUtcNow().UtcDateTime.AddDays(-10), 0, null, "قرارداد معتبر مصنوعی فقط در SQL ایزوله"));
        var command = new CreateOrderCommand(plan.VersionId, Guid.NewGuid(), CommercialOrderPurpose.Renewal, current.SubscriptionId);
        using var denied = await teacher.Client.PostAsJsonAsync("/api/v1/commerce/organization/quote", command, Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var preview = await PostAsync<CommercialQuoteDto>(manager.Client, "/api/v1/commerce/organization/quote", command);
        Assert.Equal(1_250m, preview.PriceToman);
        var order = await PostAsync<CreateOrderResult>(manager.Client, "/api/v1/commerce/orders", command);
        Assert.Equal(org, order.Order.OrganizationId); Assert.Equal(12_500m, order.Order.Amount);
        var attempt = await PostAsync<InitiatePaymentResult>(manager.Client, $"/api/v1/commerce/orders/{order.Order.OrderId}/payments", new InitiatePaymentCommand(Guid.NewGuid()));
        await VerifyAsync(attempt.PaymentAttempt.PaymentAttemptId); await VerifyAsync(attempt.PaymentAttempt.PaymentAttemptId);
        await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountPaymentsAsync(order.Order.OrderId));
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(order.Order.OrderId));
        var before = await GetAsync<CommercialSummaryDto>(manager.Client, "/api/v1/commerce/organization/subscription");
        Assert.Equal(current.SubscriptionId, before.SubscriptionId);
        factory.Clock.SetUtcNow(current.PeriodEndsAtUtc!.Value.AddTicks(1));
        manager = await ReauthenticateAsync(manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var next = await GetAsync<CommercialSummaryDto>(manager.Client, "/api/v1/commerce/organization/subscription");
        Assert.NotEqual(current.SubscriptionId, next.SubscriptionId); Assert.Equal(org, next.OwnerId);
        Assert.True(next.HasVerifiedCommercialContract); Assert.False(next.AutoRenew); Assert.Equal("Manual", next.RenewalMode);
        Assert.Equal(plan.VersionId, next.PlanVersionId);
        var personal = await GetAsync<CommercialSummaryDto>(manager.Client, "/api/v1/commerce/personal/subscription");
        Assert.Null(personal.SubscriptionId);
    }

    [Fact]
    public async Task Be17ParentAndTwoChildrenNeverShareSubscriptionQuotaOrdersOrPayments()
    {
        Be17CurrentPolicy();
        var parent = await ActorAsync(); var childA = await ActorAsync(); var childB = await ActorAsync();
        foreach (var actor in new[] { parent, childA, childB })
        {
            using var profile = await actor.Client.PatchAsJsonAsync("/api/v1/me/profile",
                new CompleteProfileCommand("کاربر", "آزمون خانوادگی", null, "be17." + Guid.NewGuid().ToString("N")[..20],
                    new DateOnly(actor.UserId == parent.UserId ? 1985 : 2012, 1, 1)), Json);
            profile.EnsureSuccessStatusCode();
        }
        var org = await factory.CreateOrganizationAsync("خانوادهٔ مصنوعی بدون اشتراک مشترک");
        await factory.CreateMembershipAsync(parent.UserId, org, OrganizationRole.Parent);
        await factory.CreateMembershipAsync(childA.UserId, org, OrganizationRole.Student);
        await factory.CreateMembershipAsync(childB.UserId, org, OrganizationRole.Student);
        var personA = await factory.CreateOrganizationPersonForUserAsync(org, childA.UserId);
        var personB = await factory.CreateOrganizationPersonForUserAsync(org, childB.UserId);
        var relations = await WithDb(async db =>
        {
            var now = factory.Clock.GetUtcNow().UtcDateTime;
            var a = GuardianRelation.CreateActive(org, parent.UserId, personA, now);
            var b = GuardianRelation.CreateActive(org, parent.UserId, personB, now);
            db.GuardianRelations.AddRange(a, b); await db.SaveChangesAsync();
            Assert.True(a.IsActiveAt(now)); Assert.True(b.IsActiveAt(now)); return new[] { a.Id, b.Id };
        });
        var userIds = new[] { parent.UserId, childA.UserId, childB.UserId };
        var membershipsBefore = await WithDb(db => db.Memberships.CountAsync(m => userIds.Contains(m.UserId)));
        var rolesBefore = await WithDb(db => db.RoleAssignments.CountAsync(r => db.Memberships.Any(m => m.Id == r.MembershipId && userIds.Contains(m.UserId))));
        var orders = new List<CreateOrderResult>();
        foreach (var (actor, limit) in new[] { (parent, 5L), (childA, 2L), (childB, 1L) })
        {
            var plan = await Be17PlanAsync(SubscriptionOwnerType.Individual, aiLimit: limit);
            var order = await PostAsync<CreateOrderResult>(actor.Client, "/api/v1/commerce/personal/orders", new CreateOrderCommand(plan.VersionId, Guid.NewGuid()));
            var attempt = await PayAsync(actor, order.Order.OrderId); await VerifyAsync(attempt.PaymentAttemptId);
            orders.Add(order);
        }
        await factory.DrainNotificationOutboxAsync();
        Assert.Equal(membershipsBefore, await WithDb(db => db.Memberships.CountAsync(m => userIds.Contains(m.UserId))));
        Assert.Equal(rolesBefore, await WithDb(db => db.RoleAssignments.CountAsync(r => db.Memberships.Any(m => m.Id == r.MembershipId && userIds.Contains(m.UserId)))));
        var context = new AiQuotaPreflightCommand(CommerceContextType.Personal);
        var childUse = await ReserveAsync(childA, context, Guid.NewGuid());
        await FinishAsync(childA, childUse, UsageReservationStatus.Consumed);
        foreach (var (actor, expected) in new[] { (parent, 5L), (childA, 1L), (childB, 1L) })
        {
            var summary = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
            Assert.Equal(actor.UserId, summary.OwnerId);
            Assert.Equal(expected, summary.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Remaining);
        }
        var selected = await GetAsync<CommercialSummaryDto>(parent.Client, $"/api/v1/commerce/personal/subscription?userId={childA.UserId}&childId={personA}");
        Assert.Equal(parent.UserId, selected.OwnerId);
        foreach (var order in orders.Skip(1))
        {
            using var read = await parent.Client.GetAsync($"/api/v1/commerce/personal/orders/{order.Order.OrderId}");
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            using var payment = await parent.Client.PostAsJsonAsync($"/api/v1/commerce/personal/orders/{order.Order.OrderId}/payments", new InitiatePaymentCommand(Guid.NewGuid()), Json);
            Assert.Equal(HttpStatusCode.NotFound, payment.StatusCode);
        }
        using var childReadsParent = await childA.Client.GetAsync($"/api/v1/commerce/personal/orders/{orders[0].Order.OrderId}");
        Assert.Equal(HttpStatusCode.NotFound, childReadsParent.StatusCode);
        using var forbiddenContext = await parent.Client.PostAsJsonAsync("/api/v1/commerce/ai/simple-text/preflight",
            new AiQuotaPreflightCommand(CommerceContextType.Personal, childA.UserId, AiPayerSelection.Individual), Json);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenContext.StatusCode);
        await WithDb(async db =>
        {
            foreach (var relation in await db.GuardianRelations.Where(r => relations.Contains(r.Id)).ToArrayAsync()) relation.End(factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        });
        var after = await GetAsync<CommercialSummaryDto>(parent.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(5, after.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Remaining);
        Assert.Equal(3, await WithDb(db => db.Subscriptions.CountAsync(s => s.OwnerType == SubscriptionOwnerType.Individual && userIds.Contains(s.OwnerId))));
    }
}
