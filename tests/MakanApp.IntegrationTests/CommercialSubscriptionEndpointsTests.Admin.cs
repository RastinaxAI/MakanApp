using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task AdminCannotPublishUnsetTermsAndSchoolManagerCannotManageGlobalPlans()
    {
        var admin = await AdminAsync(); var manager = await ActorAsync();
        var org = await factory.CreateOrganizationAsync("سازمان مجوز " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(manager.UserId, org, OrganizationRole.Manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        var create = new CreatePlatformPlanCommand("draft-test." + Guid.NewGuid().ToString("N"), "پیش‌نویس آزمون", "تعریف پیش‌نویس", SubscriptionOwnerType.Individual);
        using var denied = await manager.Client.PostAsJsonAsync("/api/admin/v1/commerce/plans", create, Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var plan = await PostAsync<PlatformPlanDetail>(admin.Client, "/api/admin/v1/commerce/plans", create);
        var draft = await PostAsync<PlatformPlanVersionItem>(admin.Client, $"/api/admin/v1/commerce/plans/{plan.PlanId}/versions",
            new CreatePlatformPlanVersionCommand(null, CurrencyCode.Irr, [new(EntitlementKey.Ai, true)], [], "مقادیر هنوز تعیین نشده‌اند"));
        var validation = await GetAsync<CommercialValidationDto>(admin.Client, $"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/validation");
        Assert.False(validation.CanPublish); Assert.Contains("Price", validation.MissingFields); Assert.Contains("Quota:AiTextRequests", validation.MissingFields);
        using var rejected = await admin.Client.PostAsJsonAsync($"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/publish",
            new PublishPlatformPlanVersionCommand(draft.Version, "تلاش انتشار ناقص"), Json);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var excessivePrice = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/draft",
            new CommercialDraftCommand(decimal.MaxValue, false, BillingCalendar.Persian, "test-only-price-validation", null, null,
                [new(EntitlementKey.Ai, true)], [new(QuotaKey.AiTextRequests, null)], validation.Version, "بررسی مبلغ خارج از محدوده بدون سرریز"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, excessivePrice.StatusCode);
        Assert.Equal(CommerceErrorCodes.UsageInvalid, await ProblemAsync(excessivePrice));
        using var missingLimit = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/draft", new
        {
            PriceToman = 0, ExplicitlyFree = true, Calendar = BillingCalendar.Persian, SaleTermsReference = "test-only-required-limit",
            Entitlements = new[] { new { Key = EntitlementKey.Ai, Enabled = true } },
            Quotas = new[] { new { Key = QuotaKey.AiTextRequests } }, ExpectedVersion = validation.Version,
            Reason = "حذف Limit نباید Unlimited بسازد"
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, missingLimit.StatusCode);
        using var filled = await admin.Client.PutAsJsonAsync($"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/draft",
            new CommercialDraftCommand(0, true, BillingCalendar.Persian, "test-only-free-terms", null, null,
                [new(EntitlementKey.Ai, true)], [new(QuotaKey.AiTextRequests, null)], validation.Version, "رایگان و نامحدود به‌صورت صریح فقط برای آزمون"), Json);
        filled.EnsureSuccessStatusCode();
        var ready = (await filled.Content.ReadFromJsonAsync<CommercialValidationDto>(Json))!;
        Assert.True(ready.CanPublish); Assert.Empty(ready.MissingFields);
        var published = await PostAsync<PlatformPlanVersionItem>(admin.Client, $"/api/admin/v1/commerce/plan-versions/{draft.PlanVersionId}/publish",
            new PublishPlatformPlanVersionCommand(ready.Version, "انتشار نسخهٔ آزمون کامل"));
        Assert.Equal(PlanVersionStatus.Published, published.Status); Assert.Equal(0, published.PriceAmount);
    }

    [Fact]
    public async Task LosingOrganizationRoleDoesNotInvalidateIndividualContractOrPersonalClassHostAccess()
    {
        var actor = await ActorAsync(); var org = await factory.CreateOrganizationAsync("سازمان قبلی " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Teacher);
        await SelectAsync(actor, membership.MembershipId, OrganizationRole.Teacher);
        var version = await PlanAsync(SubscriptionOwnerType.Individual, hosting: true);
        var subscription = await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), version);
        await WithDb(async db =>
        {
            var entity = await db.Memberships.SingleAsync(m => m.Id == membership.MembershipId);
            entity.End(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync();
        });
        var personal = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(subscription, personal.SubscriptionId); Assert.Equal(actor.UserId, personal.OwnerId);
        var classroom = await PostAsync<MakanApp.Application.Academic.PersonalClassDto>(actor.Client, "/api/v1/personal-classes",
            new MakanApp.Application.Academic.CreatePersonalClassCommand("کلاس مستقل پس از پایان نقش", Guid.NewGuid()));
        Assert.Equal(actor.UserId, classroom.HostUserId);
        using var organization = await actor.Client.GetAsync("/api/v1/commerce/organization/subscription");
        Assert.Equal(HttpStatusCode.Forbidden, organization.StatusCode);
    }
}
