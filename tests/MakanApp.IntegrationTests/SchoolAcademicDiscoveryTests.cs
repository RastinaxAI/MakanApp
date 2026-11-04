using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task SchoolContentActivityReportsOnlyPublicationMetadataWithinAuthorizedOrganization()
    {
        using var s = await CreateOfficialScenarioAsync();
        var marker = "PRIVATE-EDUCATIONAL-CONTENT-" + Guid.NewGuid();
        var eventId = await PublishOfficialSourceAsync(s, AcademicActivityKind.Assignment, marker);
        Guid legacyLessonId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = DateTime.UtcNow;
            var lesson = ClassLesson.Create(s.OrganizationId, s.ClassId, s.TeacherId, Guid.NewGuid(), new string('D', 64),
                "محتوای تاریخی منتشرشده", marker, 0, [], now); lesson.Publish(now);
            db.ClassLessons.Add(lesson); await db.SaveChangesAsync(); legacyLessonId = lesson.Id;
        }
        var route = "/api/v1/organization/teacher-content-activities";
        var page = (await s.Manager.GetFromJsonAsync<SchoolPage<TeacherContentActivityDto>>(
            route + $"?teacherUserId={s.TeacherId}&classId={s.ClassId}&kind=Assignment&pageSize=1", JsonOptions))!;
        var row = Assert.Single(page.Items); Assert.Equal(eventId, row.EventId); Assert.Equal(s.TeacherId, row.TeacherUserId);
        Assert.Equal(1, page.TotalCount); Assert.NotEmpty(row.TeacherDisplayName); Assert.NotEmpty(row.ClassTitle);
        Assert.DoesNotContain(marker, await s.Manager.GetStringAsync(route));
        var historical = Assert.Single((await s.Manager.GetFromJsonAsync<SchoolPage<TeacherContentActivityDto>>(route + "?kind=Lesson", JsonOptions))!.Items);
        Assert.Equal(legacyLessonId, historical.SourceVersionId); Assert.Null(historical.EventId);
        Assert.Equal(s.TeacherId, historical.TeacherUserId);
        Assert.Empty((await s.Manager.GetFromJsonAsync<SchoolPage<TeacherContentActivityDto>>(route + "?kind=Exam", JsonOptions))!.Items);
        using var foreign = await CreateOfficialScenarioAsync();
        Assert.Empty((await foreign.Manager.GetFromJsonAsync<SchoolPage<TeacherContentActivityDto>>(route + $"?classId={s.ClassId}", JsonOptions))!.Items);
        foreach (var client in new[] { s.Teacher, s.Student })
        { using var denied = await client.GetAsync(route); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
        foreach (var query in new[] { "pageSize=101", "kind=999", "kind=Session", "fromUtc=2026-11-04T00:00:00Z&toUtc=2026-11-03T00:00:00Z" })
        { using var invalid = await s.Manager.GetAsync(route + "?" + query); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
    }

    [Fact]
    public async Task OrganizationExamDiscoveryFindsDraftsWithFiltersAndNeverExposesEditorKeys()
    {
        using var s = await CreateOfficialScenarioAsync();
        var draft = await OfficialPostAsync(s.Teacher, $"/api/v1/academic/classes/{s.ClassId}/exams", new
        { title = "Draft discoverable", description = "safe summary", availableFromUtc = DateTime.UtcNow.AddMinutes(-5),
            availableUntilUtc = DateTime.UtcNow.AddHours(2), durationMinutes = 30, maxAttempts = 2, maxScore = 10m, randomizationPolicy = "None" });
        var id = Guid.Parse(draft["id"]!.GetValue<string>());
        var route = "/api/v1/academic/exams/discovery";
        var page = (await s.Teacher.GetFromJsonAsync<AcademicPage<StudentExamSummary>>(
            route + $"?classId={s.ClassId}&status=Draft&search=discoverable&pageSize=1", JsonOptions))!;
        Assert.Equal(id, Assert.Single(page.Items).Id); Assert.Equal(1, page.TotalCount);
        Assert.Equal(ExamVersionStatus.Draft, page.Items.Single().PublicationState);
        Assert.Contains((await s.Teacher.GetFromJsonAsync<StudentExamSummary[]>("/api/v1/academic/exams", JsonOptions))!, e => e.Id == id);
        var json = await s.Teacher.GetStringAsync(route); Assert.DoesNotContain("isCorrect", json); Assert.DoesNotContain("questions", json);
        Assert.Empty((await s.Teacher.GetFromJsonAsync<AcademicPage<StudentExamSummary>>(route + "?status=Published", JsonOptions))!.Items);
        using var hidden = await s.Student.GetAsync(route); Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        using var foreign = await CreateOfficialScenarioAsync();
        Assert.Empty((await foreign.Teacher.GetFromJsonAsync<AcademicPage<StudentExamSummary>>(route + $"?classId={s.ClassId}", JsonOptions))!.Items);
        foreach (var query in new[] { "status=999", "pageSize=101", "page=0" })
        { using var invalid = await s.Teacher.GetAsync(route + "?" + query); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
        await factory.EndTeacherAssignmentAsync(s.TeacherAssignmentId);
        Assert.Empty((await s.Teacher.GetFromJsonAsync<AcademicPage<StudentExamSummary>>(route, JsonOptions))!.Items);
    }
}
