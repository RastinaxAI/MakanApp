using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class DashboardEndpointsTests
{
    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    public async Task LearnerDashboardKeepsReleasedExamScoreOnItsPublishedVersion(
        OrganizationRole role)
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var organizationId = await factory.CreateOrganizationAsync(NewName("Versioned dashboard"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        Guid enrollmentId;
        Guid membershipId;
        if (role == OrganizationRole.Student)
        {
            var student = await CreateStudentAsync(client, organizationId, academic.ClassId);
            enrollmentId = student.EnrollmentId;
            membershipId = student.MembershipId;
        }
        else
        {
            var user = await CreateAuthenticatedUserAsync(client);
            var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, role);
            var child = await CreateChildAsync(user.User.Id, organizationId, academic.ClassId);
            enrollmentId = child.EnrollmentId;
            membershipId = membership.MembershipId;
            UseBearerToken(client, user.AccessToken);
            await SelectWorkspaceAsync(client, membershipId, role);
            await SelectChildAsync(client, child.OrganizationPersonId);
        }

        var released = await factory.CreateDashboardExamAsync(
            organizationId, academic.ClassId, enrollmentId, membershipId,
            nowUtc.AddHours(-1), nowUtc.AddHours(2), "Historical released exam",
            DashboardExamSeedState.Released);
        Guid currentVersionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var exam = await database.Exams.SingleAsync(item => item.Id == released.ExamId);
            var previous = await database.ExamVersions.SingleAsync(item => item.Id == released.ExamVersionId);
            var current = previous.CreateNextDraft(exam.StartNextDraft(1), membershipId, nowUtc);
            current.UpdateDraft("Current exam", null, nowUtc, nowUtc.AddHours(3), 30, 2,
                10m, ExamRandomizationPolicy.None, nowUtc);
            var question = QuestionVersion.CreateDraft(organizationId, current.Id, 1,
                ExamQuestionType.Descriptive, "Private new version question", 10m, null, nowUtc);
            current.Publish([question], nowUtc);
            exam.PublishCurrentVersion(current.VersionNumber);
            database.ExamVersions.Add(current);
            database.QuestionVersions.Add(question);
            await database.SaveChangesAsync();
            currentVersionId = current.Id;
        }

        using var response = await client.GetAsync($"/api/v1/dashboards/{role.ToString().ToLowerInvariant()}");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        var exams = role == OrganizationRole.Student
            ? JsonSerializer.Deserialize<StudentDashboardDto>(raw, JsonOptions)!.UpcomingExams
            : JsonSerializer.Deserialize<ParentDashboardDto>(raw, JsonOptions)!.Exams;
        var historical = Assert.Single(exams, item => item.ExamVersionId == released.ExamVersionId);
        Assert.Equal(18m, historical.ReleasedScore);
        Assert.Equal(20m, historical.MaximumScore);
        Assert.Equal("Historical released exam", historical.Title);
        var upcoming = Assert.Single(exams, item => item.ExamVersionId == currentVersionId);
        Assert.Null(upcoming.ReleasedScore);
        Assert.Null(upcoming.MaximumScore);
        Assert.Null(upcoming.ResultReleaseStatus);
        Assert.Null(upcoming.LatestAttemptStatus);
        Assert.DoesNotContain("Private new version question", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParentAttentionCountExcludesClosedAssignments()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Parent actionable"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, OrganizationRole.Parent);
        var child = await CreateChildAsync(user.User.Id, organizationId, academic.ClassId);
        var closed = await factory.CreateDashboardAssignmentAsync(
            organizationId, academic.ClassId, child.EnrollmentId, membership.MembershipId,
            nowUtc.AddHours(1), "Closed child assignment");
        await factory.CloseDashboardAssignmentAsync(closed.AssignmentId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, child.OrganizationPersonId);

        var dashboard = await client.GetFromJsonAsync<ParentDashboardDto>("/api/v1/dashboards/parent", JsonOptions);

        Assert.NotNull(dashboard);
        Assert.Equal(0, dashboard.AttentionItemCount);
        Assert.Equal(closed.AssignmentId, Assert.Single(dashboard.Assignments).AssignmentId);
    }

    [Theory]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task OperationalDashboardCountsObjectiveGradingAndReleaseBacklog(
        OrganizationRole role)
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Objective queue"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, role);
        if (role == OrganizationRole.Teacher)
            _ = await factory.CreateTeacherAssignmentAsync(organizationId, academic.ClassId, membership.MembershipId);
        var learnerId = (await factory.CreateOrganizationPersonsAsync(organizationId, 1))[0];
        var enrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learnerId);
        foreach (var state in new[] { DashboardExamSeedState.AwaitingGrading, DashboardExamSeedState.ReadyForRelease, DashboardExamSeedState.Released })
        {
            _ = await factory.CreateDashboardExamAsync(organizationId, academic.ClassId, enrollmentId,
                membership.MembershipId, nowUtc.AddMinutes(-5), nowUtc.AddHours(2),
                "Objective operational exam", state, ExamQuestionType.ObjectiveSingleChoice);
        }
        _ = await factory.CreateDashboardExamAsync(organizationId, academic.ClassId, enrollmentId,
            membership.MembershipId, nowUtc.AddMinutes(-5), nowUtc.AddHours(2),
            "Manual operational exam", DashboardExamSeedState.DraftGrade);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, role);

        using var response = await client.GetAsync($"/api/v1/dashboards/{role.ToString().ToLowerInvariant()}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var queue = document.RootElement.GetProperty("examGradingQueue");

        Assert.Equal(1, queue.GetProperty("awaitingGradingCount").GetInt32());
        Assert.Equal(1, queue.GetProperty("manualReviewCount").GetInt32());
        Assert.Equal(1, queue.GetProperty("readyForReleaseCount").GetInt32());
        Assert.Equal(3, queue.GetProperty("totalPendingCount").GetInt32());
    }

    [Theory]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task AttendanceBacklogDoesNotLetEndedEnrollmentMaskMissingOfficialRecord(
        OrganizationRole role)
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Attendance backlog"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, role);
        if (role == OrganizationRole.Teacher)
            _ = await factory.CreateTeacherAssignmentAsync(organizationId, academic.ClassId, membership.MembershipId);
        var learners = await factory.CreateOrganizationPersonsAsync(organizationId, 2);
        _ = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learners[0]);
        var endedEnrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learners[1]);
        var sessionId = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId,
            nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Completed);
        _ = await factory.CreateAttendanceAsync(organizationId, academic.ClassId, sessionId,
            endedEnrollmentId, membership.MembershipId, AttendanceStatus.Present);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var enrollment = await database.Enrollments.SingleAsync(item => item.Id == endedEnrollmentId);
            enrollment.Withdraw(nowUtc);
            await database.SaveChangesAsync();
        }
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, role);

        using var response = await client.GetAsync($"/api/v1/dashboards/{role.ToString().ToLowerInvariant()}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var pending = Assert.Single(document.RootElement.GetProperty("attendancePending").EnumerateArray());

        Assert.Equal(sessionId, pending.GetProperty("sessionId").GetGuid());
        Assert.Equal(1, pending.GetProperty("expectedEnrollmentCount").GetInt32());
        Assert.Equal(0, pending.GetProperty("recordedCount").GetInt32());
        Assert.Equal(1, pending.GetProperty("missingCount").GetInt32());
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task DashboardIgnoresClientScopeAndCannotSelectAnotherUsersWorkspace(
        OrganizationRole role)
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        using var foreignClient = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var foreignUser = await CreateAuthenticatedUserAsync(foreignClient);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Trusted scope"));
        var foreignOrganizationId = await factory.CreateOrganizationAsync(NewName("Untrusted scope"));
        var foreignClass = await factory.CreateAcademicClassAsync(foreignOrganizationId);
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, role);
        var foreignMembership = await factory.CreateMembershipAsync(foreignUser.User.Id, foreignOrganizationId, role);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, role);
        using var selection = await client.PostAsJsonAsync("/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, foreignMembership.MembershipId, role), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, selection.StatusCode);
        Assert.Equal(OrganizationErrorCodes.WorkspaceNotFound, await ReadProblemCodeAsync(selection));

        using var response = await client.GetAsync(
            $"/api/v1/dashboards/{role.ToString().ToLowerInvariant()}?organizationId={foreignOrganizationId}&learnerId={Guid.NewGuid()}&classId={foreignClass.ClassId}&role=Manager");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);

        Assert.Equal(organizationId, document.RootElement.GetProperty("organizationId").GetGuid());
        Assert.Equal(role.ToString(), document.RootElement.GetProperty("audience").GetString());
        Assert.Equal(nowUtc, document.RootElement.GetProperty("dataAsOfUtc").GetDateTime());
        Assert.DoesNotContain(foreignOrganizationId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(foreignClass.ClassId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParentWithoutEnrollmentHasEmptyDataAndEndedRelationBlocksNextRequest()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Unenrolled child"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, OrganizationRole.Parent);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var childId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        var relationId = await factory.CreateGuardianRelationAsync(user.User.Id, organizationId, childId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, childId);

        var dashboard = await client.GetFromJsonAsync<ParentDashboardDto>("/api/v1/dashboards/parent", JsonOptions);

        Assert.Equal(childId, dashboard?.SelectedLearner?.LearnerOrganizationPersonId);
        Assert.Equal(nowUtc, dashboard!.DataAsOfUtc);
        Assert.Empty(dashboard.UpcomingSessions);
        Assert.Empty(dashboard.Assignments);
        Assert.Empty(dashboard.ReleasedAssignmentResults);
        Assert.Empty(dashboard.Exams);
        Assert.Equal(0, dashboard.AttentionItemCount);
        Assert.Equal(new DashboardAttendanceSummary(0, 0, 0, 0, 0, 0, 0), dashboard.AttendanceSummary);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var relation = await database.GuardianRelations.SingleAsync(item => item.Id == relationId);
            relation.End(nowUtc);
            await database.SaveChangesAsync();
        }

        using var blocked = await client.GetAsync("/api/v1/dashboards/parent");

        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotAllowed, await ReadProblemCodeAsync(blocked));
    }

    [Fact]
    public async Task StudentAttendanceSummaryAggregatesOnlyOfficialStatusesAndKeepsMissingSeparate()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var organizationId = await factory.CreateOrganizationAsync(NewName("Official attendance"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var student = await CreateStudentAsync(client, organizationId, academic.ClassId);
        foreach (var status in new[] { AttendanceStatus.Present, AttendanceStatus.Absent, AttendanceStatus.Late, AttendanceStatus.Excused })
        {
            var sessionId = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId,
                nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Completed);
            _ = await factory.CreateAttendanceAsync(organizationId, academic.ClassId, sessionId,
                student.EnrollmentId, student.MembershipId, status);
        }
        _ = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId,
            nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Completed);
        _ = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId,
            nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Cancelled);

        var dashboard = await client.GetFromJsonAsync<StudentDashboardDto>("/api/v1/dashboards/student", JsonOptions);

        Assert.Equal(new DashboardAttendanceSummary(5, 4, 1, 1, 1, 1, 1), dashboard?.AttendanceSummary);
    }
}
