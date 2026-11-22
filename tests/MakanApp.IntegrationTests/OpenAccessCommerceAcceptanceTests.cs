using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenAccessDoesNotHideInvalidAiUsageConfiguration(bool commercialEnforcementEnabled)
    {
        using var host = factory.WithAiUsageMode((AiUsageMode)9876, commercialEnforcementEnabled: commercialEnforcementEnabled);
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    [Fact]
    public async Task OpenAccessOpenApiSeparatesEnforcementFromHistoricalSubscriptionData()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "CommercialSummaryDto", "ManagerCommerceSummaryDto" })
        {
            var schema = Assert.Single(schemas.EnumerateObject(), item => item.Name.EndsWith(name, StringComparison.Ordinal)).Value;
            var enforcement = schema.GetProperty("properties").GetProperty("enforcementEnabled");
            Assert.Equal("boolean", enforcement.GetProperty("type").GetString());
            Assert.Contains("Subscription", enforcement.GetProperty("description").GetString());
        }
    }

    [Fact]
    public async Task OpenAccessOrganizationWritesIgnoreCancelledCommercialContractButPreserveManagerAuthorization()
    {
        var legacy = await LegacyOrganizationAsync(incomplete: false);
        var before = await GetAsync<CommercialSummaryDto>(legacy.Manager.Client, "/api/v1/commerce/organization/subscription");
        var admin = await AdminAsync();
        var version = await PlanAsync(SubscriptionOwnerType.Organization);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var contract = await PostAsync<CommercialSummaryDto>(admin.Client,
            $"/api/admin/v1/commerce/organizations/{legacy.OrganizationId}/commercial-contracts",
            new RegisterCommercialContractCommand(version, now.AddDays(-1), 0, before.Version, "قرارداد صریح تست ایزوله"));
        await WithDb(async db =>
        {
            (await db.Subscriptions.SingleAsync(s => s.Id == contract.SubscriptionId)).Cancel(now);
            await db.SaveChangesAsync();
        });
        using (var blocked = await legacy.Manager.Client.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("نوشتن با کنترل تجاری روشن"), Json))
        {
            Assert.False(blocked.IsSuccessStatusCode);
            Assert.Equal(CommerceErrorCodes.CommercialLocked, await ProblemAsync(blocked));
        }
        using var host = factory.WithAiUsageMode(AiUsageMode.Commercial, commercialEnforcementEnabled: false);
        using var manager = host.CreateClient();
        manager.DefaultRequestHeaders.Authorization = legacy.Manager.Client.DefaultRequestHeaders.Authorization;
        using (var created = await manager.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("درس مجاز بدون کنترل اشتراک"), Json))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var course = (await created.Content.ReadFromJsonAsync<CourseResult>(Json))!;
            Assert.Equal(legacy.OrganizationId, course.OrganizationId);
        }
        var summary = await GetAsync<CommercialSummaryDto>(manager, "/api/v1/commerce/organization/subscription");
        Assert.False(summary.EnforcementEnabled);
        Assert.Equal(contract.SubscriptionId, summary.SubscriptionId);
        var outsider = await ActorAsync();
        using var outsiderClient = host.CreateClient();
        outsiderClient.DefaultRequestHeaders.Authorization = outsider.Client.DefaultRequestHeaders.Authorization;
        using (var denied = await outsiderClient.PostAsJsonAsync("/api/v1/academic/courses", new CreateCourseCommand("درس غیرمجاز"), Json))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal(2, await db.Subscriptions.CountAsync(s => s.OwnerId == legacy.OrganizationId));
            Assert.Equal(SubscriptionStatus.Cancelled, (await db.Subscriptions.SingleAsync(s => s.Id == contract.SubscriptionId)).Status);
            Assert.False(await db.Courses.AnyAsync(c => c.OrganizationId == legacy.OrganizationId && c.Title == "درس غیرمجاز"));
        });
    }

    [Fact]
    public async Task OpenAccessPersonalClassNeedsNoHostPlanButRetainsReviewManagerConsentAndEducationalCapacity()
    {
        var owner = await ActorAsync(); var learner = await ActorAsync(); var extraLearner = await ActorAsync(); var admin = await AdminAsync();
        using var host = factory.WithAiUsageMode(AiUsageMode.Commercial, commercialEnforcementEnabled: false);
        using var ownerClient = host.CreateClient(); using var adminClient = host.CreateClient();
        using var learnerClient = host.CreateClient(); using var extraClient = host.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
        adminClient.DefaultRequestHeaders.Authorization = admin.Client.DefaultRequestHeaders.Authorization;
        learnerClient.DefaultRequestHeaders.Authorization = learner.Client.DefaultRequestHeaders.Authorization;
        extraClient.DefaultRequestHeaders.Authorization = extraLearner.Client.DefaultRequestHeaders.Authorization;
        var openOwner = owner with { Client = ownerClient };
        var classroom = await ApprovedClassAsync(openOwner, "کلاس بدون پلن با ظرفیت آموزشی صریح", capacity: 1,
            reviewer: admin with { Client = adminClient });
        var invite = await InviteAsync(openOwner, learner, classroom.Id);
        using (var denied = await extraClient.PostAsJsonAsync($"/api/v1/personal-classes/{classroom.Id}/invitations",
            new InvitePersonalClassStudentCommand(learner.UserId, Guid.NewGuid()), Json))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var joined = await PostAsync<PersonalClassDto>(learnerClient, $"/api/v1/personal-classes/invitations/{invite.Id}/accept", null);
        Assert.Equal(classroom.Id, joined.Id);
        var extra = await InviteAsync(openOwner, extraLearner, classroom.Id);
        using (var full = await extraClient.PostAsync($"/api/v1/personal-classes/invitations/{extra.Id}/accept", null))
        {
            Assert.False(full.IsSuccessStatusCode);
            Assert.Equal(AcademicErrorCodes.PersonalClassCapacityReached, await ProblemAsync(full));
        }
        var summary = await GetAsync<CommercialSummaryDto>(ownerClient, "/api/v1/commerce/personal/subscription");
        Assert.False(summary.EnforcementEnabled); Assert.Null(summary.SubscriptionId); Assert.Empty(summary.Quotas);
        await WithDb(async db =>
        {
            Assert.False(await db.Subscriptions.AnyAsync(s => s.OwnerId == owner.UserId));
            Assert.False(await db.Orders.AnyAsync(o => o.PurchaserUserId == owner.UserId));
            Assert.False(await db.UsageReservations.AnyAsync(r => r.ConsumerUserId == owner.UserId));
        });
    }
}
