using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Knowledge;
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
    public async Task EducationalSourcesBindingPendingFileDefeatsStaleUploaderDeletionAtSqlRowVersionBoundary()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 5);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var file = MakanApp.Domain.Storage.FileAsset.CreatePending(Guid.NewGuid(), null, user.UserId, "pending.pdf", Guid.NewGuid().ToString("N"), "application/pdf", now, now.AddHours(1));
        db.FileAssets.Add(file); await db.SaveChangesAsync();
        var source = await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, file.Id));
        file.MarkDeleted(now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.False(source.SourceReady);
    }
    [Theory]
    [InlineData(SourceSafetyStatus.ReviewRequired)]
    [InlineData(SourceSafetyStatus.Rejected)]
    public async Task EducationalSourcesSafetyHoldCannotPublishDownloadOrBeLaunderedIntoAnotherSource(SourceSafetyStatus safety)
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10);
        var file = await SourceUpload(client, true); var source = await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, file.Id));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.EducationalSourceVersions.SingleAsync(v => v.SourceId == source.SourceId)).RecordFileValidation(safety); await db.SaveChangesAsync();
        }
        using var current = await client.GetAsync($"/api/v1/sources/{source.SourceId}/processing");
        var held = (await current.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        Assert.True(held.FileReady); Assert.False(held.SourceReady); Assert.False(held.AiReady); Assert.Equal(safety, held.SafetyStatus);
        using var publish = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/publish", new SourceMutationCommand(held.Version, "آزمون Safety"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
        using var raw = await client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, raw.StatusCode);
        using var copy = await client.PostAsJsonAsync("/api/v1/sources", SourceCommand(EducationalSourceScope.Personal, file.Id), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, copy.StatusCode);
    }
    [Fact]
    public async Task EducationalSourcesFailedProcessingIsSafeExplicitAndDoesNotInventAiReadinessOrDeleteFile()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10);
        var source = await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.EducationalSourceVersions.SingleAsync(v => v.SourceId == source.SourceId)).RecordProcessing(SourceProcessingStatus.Failed, "SOURCE_PROCESSING_FAILED");
            await db.SaveChangesAsync();
        }
        using var current = await client.GetAsync($"/api/v1/sources/{source.SourceId}/processing");
        var failed = (await current.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
        Assert.True(failed.SourceReady); Assert.False(failed.AiReady); Assert.Equal(SourceProcessingStatus.Failed, failed.ProcessingStatus);
        Assert.Equal("SOURCE_PROCESSING_FAILED", failed.ProcessingFailureCode);
        using var content = await client.GetAsync($"/api/v1/sources/{source.SourceId}/content"); Assert.Equal(HttpStatusCode.OK, content.StatusCode);
    }
    [Fact]
    public async Task EducationalSourcesParentGetsNoClassAccessAndPersonalSurvivesEndedRoleWithoutOwnershipTransfer()
    {
        using var s = await CreateOfficialScenarioAsync(false); await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 5);
        await SourceContract(SubscriptionOwner.Individual(s.TeacherId), 5);
        var personal = await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(s.Teacher, true)).Id));
        var classroom = await SourcePublish(s.Teacher, await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId)));
        Guid studentMembership;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            studentMembership = await db.Memberships.Where(m => m.UserId == s.StudentId && m.OrganizationId == s.OrganizationId).Select(m => m.Id).SingleAsync();
            db.RoleAssignments.Add(RoleAssignment.CreateActive(studentMembership, OrganizationRole.Parent, factory.Clock.GetUtcNow().UtcDateTime)); await db.SaveChangesAsync();
        }
        await SelectOrganizationAsync(s.Student, studentMembership, OrganizationRole.Parent);
        using (var denied = await s.Student.GetAsync($"/api/v1/sources/{classroom.SourceId}")) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var member = await db.Memberships.SingleAsync(m => m.UserId == s.TeacherId && m.OrganizationId == s.OrganizationId);
            member.End(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync();
        }
        using var own = await s.Teacher.GetAsync($"/api/v1/sources/{personal.SourceId}"); Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var revoked = await s.Teacher.GetAsync($"/api/v1/sources/{classroom.SourceId}"); Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }
    [Fact]
    public async Task EducationalSourcesKnownOperationWithDifferentPayloadConflictsWithoutSecondConsumption()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 2);
        var command = SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id);
        var source = await SourceCreate(client, command);
        using var changed = await client.PostAsJsonAsync("/api/v1/sources", command with { Title = "payload متفاوت" }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode); Assert.Contains(SourceErrorCodes.IdempotencyConflict, await changed.Content.ReadAsStringAsync());
        using var stale = await client.PatchAsJsonAsync($"/api/v1/sources/{source.SourceId}/draft", new UpdateSourceDraftCommand("به‌روزرسانی", null, source.Version, "ویرایش"), JsonOptions);
        stale.EnsureSuccessStatusCode();
        using var publishOld = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/publish", new SourceMutationCommand(source.Version, "نسخهٔ کهنه"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, publishOld.StatusCode);
    }
}
