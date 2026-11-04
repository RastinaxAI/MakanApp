using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task SchoolClassCreationReplaysSameIntentAndRejectsPayloadReuseAcrossConcurrentRequests()
    {
        var org = await factory.CreateOrganizationAsync("کلاس idempotent " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var client = manager.Client;
        var source = await factory.CreateAcademicClassAsync(org);
        var command = new CreateClassCommand(source.PeriodId, source.CourseId, "کلاس یک قصد", 10, true, Guid.NewGuid());
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/academic/classes", command, PolicyJson),
            client.PostAsJsonAsync("/api/v1/academic/classes", command, PolicyJson));
        var rows = new List<ClassResult>();
        foreach (var r in responses) rows.Add(await SchoolReadAsync<ClassResult>(r, HttpStatusCode.Created));
        Assert.Equal(rows[0].Id, rows[1].Id); Assert.Equal(rows[0].Version, rows[1].Version);
        var replay = await SchoolReadAsync<ClassResult>(await client.PostAsJsonAsync("/api/v1/academic/classes", command, PolicyJson), HttpStatusCode.Created);
        Assert.Equal(rows[0].Id, replay.Id); Assert.Equal(8, Convert.FromBase64String(replay.Version).Length);
        using var mismatch = await client.PostAsJsonAsync("/api/v1/academic/classes", command with { Capacity = 20 }, PolicyJson);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        using var missingIntent = await client.PostAsJsonAsync("/api/v1/academic/classes", new
        { command.AcademicPeriodId, command.CourseId, command.Title, command.Capacity, command.ActivateImmediately }, PolicyJson);
        Assert.Equal(HttpStatusCode.BadRequest, missingIntent.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.Classes.CountAsync(c => c.OrganizationId == org && c.ClientOperationId == command.ClientOperationId));
        Assert.Equal(2, await db.OfficialSpaceProvisions.CountAsync(p => p.ClassId == replay.Id));
    }

    [Fact]
    public async Task SchoolClassSettingsUseCurrentVersionAndCapacitySharesEnrollmentLock()
    {
        var org = await factory.CreateOrganizationAsync("کلاس settings " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var client = manager.Client;
        var source = await factory.CreateAcademicClassAsync(org);
        var learner = await SchoolActorAsync(org, OrganizationRole.Student); using var lc = learner.Client;
        _ = await SchoolReadAsync<EnrollmentResult>(await client.PostAsJsonAsync($"/api/v1/academic/classes/{source.ClassId}/enrollments",
            new EnrollLearnerCommand(learner.OrganizationPersonId), PolicyJson), HttpStatusCode.Created);
        var secondLearner = await SchoolActorAsync(org, OrganizationRole.Student); using var secondClient = secondLearner.Client;
        _ = await SchoolReadAsync<EnrollmentResult>(await client.PostAsJsonAsync($"/api/v1/academic/classes/{source.ClassId}/enrollments",
            new EnrollLearnerCommand(secondLearner.OrganizationPersonId), PolicyJson), HttpStatusCode.Created);
        var rows = await SchoolReadAsync<ClassResult[]>(await client.GetAsync("/api/v1/academic/classes"));
        var current = rows.Single(c => c.Id == source.ClassId); var route = $"/api/v1/academic/classes/{current.Id}";
        var command = new UpdateClassSettingsCommand("عنوان تازه", 2, current.Version, "تغییر ظرفیت بدون حذف فراگیر");
        var responses = await Task.WhenAll(client.PatchAsJsonAsync(route, command, PolicyJson), client.PatchAsJsonAsync(route, command with { Title = "رقیب" }, PolicyJson));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.PreconditionFailed);
        var winner = await responses.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<ClassResult>(PolicyJson);
        foreach (var r in responses) r.Dispose(); Assert.NotEqual(current.Version, winner!.Version);
        using var invalid = await client.PatchAsJsonAsync(route, command with { ExpectedVersion = winner.Version, Capacity = 0 }, PolicyJson);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var belowActive = await client.PatchAsJsonAsync(route, command with { ExpectedVersion = winner.Version, Capacity = 1 }, PolicyJson);
        Assert.Equal(HttpStatusCode.Conflict, belowActive.StatusCode);
        using var student = await lc.PatchAsJsonAsync(route, command, PolicyJson); Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
        var foreign = await SchoolActorAsync(await factory.CreateOrganizationAsync("بیگانه " + Guid.NewGuid()), OrganizationRole.Manager);
        using var fc = foreign.Client; using var hidden = await fc.PatchAsJsonAsync(route, command, PolicyJson); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(2, (await SchoolPageAsync<ClassLearnerResult>(client, route + "/learners")).TotalCount);
    }

    [Fact]
    public async Task SchoolRelationshipEndUsesItsOwnVersionAndPreservesAccountAndHistory()
    {
        var org = await factory.CreateOrganizationAsync("پایان رابطه نسخه‌دار " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var client = manager.Client;
        var teacher = await SchoolActorAsync(org, OrganizationRole.Teacher); using var tc = teacher.Client;
        var student = await SchoolActorAsync(org, OrganizationRole.Student); using var sc = student.Client;
        var source = await factory.CreateAcademicClassAsync(org); var route = $"/api/v1/academic/classes/{source.ClassId}";
        var assignment = await SchoolReadAsync<TeacherAssignmentResult>(await client.PostAsJsonAsync(route + "/teachers", new AssignTeacherCommand(teacher.MembershipId), PolicyJson), HttpStatusCode.Created);
        var enrollment = await SchoolReadAsync<EnrollmentResult>(await client.PostAsJsonAsync(route + "/enrollments", new EnrollLearnerCommand(student.OrganizationPersonId), PolicyJson), HttpStatusCode.Created);
        var ended = await SchoolReadAsync<TeacherAssignmentResult>(await client.PostAsJsonAsync(route + $"/teachers/{assignment.Id}/end", new EndTeacherAssignmentCommand(assignment.Version), PolicyJson));
        Assert.NotEqual(assignment.Version, ended.Version);
        using var stale = await client.PostAsJsonAsync(route + $"/teachers/{assignment.Id}/end", new EndTeacherAssignmentCommand(assignment.Version), PolicyJson);
        Assert.True(stale.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed);
        _ = await SchoolReadAsync<EnrollmentResult>(await client.PostAsJsonAsync(route + $"/enrollments/{enrollment.Id}/end",
            new EndEnrollmentCommand(EnrollmentStatus.Withdrawn, enrollment.Version), PolicyJson));
        using var staleEnrollment = await client.PostAsJsonAsync(route + $"/enrollments/{enrollment.Id}/end",
            new EndEnrollmentCommand(EnrollmentStatus.Completed, enrollment.Version), PolicyJson);
        Assert.True(staleEnrollment.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await db.Users.AnyAsync(u => u.Id == teacher.UserId)); Assert.True(await db.Users.AnyAsync(u => u.Id == student.UserId));
        Assert.Equal(TeacherAssignmentStatus.Ended, (await db.TeacherAssignments.SingleAsync(t => t.Id == assignment.Id)).Status);
    }
}
