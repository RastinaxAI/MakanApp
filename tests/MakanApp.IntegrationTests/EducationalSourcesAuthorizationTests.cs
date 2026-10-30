using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task EducationalSourcesLibraryCatalogIsMetadataOnlyHiddenDoesNotBreakReferenceAndSchoolCannotManage()
    {
        await factory.ResetPlatformIamAsync();
        using var owner = factory.CreateClient(); var ownerAccount = await CreateMessagingUserAsync(owner);
        await factory.BootstrapPlatformOwnerAsync(ownerAccount.UserId);
        using var reader = factory.CreateClient(); await CreateMessagingUserAsync(reader);
        using var school = await CreateOfficialScenarioAsync(false);
        var file = await SourceUpload(owner, true);
        using var created = await owner.PostAsJsonAsync("/api/admin/v1/library/sources", SourceCommand(EducationalSourceScope.Library, file.Id), JsonOptions);
        created.EnsureSuccessStatusCode(); var source = (await created.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        using (var draft = await reader.GetAsync($"/api/v1/sources/{source.SourceId}")) Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        source = await SourcePublish(owner, source);
        using (var hidden = await reader.GetAsync("/api/v1/sources?scope=Library"))
            Assert.DoesNotContain((await hidden.Content.ReadFromJsonAsync<SourcePage>(JsonOptions))!.Items, x => x.SourceId == source.SourceId);
        using (var show = await owner.PutAsJsonAsync($"/api/v1/sources/{source.SourceId}/visibility", new SourceVisibilityCommand(SourceVisibility.Public, source.Version, "نمایش ساختگی"), JsonOptions))
        { show.EnsureSuccessStatusCode(); source = (await show.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!; }
        using (var catalog = await reader.GetAsync("/api/v1/sources?scope=Library"))
        {
            var item = Assert.Single((await catalog.Content.ReadFromJsonAsync<SourcePage>(JsonOptions))!.Items, x => x.SourceId == source.SourceId);
            Assert.Null(item.File); Assert.False(item.Capabilities.CanDownload); Assert.False(item.Capabilities.CanUseForAi);
        }
        using (var content = await reader.GetAsync($"/api/v1/sources/{source.SourceId}/content")) Assert.Equal(HttpStatusCode.Forbidden, content.StatusCode);
        using (var raw = await reader.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.NotFound, raw.StatusCode);
        using (var manage = await school.Manager.GetAsync("/api/admin/v1/library/sources")) Assert.Equal(HttpStatusCode.Forbidden, manage.StatusCode);
        using (var mutate = await school.Manager.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/archive", new SourceMutationCommand(source.Version, "آزمون رد مدیر مدرسه"), JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, mutate.StatusCode);
        using (var hide = await owner.PutAsJsonAsync($"/api/v1/sources/{source.SourceId}/visibility", new SourceVisibilityCommand(SourceVisibility.Hidden, source.Version, "پنهان‌کردن"), JsonOptions))
            hide.EnsureSuccessStatusCode();
        using var stable = await reader.GetAsync($"/api/v1/sources/{source.SourceId}?revision=1"); Assert.Equal(HttpStatusCode.OK, stable.StatusCode);
        await factory.ResetPlatformIamAsync();
    }
    [Fact]
    public async Task EducationalSourcesCatalogSearchAndPaginationAreServerSideStableAndDoNotGrantClassBWrite()
    {
        using var s = await CreateOfficialScenarioAsync(false); await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 10);
        for (var i = 0; i < 3; i++) await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId) with { Title = "كتاب فارسي " + i });
        using var page = await s.Teacher.GetAsync($"/api/v1/sources?scope=Class&classId={s.ClassId}&search=" + Uri.EscapeDataString("کتاب فارسی") + "&page=2&pageSize=1");
        page.EnsureSuccessStatusCode(); var result = (await page.Content.ReadFromJsonAsync<SourcePage>(JsonOptions))!;
        Assert.Equal(3, result.TotalCount); Assert.Equal(2, result.Page); Assert.Single(result.Items);
        using var repeat = await s.Teacher.GetAsync($"/api/v1/sources?scope=Class&classId={s.ClassId}&page=2&pageSize=1");
        Assert.Equal(result.Items[0].SourceId, (await repeat.Content.ReadFromJsonAsync<SourcePage>(JsonOptions))!.Items[0].SourceId);
        var other = await factory.CreateAcademicClassAsync(s.OrganizationId);
        using var denied = await s.Teacher.PostAsJsonAsync("/api/v1/sources", SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, other.ClassId), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    [Fact]
    public async Task EducationalSourcesOrganizationQuotaSerializesDifferentActorsAndNeverChargesTeachersPersonalContract()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 1); await SourceContract(SubscriptionOwner.Individual(s.TeacherId), 10);
        var manager = SourceCommand(EducationalSourceScope.Organization, (await SourceUpload(s.Manager, false)).Id);
        var teacher = SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId);
        var results = await Task.WhenAll(s.Manager.PostAsJsonAsync("/api/v1/sources", manager, JsonOptions), s.Teacher.PostAsJsonAsync("/api/v1/sources", teacher, JsonOptions));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in results) response.Dispose();
        await using var scope = factory.Services.CreateAsyncScope(); var commerce = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Commerce.EfCommercialStore>();
        var org = await commerce.SummaryAsync(SubscriptionOwner.Organization(s.OrganizationId), factory.Clock.GetUtcNow().UtcDateTime, default);
        var personal = await commerce.SummaryAsync(SubscriptionOwner.Individual(s.TeacherId), factory.Clock.GetUtcNow().UtcDateTime, default);
        Assert.Equal(1, org.Quotas.Single(q => q.Key == QuotaKey.SourceCount).Consumed); Assert.Equal(0, personal.Quotas.Single(q => q.Key == QuotaKey.SourceCount).Consumed);
    }
    [Fact]
    public async Task EducationalSourcesPersonalReplayIsPrivateAndPublishedClassReplayRechecksEnrollment()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        await SourceContract(SubscriptionOwner.Individual(s.TeacherId), 5); await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 5);
        var teacherCursor = await SourceCursor(s.Teacher, true); var otherCursor = await SourceCursor(s.Student, true);
        var personal = await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(s.Teacher, true)).Id));
        var own = await SourceChanges(s.Teacher, teacherCursor, true); var other = await SourceChanges(s.Student, otherCursor, true);
        Assert.Contains(own.Changes, c => c.Invalidation?.Key == "knowledge.source" && c.Invalidation.ResourceId == personal.SourceId);
        Assert.DoesNotContain(other.Changes, c => c.Invalidation?.ResourceId == personal.SourceId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var record = await db.OutboxMessages.SingleAsync(o => o.MessageType == MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter.MessageType && o.Payload.Contains(personal.SourceId.ToString()));
            var recipients = new List<(Guid SessionId, RealtimeInvalidation Notification)>();
            await foreach (var item in scope.ServiceProvider.GetRequiredService<IRealtimeInvalidationAudience>().ResolveAsync(record.Id, record.Payload, default)) recipients.Add(item);
            Assert.NotEmpty(recipients); Assert.All(recipients, r => Assert.Equal(own.ContextRevision, r.Notification.ContextRevision));
        }
        var studentCursor = await SourceCursor(s.Student, false);
        var classroom = await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId));
        var drafts = await SourceChanges(s.Student, studentCursor, false);
        Assert.DoesNotContain(drafts.Changes, c => c.Invalidation?.ResourceId == classroom.SourceId);
        classroom = await SourcePublish(s.Teacher, classroom);
        var published = await SourceChanges(s.Student, drafts.NextCursor, false);
        Assert.Contains(published.Changes, c => c.Invalidation?.ResourceId == classroom.SourceId);
        await factory.EndEnrollmentAsync(s.EnrollmentId);
        using var denied = await s.Student.GetAsync($"/api/v1/sources/{classroom.SourceId}/content"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    private static async Task<string> SourceCursor(HttpClient client, bool personal)
    {
        using var response = await client.GetAsync("/api/v1/realtime/changes" + (personal ? "?scope=Personal" : "")); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountChangePageResult>(JsonOptions))!.NextCursor;
    }
    private static async Task<AccountChangePageResult> SourceChanges(HttpClient client, string cursor, bool personal)
    {
        using var response = await client.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(cursor) + (personal ? "&scope=Personal" : ""));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<AccountChangePageResult>(JsonOptions))!;
    }
}
