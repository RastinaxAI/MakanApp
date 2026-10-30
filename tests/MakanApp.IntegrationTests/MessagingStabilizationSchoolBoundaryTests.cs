using System.Net;
using System.Net.Http.Json;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task StabilizationAssignedTeacherCannotCreateOfficialClassAndStudentCannotReadFullRoster()
    {
        var setup = await CreateTeacherStudentSetupAsync();
        using var teacher = setup.TeacherClient;
        using var student = setup.StudentClient;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var assignment = await db.TeacherAssignments.AsNoTracking().SingleAsync(a => a.Id == setup.TeacherAssignmentId);
        var cls = await db.Classes.AsNoTracking().SingleAsync(c => c.Id == assignment.ClassId);
        var before = await db.Classes.CountAsync(c => c.OrganizationId == cls.OrganizationId);
        using var forbidden = await teacher.PostAsJsonAsync("/api/v1/academic/classes", new
        {
            academicPeriodId = cls.AcademicPeriodId,
            courseId = cls.CourseId,
            title = "ساخت رسمی نامجاز توسط استاد منتسب",
            capacity = 5,
            activateImmediately = true
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(before, await db.Classes.CountAsync(c => c.OrganizationId == cls.OrganizationId));
        using var allowed = await teacher.GetAsync($"/api/v1/academic/classes/{cls.Id}/learners");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var membership = await db.Memberships.AsNoTracking().SingleAsync(m => m.OrganizationId == cls.OrganizationId && m.UserId == setup.Student.UserId);
        await SelectOrganizationAsync(student, membership.Id, OrganizationRole.Student);
        using var learners = await student.GetAsync($"/api/v1/academic/classes/{cls.Id}/learners");
        using var teachers = await student.GetAsync($"/api/v1/academic/classes/{cls.Id}/teachers");
        Assert.Equal(HttpStatusCode.NotFound, learners.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, teachers.StatusCode);
    }
}
