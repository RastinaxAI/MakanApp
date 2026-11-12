using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task ProductV6DisabledAcademicsConcealForeignResourcesAndBlockExistingPersonalEngine()
    {
        var s = await PersonalAcademicAsync();
        var assignment = await PersonalAssignmentAsync(s);
        var exam = await PersonalExamAsync(s);
        var outsider = await ActorAsync();
        var flags = factory.Services.GetRequiredService<PersonalClassAcademicFeatures>();
        flags.AssignmentsEnabled = false; flags.ExamsEnabled = false;
        var classroom = await GetAsync<PersonalClassDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}");
        Assert.False(classroom.Capabilities.CanAuthorAssignments);
        Assert.False(classroom.Capabilities.CanAuthorExams);
        Assert.False(classroom.Capabilities.CanGrade);
        foreach (var path in new[] {
            $"/api/v1/academic/personal-classes/{s.Class.Id}/assignments",
            $"/api/v1/academic/personal-classes/{s.Class.Id}/exams",
            $"/api/v1/academic/assignments/{assignment.Id}",
            $"/api/v1/academic/exams/{exam.Id}/editor" })
        {
            using var denied = await s.Manager.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(CommerceErrorCodes.FeatureNotEntitled, await ProblemAsync(denied));
            using var hidden = await outsider.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        using var submit = await s.Learner.Client.PostAsJsonAsync($"/api/v1/academic/assignments/{assignment.Id}/attempts", (object?)null, Json);
        Assert.Equal(HttpStatusCode.Forbidden, submit.StatusCode);
        using var start = await s.Learner.Client.PostAsJsonAsync($"/api/v1/academic/exams/{exam.Id}/attempts/start", new StartExamCommand(Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
        await WithDb(async db =>
        {
            Assert.True(await db.PersonalClassAcademicGrants.AnyAsync(g => g.Id == s.Grant.Id && g.RevokedAtUtc == null));
            Assert.False(await db.SubmissionAttempts.AnyAsync(a => a.AssignmentId == assignment.Id));
            Assert.False(await db.ExamAttempts.AnyAsync(a => a.ExamId == exam.Id));
        });
    }

    [Fact]
    public async Task ProductV6DisabledPersonalAcademicsDoNotDisableSchoolAssignmentsOrExams()
    {
        var manager = await ActorAsync();
        var organization = await factory.CreateOrganizationAsync("مدرسه آزمون Gate");
        var member = await factory.CreateMembershipAsync(manager.UserId, organization, OrganizationRole.Manager);
        await SelectAsync(manager, member.MembershipId, OrganizationRole.Manager);
        var academic = await factory.CreateAcademicClassAsync(organization);
        var flags = factory.Services.GetRequiredService<PersonalClassAcademicFeatures>();
        flags.AssignmentsEnabled = false; flags.ExamsEnabled = false;
        using var assignment = await manager.Client.PostAsJsonAsync($"/api/v1/academic/classes/{academic.ClassId}/assignments", AssignmentDraft(), Json);
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        using var exam = await manager.Client.PostAsJsonAsync($"/api/v1/academic/classes/{academic.ClassId}/exams",
            new CreateExamDraftCommand("آزمون مدرسه", null, now, now.AddDays(1), 10, 1, 20, ExamRandomizationPolicy.None), Json);
        Assert.Equal(HttpStatusCode.Created, exam.StatusCode);
    }
}
