using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MakanApp.Application.Commerce;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task EducationalSourcesPersonalScopeRemainsPrivateWhileEducationalRoleAndArchiveNeverReleaseGuessedCapacity()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        await SourceContract(SubscriptionOwner.Individual(s.TeacherId), 1);
        var file = await SourceUpload(s.Teacher, true);
        var command = SourceCommand(EducationalSourceScope.Personal, file.Id);
        var source = await SourceCreate(s.Teacher, command);
        Assert.True(source.SourceReady); Assert.False(source.AiReady); Assert.Equal(SourceProcessingStatus.NotRequested, source.ProcessingStatus);
        Assert.Equal(s.TeacherId, source.OwnerUserId); Assert.Null(source.OrganizationId);
        Assert.Equal(source.SourceId, (await SourceCreate(s.Teacher, command)).SourceId);
        foreach (var route in new[] { $"/api/v1/sources/{source.SourceId}", $"/api/v1/sources/{source.SourceId}/content", $"/api/v1/files/{file.Id}/content" })
        { using var denied = await s.Student.GetAsync(route); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        using (var ownDownload = await s.Teacher.GetAsync($"/api/v1/sources/{source.SourceId}/content")) Assert.Equal(HttpStatusCode.OK, ownDownload.StatusCode);
        using (var stolenUpdate = await s.Student.PatchAsJsonAsync($"/api/v1/sources/{source.SourceId}/draft", new UpdateSourceDraftCommand("تغییر غیرمجاز", null, source.Version, "آزمون"), JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, stolenUpdate.StatusCode);
        using (var stolenArchive = await s.Student.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/archive", new SourceMutationCommand(source.Version, "آزمون"), JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, stolenArchive.StatusCode);
        using var archived = await s.Teacher.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/archive", new SourceMutationCommand(source.Version, "آرشیو ساختگی"), JsonOptions);
        archived.EnsureSuccessStatusCode();
        var secondFile = await SourceUpload(s.Teacher, true);
        using var full = await s.Teacher.PostAsJsonAsync("/api/v1/sources", SourceCommand(EducationalSourceScope.Personal, secondFile.Id), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode); Assert.Contains(SourceErrorCodes.QuotaExceeded, await full.Content.ReadAsStringAsync());
        using var storageDelete = await s.Teacher.DeleteAsync($"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(file.RowVersion)}");
        Assert.Equal(HttpStatusCode.Conflict, storageDelete.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var quota = await scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Commerce.EfCommercialStore>()
            .SummaryAsync(SubscriptionOwner.Individual(s.TeacherId), factory.Clock.GetUtcNow().UtcDateTime, default);
        Assert.Equal(1, quota.Quotas.Single(q => q.Key == QuotaKey.SourceCount).Consumed);
        Assert.False(await db.EducationalSources.AnyAsync(x => x.OrganizationId == s.OrganizationId));
    }
    [Fact]
    public async Task EducationalSourcesConcurrentLastSlotAndLostAckCreateExactlyOneSourceWithoutPooling()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 1);
        var one = SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id);
        var two = SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id);
        var results = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/sources", one, JsonOptions), client.PostAsJsonAsync("/api/v1/sources", two, JsonOptions));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var accepted = results.Single(r => r.StatusCode == HttpStatusCode.Created); var original = (await accepted.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        var replay = await SourceCreate(client, original.File!.FileAssetId == one.FileAssetId ? one : two);
        Assert.Equal(original.SourceId, replay.SourceId); Assert.True(replay.Replayed);
        foreach (var response in results) response.Dispose();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.EducationalSources.CountAsync(s => s.OwnerUserId == user.UserId));
    }
    [Fact]
    public async Task EducationalSourcesClassDraftPublicationVersioningAndRevocationGuardBothSourceAndRawFile()
    {
        using var s = await CreateOfficialScenarioAsync(false); await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 10);
        var file = await SourceUpload(s.Teacher, false);
        var source = await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, file.Id, s.ClassId));
        using (var draft = await s.Student.GetAsync($"/api/v1/sources/{source.SourceId}")) Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        using (var rawDraft = await s.Student.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.NotFound, rawDraft.StatusCode);
        source = await SourcePublish(s.Teacher, source);
        using (var published = await s.Student.GetAsync($"/api/v1/sources/{source.SourceId}/content")) Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using (var rawPublished = await s.Student.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.OK, rawPublished.StatusCode);
        using (var mutatePublished = await s.Teacher.PatchAsJsonAsync($"/api/v1/sources/{source.SourceId}/draft", new UpdateSourceDraftCommand("نباید تاریخچه تغییر کند", null, source.Version, "آزمون"), JsonOptions))
            Assert.Equal(HttpStatusCode.Conflict, mutatePublished.StatusCode);
        var replacement = await SourceUpload(s.Teacher, false);
        using var next = await s.Teacher.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/versions", new CreateSourceVersionCommand(replacement.Id, "نسخه دوم", null, source.Version, "نسخه تازه"), JsonOptions);
        next.EnsureSuccessStatusCode(); var draftVersion = (await next.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        Assert.Equal(source.SourceId, draftVersion.SourceId); Assert.Equal(2, draftVersion.Revision);
        using (var student = await s.Student.GetAsync($"/api/v1/sources/{source.SourceId}"))
            Assert.Equal(1, (await student.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!.Revision);
        using (var studentWrite = await s.Student.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/publish", new SourceMutationCommand(draftVersion.Version, "آزمون"), JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, studentWrite.StatusCode);
        using (var stale = await s.Teacher.PatchAsJsonAsync($"/api/v1/sources/{source.SourceId}/draft", new UpdateSourceDraftCommand("مقادیر کهنه", null, source.Version, "آزمون"), JsonOptions))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await factory.EndEnrollmentAsync(s.EnrollmentId);
        foreach (var route in new[] { $"/api/v1/sources/{source.SourceId}", $"/api/v1/sources/{source.SourceId}/content", $"/api/v1/files/{file.Id}/content" })
        { using var denied = await s.Student.GetAsync(route); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        await factory.EndTeacherAssignmentAsync(s.TeacherAssignmentId);
        using (var ended = await s.Teacher.GetAsync($"/api/v1/sources/{source.SourceId}")) Assert.Equal(HttpStatusCode.NotFound, ended.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.AcademicActivityPublications.AnyAsync(x => x.ClassId == s.ClassId));
        Assert.Equal(s.OrganizationId, (await db.EducationalSources.SingleAsync(x => x.Id == source.SourceId)).OrganizationId);
    }
    [Fact]
    public async Task EducationalSourcesOrganizationReadWriteIsManagerOnlyAndCannotUseAnotherUsersFile()
    {
        using var a = await CreateOfficialScenarioAsync(false); using var b = await CreateOfficialScenarioAsync(false);
        await SourceContract(SubscriptionOwner.Organization(a.OrganizationId), 10);
        var file = await SourceUpload(a.Manager, false); var source = await SourceCreate(a.Manager, SourceCommand(EducationalSourceScope.Organization, file.Id));
        source = await SourcePublish(a.Manager, source);
        foreach (var caller in new[] { b.Manager, b.Teacher, a.Teacher, a.Student })
        { using var denied = await caller.GetAsync($"/api/v1/sources/{source.SourceId}"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        var stolen = await SourceUpload(a.Teacher, false);
        using var invalid = await a.Manager.PostAsJsonAsync("/api/v1/sources", SourceCommand(EducationalSourceScope.Organization, stolen.Id), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);
    }
    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    public async Task EducationalSourcesPendingOrRejectedFilesNeverBecomeReady(FileAssetStatus status)
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10); Guid fileId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = factory.Clock.GetUtcNow().UtcDateTime;
            var file = FileAsset.CreatePending(Guid.NewGuid(), null, user.UserId, "fixture.pdf", Guid.NewGuid().ToString("N"), "application/pdf", now, now.AddHours(1));
            if (status == FileAssetStatus.Rejected) file.MarkRejected(now);
            db.FileAssets.Add(file); await db.SaveChangesAsync(); fileId = file.Id;
        }
        using var response = await client.PostAsJsonAsync("/api/v1/sources", SourceCommand(EducationalSourceScope.Personal, fileId), JsonOptions);
        if (status == FileAssetStatus.Rejected) { Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); return; }
        response.EnsureSuccessStatusCode(); var source = (await response.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        Assert.False(source.FileReady); Assert.False(source.SourceReady); Assert.False(source.AiReady);
        using var publish = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/publish", new SourceMutationCommand(source.Version, "آزمون"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
        var replacement = await SourceUpload(client, true);
        using var next = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/versions",
            new CreateSourceVersionCommand(replacement.Id, "بازیابی منبع Pending", null, source.Version, "فایل جایگزین"), JsonOptions);
        next.EnsureSuccessStatusCode(); var recovered = (await next.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        Assert.Equal(source.SourceId, recovered.SourceId); Assert.Equal(2, recovered.Revision); Assert.True(recovered.SourceReady);
    }
    private static CreateEducationalSourceCommand SourceCommand(EducationalSourceScope scope, Guid file, Guid? classId = null) =>
        new(Guid.NewGuid(), scope, classId, "كتاب فارسي منبع", "منبع ساختگی آزمون", file, "آزمون ایزوله");
    private async Task<EducationalSourceResult> SourceCreate(HttpClient client, CreateEducationalSourceCommand command)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/sources", command, JsonOptions);
        Assert.True(response.IsSuccessStatusCode, $"Source create returned {(int)response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
    }
    private static async Task<EducationalSourceResult> SourcePublish(HttpClient client, EducationalSourceResult source)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/publish", new SourceMutationCommand(source.Version, "انتشار ساختگی"), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
    }
    private async Task<FileAssetResult> SourceUpload(HttpClient client, bool personal)
    {
        using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7\nSynthetic fixture\n%%EOF"));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(content, "file", "synthetic.pdf");
        using var response = await client.PostAsync("/api/v1/files" + (personal ? "?workspaceScope=Personal" : ""), form);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
    }
    private async Task SourceContract(SubscriptionOwner owner, long limit)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var plan = Plan.Create("source-test." + Guid.NewGuid().ToString("N"), "قرارداد ساختگی منبع", now.AddDays(-2), owner.Type);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-2)); version.SetMonthlyTerms(BillingCalendar.Gregorian, "isolated-fixture-not-for-sale", 80, 95);
        version.SetPrice(1000, CurrencyCode.Irr); version.SetEntitlement(owner.Type == SubscriptionOwnerType.Individual ? EntitlementKey.PersonalSources : EntitlementKey.Academic, true);
        version.SetEntitlement(EntitlementKey.Assignments, owner.Type == SubscriptionOwnerType.Organization); version.SetEntitlement(EntitlementKey.Exams, owner.Type == SubscriptionOwnerType.Organization);
        version.SetQuota(QuotaKey.SourceCount, limit); version.SetQuota(QuotaKey.StorageBytes, 1000000); version.SetQuota(QuotaKey.ActiveClasses, 100); version.SetQuota(QuotaKey.ActiveLearners, 100);
        version.Publish(now.AddDays(-1)); db.Plans.Add(plan); db.PlanVersions.Add(version);
        db.Subscriptions.Add(Subscription.CreateMonthly(owner, version, now.AddHours(-1), 0, now, null, true)); await db.SaveChangesAsync();
    }
}
