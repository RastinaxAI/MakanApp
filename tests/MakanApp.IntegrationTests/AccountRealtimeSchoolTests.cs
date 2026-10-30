using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountRealtimeLessonDraftIsStaffOnlyAndPublicationReachesOnlyCurrentlyEnrolledSameOrganization()
    {
        var setup = await CreateTeacherStudentSetupAsync();
        using var teacher = setup.TeacherClient; using var student = setup.StudentClient; using var outside = factory.CreateClient();
        var stranger = await CreateMessagingUserAsync(outside);
        await using var dbScope = factory.Services.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var assignment = await db.TeacherAssignments.AsNoTracking().SingleAsync(t => t.Id == setup.TeacherAssignmentId);
        var studentMembership = await db.Memberships.AsNoTracking().SingleAsync(m => m.UserId == setup.Student.UserId && m.OrganizationId == assignment.OrganizationId);
        await SelectOrganizationAsync(student, studentMembership.Id, OrganizationRole.Student);
        var foreignOrg = await factory.CreateOrganizationAsync(NewOrganizationName());
        var foreignMembership = await factory.CreateMembershipAsync(stranger.UserId, foreignOrg, OrganizationRole.Manager);
        await SelectOrganizationAsync(outside, foreignMembership.MembershipId, OrganizationRole.Manager);
        var studentBoundary = await AccountChangesAsync(student); var outsideBoundary = await AccountChangesAsync(outside);
        var studentEvents = new ConcurrentQueue<RealtimeInvalidation>(); var staffEvents = new ConcurrentQueue<RealtimeInvalidation>();
        await using var studentLive = CreateHubConnection(setup.Student.AccessToken);
        await using var teacherLive = CreateHubConnection(setup.Teacher.AccessToken);
        studentLive.On<RealtimeInvalidation>("ClientInvalidated", studentEvents.Enqueue);
        teacherLive.On<RealtimeInvalidation>("ClientInvalidated", staffEvents.Enqueue);
        await studentLive.StartAsync(); await teacherLive.StartAsync();
        var lesson = await CreateReplayLessonAsync(teacher, assignment.ClassId);
        await FlushReplayOutboxAsync();
        Assert.DoesNotContain(studentEvents, e => e.ResourceId == lesson.Id);
        Assert.Contains(staffEvents, e => e.ResourceId == lesson.Id);
        Assert.DoesNotContain((await DrainAccountAsync(student, studentBoundary.NextCursor)).Values, e => e.Invalidation?.ResourceId == lesson.Id);
        using var published = await teacher.PostAsJsonAsync($"/api/v1/academic/lessons/{lesson.Id}/publish", new PublishClassLessonCommand(lesson.Version), JsonOptions);
        published.EnsureSuccessStatusCode();
        var delivered = new TaskCompletionSource<RealtimeInvalidation>(TaskCreationOptions.RunContinuationsAsynchronously);
        studentLive.On<RealtimeInvalidation>("ClientInvalidated", e => { if (e.ResourceId == lesson.Id) delivered.TrySetResult(e); });
        await FlushReplayOutboxAsync();
        var notification = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("school.lesson", notification.Key);
        Assert.DoesNotContain("محتوای خصوصی پیش‌نویس", JsonSerializer.Serialize(notification));
        var authorized = await student.GetFromJsonAsync<ClassLessonResult>($"/api/v1/academic/lessons/{lesson.Id}", JsonOptions);
        Assert.Equal(lesson.Id, authorized!.Id);
        var replay = await DrainAccountAsync(student, studentBoundary.NextCursor, 1);
        Assert.Single(replay.Values, e => e.Invalidation?.ResourceId == lesson.Id);
        Assert.DoesNotContain((await DrainAccountAsync(outside, outsideBoundary.NextCursor)).Values, e => e.Invalidation?.ResourceId == lesson.Id);
        await SelectPersonalAsync(student);
        var personalBoundary = await AccountChangesAsync(student);
        Assert.DoesNotContain((await DrainAccountAsync(student, personalBoundary.NextCursor)).Values, e => e.Invalidation?.Key.StartsWith("school.") == true);
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("enrollment")]
    [InlineData("role")]
    [InlineData("membership")]
    [InlineData("session")]
    public async Task AccountRealtimeQueuedSchoolEventRechecksCurrentGrantBeforeDeliveryAndReplay(string revoke)
    {
        var setup = await CreateTeacherStudentSetupAsync();
        using var teacher = setup.TeacherClient; using var student = setup.StudentClient;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var assignment = await db.TeacherAssignments.AsNoTracking().SingleAsync(t => t.Id == setup.TeacherAssignmentId);
        var membership = await db.Memberships.AsNoTracking().SingleAsync(m => m.UserId == setup.Student.UserId && m.OrganizationId == assignment.OrganizationId);
        await SelectOrganizationAsync(student, membership.Id, OrganizationRole.Student);
        var boundary = await AccountChangesAsync(revoke == "assignment" ? teacher : student);
        var lesson = await CreateReplayLessonAsync(teacher, assignment.ClassId);
        using var published = await teacher.PostAsJsonAsync($"/api/v1/academic/lessons/{lesson.Id}/publish", new PublishClassLessonCommand(lesson.Version), JsonOptions);
        published.EnsureSuccessStatusCode();
        var queued = (await db.OutboxMessages.AsNoTracking().Where(x => x.MessageType == RealtimeInvalidationWriter.MessageType).ToArrayAsync())
            .Where(x =>
            {
                using var payload = JsonDocument.Parse(x.Payload);
                return payload.RootElement.TryGetProperty("ResourceId", out var resource) && resource.ToString() == lesson.Id.ToString()
                    && !payload.RootElement.GetProperty("StaffOnly").GetBoolean();
            }).Single();
        var victimId = revoke == "assignment" ? setup.Teacher.UserId : setup.Student.UserId;
        var before = await ReplayRecipientsAsync(queued.Id, queued.Payload);
        var victimSession = await db.UserSessions.AsNoTracking().Where(s => s.UserId == victimId).Select(s => s.Id).SingleAsync();
        Assert.Contains(before, s => s.SessionId == victimSession);
        if (revoke == "assignment") await factory.EndTeacherAssignmentAsync(assignment.Id);
        if (revoke == "enrollment")
        {
            var enrollment = await db.Enrollments.AsNoTracking().SingleAsync(e => e.ClassId == assignment.ClassId);
            await factory.EndEnrollmentAsync(enrollment.Id);
        }
        if (revoke == "role")
        {
            var roleId = await db.RoleAssignments.Where(r => r.MembershipId == membership.Id && r.Role == OrganizationRole.Student).Select(r => r.Id).SingleAsync();
            await factory.EndRoleAssignmentAsync(roleId);
        }
        if (revoke == "membership") await factory.EndMembershipAsync(membership.Id);
        if (revoke == "session") await factory.RevokeSessionByTokenAsync(setup.Student.AccessToken);
        Assert.DoesNotContain(await ReplayRecipientsAsync(queued.Id, queued.Payload), s => s.SessionId == victimSession);
        using var response = await (revoke == "assignment" ? teacher : student).GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(boundary.NextCursor));
        if (revoke == "session") Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        else if (revoke is "role" or "membership") Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        else
        {
            response.EnsureSuccessStatusCode();
            var result = (await response.Content.ReadFromJsonAsync<AccountChangePageResult>(JsonOptions))!;
            Assert.DoesNotContain(result.Changes, x => x.Invalidation?.ResourceId == lesson.Id);
        }
    }

    private static async Task<ClassLessonResult> CreateReplayLessonAsync(HttpClient teacher, Guid classId)
    {
        using var created = await teacher.PostAsJsonAsync($"/api/v1/academic/classes/{classId}/lessons",
            new CreateClassLessonCommand(Guid.NewGuid(), "درس آزمون Realtime", "محتوای خصوصی پیش‌نویس", 0, []), JsonOptions);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<ClassLessonResult>(JsonOptions))!;
    }
    private async Task FlushReplayOutboxAsync()
    {
        for (var i = 0; i < 100; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().DispatchBatchAsync(default) == 0) return;
        }
        throw new InvalidOperationException("Outbox آزمون باید در budget مشخص تخلیه شود.");
    }
    private async Task<List<(Guid SessionId, RealtimeInvalidation Notification)>> ReplayRecipientsAsync(Guid id, string payload)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = new List<(Guid, RealtimeInvalidation)>();
        await foreach (var recipient in scope.ServiceProvider.GetRequiredService<IRealtimeInvalidationAudience>().ResolveAsync(id, payload, default)) result.Add(recipient);
        return result;
    }
}
