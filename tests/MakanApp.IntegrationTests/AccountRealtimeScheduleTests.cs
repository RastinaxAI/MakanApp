using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountRealtimeSessionCreateUpdateCancelInvalidatesAuthorizedStudentSchedule()
    {
        var setup = await CreateTeacherStudentSetupAsync();
        using var teacher = setup.TeacherClient; using var student = setup.StudentClient;
        Guid classId; Guid membershipId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var assignment = await db.TeacherAssignments.AsNoTracking().SingleAsync(t => t.Id == setup.TeacherAssignmentId);
            classId = assignment.ClassId;
            membershipId = await db.Memberships.Where(m => m.UserId == setup.Student.UserId && m.OrganizationId == assignment.OrganizationId).Select(m => m.Id).SingleAsync();
        }
        await SelectOrganizationAsync(student, membershipId, OrganizationRole.Student);
        var before = await AccountChangesAsync(student);
        var start = DateTime.UtcNow.AddDays(1);
        using var created = await teacher.PostAsJsonAsync($"/api/v1/academic/classes/{classId}/sessions",
            new CreateSessionCommand("جلسهٔ Realtime", start, start.AddHours(1), "Asia/Tehran", null), JsonOptions);
        created.EnsureSuccessStatusCode();
        var session = (await created.Content.ReadFromJsonAsync<SessionResult>(JsonOptions))!;
        using var updated = await teacher.PatchAsJsonAsync($"/api/v1/academic/sessions/{session.Id}",
            new UpdateSessionCommand("جلسهٔ جابه‌جاشده", start.AddHours(1), start.AddHours(2), "Asia/Tehran", null, session.RowVersion), JsonOptions);
        updated.EnsureSuccessStatusCode();
        var current = (await updated.Content.ReadFromJsonAsync<SessionResult>(JsonOptions))!;
        using var cancelled = await teacher.PostAsJsonAsync($"/api/v1/academic/sessions/{session.Id}/cancel", new SessionVersionCommand(current.RowVersion), JsonOptions);
        cancelled.EnsureSuccessStatusCode();
        var changes = await DrainAccountAsync(student, before.NextCursor, 1);
        Assert.Equal(3, changes.Values.Count(x => x.Invalidation?.Key == "school.schedule" && x.Invalidation.ResourceId == session.Id));
        var authoritative = (await student.GetFromJsonAsync<SessionResult>($"/api/v1/academic/sessions/{session.Id}", JsonOptions))!;
        Assert.Equal(MakanApp.Domain.Academic.SessionStatus.Cancelled, authoritative.Status);
    }
}
