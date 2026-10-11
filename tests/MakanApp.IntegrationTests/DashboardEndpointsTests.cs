using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class DashboardEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 8_000_000;

    [Fact]
    public async Task StudentDashboardReturnsOnlyOwnReleasedAndOfficialAcademicData()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var organizationId = await factory.CreateOrganizationAsync(NewName("Student Org A"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var student = await CreateStudentAsync(client, organizationId, academic.ClassId);
        var otherLearnerId = (await factory.CreateOrganizationPersonsAsync(organizationId, 1))[0];
        var otherEnrollmentId = await factory.CreateEnrollmentAsync(
            organizationId,
            academic.ClassId,
            otherLearnerId);

        var nearestSessionId = await factory.CreateAcademicSessionAsync(
            organizationId,
            academic.ClassId,
            nowUtc.AddHours(1),
            nowUtc.AddHours(2));
        _ = await factory.CreateAcademicSessionAsync(
            organizationId,
            academic.ClassId,
            nowUtc.AddHours(2),
            nowUtc.AddHours(3));
        _ = await factory.CreateAcademicSessionAsync(
            organizationId,
            academic.ClassId,
            nowUtc.AddMinutes(30),
            nowUtc.AddHours(1),
            SessionStatus.Cancelled);

        var attendanceSessionId = await factory.CreateAcademicSessionAsync(
            organizationId,
            academic.ClassId,
            nowUtc.AddHours(-3),
            nowUtc.AddHours(-2),
            SessionStatus.Completed);
        _ = await factory.CreateAcademicSessionAsync(
            organizationId,
            academic.ClassId,
            nowUtc.AddHours(-2),
            nowUtc.AddHours(-1),
            SessionStatus.Completed);
        _ = await factory.CreateAttendanceAsync(
            organizationId,
            academic.ClassId,
            attendanceSessionId,
            student.EnrollmentId,
            student.MembershipId,
            AttendanceStatus.Present);

        var nearestAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddHours(3),
            "Nearest own assignment");
        _ = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddHours(5),
            "Later own assignment");
        var unrelatedAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            otherEnrollmentId,
            student.MembershipId,
            nowUtc.AddHours(1),
            "Another learner assignment");
        var closedAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddHours(2),
            "Closed assignment");
        await factory.CloseDashboardAssignmentAsync(closedAssignment.AssignmentId);
        var releasedAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddHours(6),
            "Released assignment",
            DashboardAssignmentSeedState.Released);
        var draftEvaluation = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddHours(7),
            "Unreleased assignment",
            DashboardAssignmentSeedState.DraftEvaluation);

        var availableExam = await factory.CreateDashboardExamAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddMinutes(-10),
            nowUtc.AddHours(2),
            "Available exam");
        var unreleasedExam = await factory.CreateDashboardExamAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddMinutes(-10),
            nowUtc.AddHours(3),
            "Exam with draft grade",
            DashboardExamSeedState.DraftGrade);
        var releasedExam = await factory.CreateDashboardExamAsync(
            organizationId,
            academic.ClassId,
            student.EnrollmentId,
            student.MembershipId,
            nowUtc.AddMinutes(-10),
            nowUtc.AddHours(4),
            "Exam with released grade",
            DashboardExamSeedState.Released);

        using var response = await client.GetAsync("/api/v1/dashboards/student");
        var rawJson = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var dashboard = JsonSerializer.Deserialize<StudentDashboardDto>(rawJson, JsonOptions)!;

        Assert.Equal(organizationId, dashboard.OrganizationId);
        Assert.Equal(DashboardAudience.Student, dashboard.Audience);
        Assert.Equal(nowUtc, dashboard.DataAsOfUtc);
        Assert.Equal(nearestSessionId, dashboard.NextSession?.SessionId);
        Assert.Equal(nearestAssignment.AssignmentId, dashboard.NearestAssignmentDeadline?.AssignmentId);
        Assert.DoesNotContain(
            unrelatedAssignment.AssignmentId.ToString(),
            rawJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            dashboard.RecentReleasedFeedback,
            item => item.SubmissionAttemptId == releasedAssignment.SubmissionAttemptId);
        Assert.DoesNotContain(
            dashboard.RecentReleasedFeedback,
            item => item.SubmissionAttemptId == draftEvaluation.SubmissionAttemptId);
        Assert.Contains(dashboard.UpcomingExams, item => item.ExamId == availableExam.ExamId);
        Assert.Null(dashboard.UpcomingExams.Single(item => item.ExamId == unreleasedExam.ExamId).ReleasedScore);
        Assert.Equal(18m, dashboard.UpcomingExams.Single(item => item.ExamId == releasedExam.ExamId).ReleasedScore);
        Assert.Equal(2, dashboard.AttendanceSummary.EligibleSessionCount);
        Assert.Equal(1, dashboard.AttendanceSummary.PresentCount);
        Assert.Equal(0, dashboard.AttendanceSummary.AbsentCount);
        Assert.Equal(1, dashboard.AttendanceSummary.NotRecordedCount);
        Assert.DoesNotContain("Private question content", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CorrectOption", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Teacher private note", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StudentDashboardIsOrganizationScopedAndHasCleanEmptyState()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var organizationA = await factory.CreateOrganizationAsync(NewName("Student Empty A"));
        var classA = await factory.CreateAcademicClassAsync(organizationA);
        var student = await CreateStudentAsync(client, organizationA, classA.ClassId);
        var organizationB = await factory.CreateOrganizationAsync(NewName("Student Foreign B"));
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        var foreignMembership = await factory.CreateMembershipAsync(
            student.UserId,
            organizationB,
            OrganizationRole.Student);
        var foreignLearnerId = (await factory.CreateOrganizationPersonsAsync(organizationB, 1))[0];
        var foreignEnrollmentId = await factory.CreateEnrollmentAsync(
            organizationB,
            classB.ClassId,
            foreignLearnerId);
        var foreignSessionId = await factory.CreateAcademicSessionAsync(
            organizationB,
            classB.ClassId,
            nowUtc.AddHours(1),
            nowUtc.AddHours(2));
        var foreignAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationB,
            classB.ClassId,
            foreignEnrollmentId,
            foreignMembership.MembershipId,
            nowUtc.AddHours(2),
            "Foreign assignment");

        var dashboard = await client.GetFromJsonAsync<StudentDashboardDto>(
            "/api/v1/dashboards/student",
            JsonOptions);
        using var arbitraryLearnerResponse = await client.GetAsync(
            $"/api/v1/dashboards/student/{foreignLearnerId}");

        Assert.NotNull(dashboard);
        Assert.Equal(organizationA, dashboard.OrganizationId);
        Assert.Null(dashboard.NextSession);
        Assert.Null(dashboard.NearestAssignmentDeadline);
        Assert.Empty(dashboard.RecentReleasedFeedback);
        Assert.Empty(dashboard.UpcomingExams);
        Assert.Equal(0, dashboard.PendingAssignmentCount);
        Assert.NotEqual(foreignSessionId, dashboard.NextSession?.SessionId);
        Assert.NotEqual(foreignAssignment.AssignmentId, dashboard.NearestAssignmentDeadline?.AssignmentId);
        Assert.Equal(HttpStatusCode.NotFound, arbitraryLearnerResponse.StatusCode);
    }

    [Fact]
    public async Task DashboardEndpointRejectsWrongActiveRoleAndAnonymousCaller()
    {
        using var anonymousClient = factory.CreateClient();
        using var anonymousResponse = await anonymousClient.GetAsync("/api/v1/dashboards/student");

        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Role Gate"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);
        using var wrongRoleResponse = await client.GetAsync("/api/v1/dashboards/student");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRoleResponse.StatusCode);
        Assert.Equal(
            OrganizationErrorCodes.WorkspaceNotAllowed,
            await ReadProblemCodeAsync(wrongRoleResponse));
    }

    [Fact]
    public async Task ParentDashboardUsesOnlySelectedChildAndReleasedResults()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Parent Org"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var firstChild = await CreateChildAsync(user.User.Id, organizationId, academic.ClassId);
        var secondChild = await CreateChildAsync(user.User.Id, organizationId, academic.ClassId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, firstChild.OrganizationPersonId);

        var firstAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            firstChild.EnrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(2),
            "First child released",
            DashboardAssignmentSeedState.Released);
        var firstDraft = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            firstChild.EnrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(3),
            "First child unreleased",
            DashboardAssignmentSeedState.DraftEvaluation);
        var secondAssignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            secondChild.EnrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(1),
            "Second child assignment",
            DashboardAssignmentSeedState.Released);
        var releasedExam = await factory.CreateDashboardExamAsync(
            organizationId,
            academic.ClassId,
            firstChild.EnrollmentId,
            membership.MembershipId,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(2),
            "Released child exam",
            DashboardExamSeedState.Released);
        var draftExam = await factory.CreateDashboardExamAsync(
            organizationId,
            academic.ClassId,
            firstChild.EnrollmentId,
            membership.MembershipId,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(3),
            "Unreleased child exam",
            DashboardExamSeedState.DraftGrade);

        using var response = await client.GetAsync("/api/v1/dashboards/parent");
        var rawJson = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var dashboard = JsonSerializer.Deserialize<ParentDashboardDto>(rawJson, JsonOptions)!;

        Assert.Equal(nowUtc, dashboard.DataAsOfUtc);
        Assert.Equal(firstChild.OrganizationPersonId, dashboard.SelectedLearner?.LearnerOrganizationPersonId);
        Assert.Contains(
            dashboard.ReleasedAssignmentResults,
            item => item.SubmissionAttemptId == firstAssignment.SubmissionAttemptId);
        Assert.DoesNotContain(
            dashboard.ReleasedAssignmentResults,
            item => item.SubmissionAttemptId == firstDraft.SubmissionAttemptId);
        Assert.DoesNotContain(
            dashboard.ReleasedAssignmentResults,
            item => item.SubmissionAttemptId == secondAssignment.SubmissionAttemptId);
        Assert.Equal(18m, dashboard.Exams.Single(item => item.ExamId == releasedExam.ExamId).ReleasedScore);
        Assert.Null(dashboard.Exams.Single(item => item.ExamId == draftExam.ExamId).ReleasedScore);
        Assert.DoesNotContain("Teacher private note", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Evaluator private exam note", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Message", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Copilot", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("submitUrl", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("startExamUrl", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("finalizeUrl", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParentWithoutSelectedChildGetsSafeEmptyDashboard()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Parent Empty"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);

        var unrelatedPersonId = await factory.CreatePersonWithoutUserAsync();
        var unrelatedLearnerId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            unrelatedPersonId);
        using var unrelatedSelection = await client.PostAsync(
            $"/api/v1/guardian/children/{unrelatedLearnerId}/select",
            null);

        var dashboard = await client.GetFromJsonAsync<ParentDashboardDto>(
            "/api/v1/dashboards/parent",
            JsonOptions);

        Assert.NotNull(dashboard);
        Assert.Equal(HttpStatusCode.NotFound, unrelatedSelection.StatusCode);
        Assert.Equal(nowUtc, dashboard.DataAsOfUtc);
        Assert.Null(dashboard.SelectedLearner);
        Assert.Empty(dashboard.UpcomingSessions);
        Assert.Empty(dashboard.Assignments);
        Assert.Empty(dashboard.ReleasedAssignmentResults);
        Assert.Empty(dashboard.Exams);
        Assert.Equal(0, dashboard.AttentionItemCount);
    }

    [Fact]
    public async Task RevokedSelectedGuardianRelationImmediatelyDeniesDashboard()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Revoked Parent"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var child = await CreateChildAsync(user.User.Id, organizationId, academic.ClassId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, child.OrganizationPersonId);
        await factory.RevokeGuardianRelationAsync(child.RelationId);

        using var response = await client.GetAsync("/api/v1/dashboards/parent");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Theory]
    [InlineData(GuardianRelationStatus.Pending)]
    [InlineData(GuardianRelationStatus.Ended)]
    public async Task InactiveGuardianRelationCannotEstablishDashboardContext(
        GuardianRelationStatus relationStatus)
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Inactive Parent"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var childId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        _ = await factory.CreateGuardianRelationAsync(
            user.User.Id,
            organizationId,
            childId,
            relationStatus);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);

        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{childId}/select",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ParentWithChildrenInTwoOrganizationsRemainsWorkspaceScoped()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationA = await factory.CreateOrganizationAsync(NewName("Parent School A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("Parent School B"));
        var classA = await factory.CreateAcademicClassAsync(organizationA);
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        var membershipA = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationA,
            OrganizationRole.Parent);
        var membershipB = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationB,
            OrganizationRole.Parent);
        var childA = await CreateChildAsync(user.User.Id, organizationA, classA.ClassId);
        var childB = await CreateChildAsync(user.User.Id, organizationB, classB.ClassId);
        UseBearerToken(client, user.AccessToken);

        await SelectWorkspaceAsync(client, membershipA.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, childA.OrganizationPersonId);
        var dashboardA = await client.GetFromJsonAsync<ParentDashboardDto>(
            "/api/v1/dashboards/parent",
            JsonOptions);
        await SelectWorkspaceAsync(client, membershipB.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, childB.OrganizationPersonId);
        var dashboardB = await client.GetFromJsonAsync<ParentDashboardDto>(
            "/api/v1/dashboards/parent",
            JsonOptions);

        Assert.Equal(organizationA, dashboardA?.OrganizationId);
        Assert.Equal(childA.OrganizationPersonId, dashboardA?.SelectedLearner?.LearnerOrganizationPersonId);
        Assert.Equal(organizationB, dashboardB?.OrganizationId);
        Assert.Equal(childB.OrganizationPersonId, dashboardB?.SelectedLearner?.LearnerOrganizationPersonId);
    }

    [Fact]
    public async Task TeacherDashboardIncludesOnlyAssignedWorkInActiveOrganization()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationA = await factory.CreateOrganizationAsync(NewName("Teacher School A"));
        var assignedClass = await factory.CreateAcademicClassAsync(organizationA);
        var unassignedClass = await factory.CreateAcademicClassAsync(organizationA);
        var organizationB = await factory.CreateOrganizationAsync(NewName("Teacher School B"));
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        var membershipA = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationA,
            OrganizationRole.Teacher);
        var membershipB = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationB,
            OrganizationRole.Teacher);
        _ = await factory.CreateTeacherAssignmentAsync(
            organizationA,
            assignedClass.ClassId,
            membershipA.MembershipId);
        _ = await factory.CreateTeacherAssignmentAsync(
            organizationB,
            classB.ClassId,
            membershipB.MembershipId);
        var assignedLearner = (await factory.CreateOrganizationPersonsAsync(organizationA, 1))[0];
        var assignedEnrollment = await factory.CreateEnrollmentAsync(
            organizationA,
            assignedClass.ClassId,
            assignedLearner);
        var unassignedLearner = (await factory.CreateOrganizationPersonsAsync(organizationA, 1))[0];
        var unassignedEnrollment = await factory.CreateEnrollmentAsync(
            organizationA,
            unassignedClass.ClassId,
            unassignedLearner);
        var assignedUpcoming = await factory.CreateAcademicSessionAsync(
            organizationA,
            assignedClass.ClassId,
            nowUtc.AddHours(1),
            nowUtc.AddHours(2));
        var unassignedUpcoming = await factory.CreateAcademicSessionAsync(
            organizationA,
            unassignedClass.ClassId,
            nowUtc.AddMinutes(30),
            nowUtc.AddHours(1));
        var pendingAttendance = await factory.CreateAcademicSessionAsync(
            organizationA,
            assignedClass.ClassId,
            nowUtc.AddHours(-2),
            nowUtc.AddHours(-1),
            SessionStatus.Completed);
        _ = await factory.CreateAcademicSessionAsync(
            organizationA,
            unassignedClass.ClassId,
            nowUtc.AddHours(-2),
            nowUtc.AddHours(-1),
            SessionStatus.Completed);
        _ = await factory.CreateDashboardAssignmentAsync(
            organizationA,
            assignedClass.ClassId,
            assignedEnrollment,
            membershipA.MembershipId,
            nowUtc.AddHours(3),
            "Assigned grading item",
            DashboardAssignmentSeedState.Submitted);
        _ = await factory.CreateDashboardAssignmentAsync(
            organizationA,
            unassignedClass.ClassId,
            unassignedEnrollment,
            membershipA.MembershipId,
            nowUtc.AddHours(3),
            "Unassigned grading item",
            DashboardAssignmentSeedState.Submitted);
        _ = await factory.CreateDashboardExamAsync(
            organizationA,
            assignedClass.ClassId,
            assignedEnrollment,
            membershipA.MembershipId,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(2),
            "Assigned manual exam",
            DashboardExamSeedState.DraftGrade);
        _ = await factory.CreateDashboardExamAsync(
            organizationA,
            unassignedClass.ClassId,
            unassignedEnrollment,
            membershipA.MembershipId,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(2),
            "Unassigned manual exam",
            DashboardExamSeedState.DraftGrade);

        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membershipA.MembershipId, OrganizationRole.Teacher);
        var dashboardA = await client.GetFromJsonAsync<TeacherDashboardDto>(
            "/api/v1/dashboards/teacher",
            JsonOptions);
        await SelectWorkspaceAsync(client, membershipB.MembershipId, OrganizationRole.Teacher);
        var dashboardB = await client.GetFromJsonAsync<TeacherDashboardDto>(
            "/api/v1/dashboards/teacher",
            JsonOptions);

        Assert.NotNull(dashboardA);
        Assert.Equal(nowUtc, dashboardA.DataAsOfUtc);
        Assert.Equal(assignedClass.ClassId, Assert.Single(dashboardA.AssignedClasses).ClassId);
        Assert.Equal(assignedUpcoming, Assert.Single(dashboardA.UpcomingSessions).SessionId);
        Assert.DoesNotContain(dashboardA.UpcomingSessions, item => item.SessionId == unassignedUpcoming);
        var attendanceItem = Assert.Single(dashboardA.AttendancePending);
        Assert.Equal(pendingAttendance, attendanceItem.SessionId);
        Assert.Equal(1, attendanceItem.MissingCount);
        Assert.Equal(1, dashboardA.AssignmentGradingQueue.AwaitingEvaluationCount);
        Assert.Equal(1, dashboardA.ExamGradingQueue.ManualReviewCount);
        Assert.Equal(organizationB, dashboardB?.OrganizationId);
        Assert.Equal(classB.ClassId, Assert.Single(dashboardB!.AssignedClasses).ClassId);
        Assert.Empty(dashboardB.UpcomingSessions);
        Assert.Equal(0, dashboardB.AssignmentGradingQueue.TotalPendingCount);
        Assert.Equal(0, dashboardB.ExamGradingQueue.TotalPendingCount);
    }

    [Fact]
    public async Task EmptyTeacherDashboardReturnsArraysAndZeroQueues()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Empty Teacher"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);

        var dashboard = await client.GetFromJsonAsync<TeacherDashboardDto>(
            "/api/v1/dashboards/teacher",
            JsonOptions);

        Assert.NotNull(dashboard);
        Assert.Empty(dashboard.AssignedClasses);
        Assert.Empty(dashboard.UpcomingSessions);
        Assert.Empty(dashboard.AttendancePending);
        Assert.Equal(0, dashboard.AssignmentGradingQueue.TotalPendingCount);
        Assert.Equal(0, dashboard.ExamGradingQueue.TotalPendingCount);
    }

    [Fact]
    public async Task ManagerDashboardReturnsOrganizationScopedOperationalStateWithoutPrivateFields()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        var managerUser = await CreateAuthenticatedUserAsync(client);
        var teacherUser = await CreateAuthenticatedUserAsync(teacherClient);
        var organizationA = await factory.CreateOrganizationAsync(NewName("Manager School A"));
        var academicA = await factory.CreateAcademicClassAsync(organizationA);
        var organizationB = await factory.CreateOrganizationAsync(NewName("Manager School B"));
        var academicB = await factory.CreateAcademicClassAsync(organizationB);
        var managerMembership = await factory.CreateMembershipAsync(
            managerUser.User.Id,
            organizationA,
            OrganizationRole.Manager);
        _ = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            organizationA,
            OrganizationRole.Teacher);
        var learnerId = (await factory.CreateOrganizationPersonsAsync(organizationA, 1))[0];
        var enrollmentId = await factory.CreateEnrollmentAsync(
            organizationA,
            academicA.ClassId,
            learnerId);
        var foreignLearnerId = (await factory.CreateOrganizationPersonsAsync(organizationB, 1))[0];
        _ = await factory.CreateEnrollmentAsync(organizationB, academicB.ClassId, foreignLearnerId);
        var missingLinkSession = await factory.CreateAcademicSessionAsync(
            organizationA,
            academicA.ClassId,
            nowUtc.AddHours(1),
            nowUtc.AddHours(2));
        _ = await factory.CreateAcademicSessionAsync(
            organizationA,
            academicA.ClassId,
            nowUtc.AddHours(2),
            nowUtc.AddHours(3),
            meetingUrl: "https://meeting.example.test/room");
        var pendingAttendanceSession = await factory.CreateAcademicSessionAsync(
            organizationA,
            academicA.ClassId,
            nowUtc.AddHours(-2),
            nowUtc.AddHours(-1),
            SessionStatus.Completed);
        _ = await factory.CreateInvitationAsync(
            teacherUser.User.Id,
            organizationA,
            OrganizationRole.Teacher);
        _ = await factory.CreateDashboardAssignmentAsync(
            organizationA,
            academicA.ClassId,
            enrollmentId,
            managerMembership.MembershipId,
            nowUtc.AddHours(4),
            "Manager assignment backlog",
            DashboardAssignmentSeedState.Submitted);
        _ = await factory.CreateDashboardExamAsync(
            organizationA,
            academicA.ClassId,
            enrollmentId,
            managerMembership.MembershipId,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(2),
            "Manager exam backlog",
            DashboardExamSeedState.DraftGrade);

        UseBearerToken(client, managerUser.AccessToken);
        await SelectWorkspaceAsync(client, managerMembership.MembershipId, OrganizationRole.Manager);
        using var response = await client.GetAsync("/api/v1/dashboards/manager");
        var rawJson = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var dashboard = JsonSerializer.Deserialize<ManagerDashboardDto>(rawJson, JsonOptions)!;

        Assert.Equal(organizationA, dashboard.OrganizationId);
        Assert.Equal(nowUtc, dashboard.DataAsOfUtc);
        Assert.Equal(1, dashboard.OperationalCounts.ActiveClassCount);
        Assert.Equal(1, dashboard.OperationalCounts.EnrolledLearnerCount);
        Assert.Equal(1, dashboard.OperationalCounts.TeacherCount);
        Assert.True(dashboard.Onboarding.OrganizationBasicDataAvailable);
        Assert.True(dashboard.Onboarding.HasAcademicPeriod);
        Assert.True(dashboard.Onboarding.HasCourse);
        Assert.True(dashboard.Onboarding.HasTeacher);
        Assert.True(dashboard.Onboarding.HasLearner);
        Assert.True(dashboard.Onboarding.HasClass);
        Assert.Equal(missingLinkSession, Assert.Single(dashboard.SessionsMissingMeetingLink).SessionId);
        Assert.Equal(pendingAttendanceSession, Assert.Single(dashboard.AttendancePending).SessionId);
        Assert.Equal(1, dashboard.PendingInvitationCount);
        Assert.Equal(1, dashboard.AssignmentGradingQueue.AwaitingEvaluationCount);
        Assert.Equal(1, dashboard.ExamGradingQueue.ManualReviewCount);
        Assert.DoesNotContain(organizationB.ToString(), rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(academicB.ClassId.ToString(), rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Message", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Copilot", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Plan", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Quota", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Subscription", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NewOrganizationManagerDashboardHasIncompleteOnboardingAndZeroState()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Empty Manager"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);

        var dashboard = await client.GetFromJsonAsync<ManagerDashboardDto>(
            "/api/v1/dashboards/manager",
            JsonOptions);

        Assert.NotNull(dashboard);
        Assert.Equal(new ManagerOperationalCounts(0, 0, 0), dashboard.OperationalCounts);
        Assert.True(dashboard.Onboarding.OrganizationBasicDataAvailable);
        Assert.False(dashboard.Onboarding.HasAcademicPeriod);
        Assert.False(dashboard.Onboarding.HasCourse);
        Assert.False(dashboard.Onboarding.HasTeacher);
        Assert.False(dashboard.Onboarding.HasLearner);
        Assert.False(dashboard.Onboarding.HasClass);
        Assert.Empty(dashboard.UpcomingSessions);
        Assert.Empty(dashboard.SessionsMissingMeetingLink);
        Assert.Empty(dashboard.AttendancePending);
        Assert.Equal(0, dashboard.PendingInvitationCount);
    }

    [Fact]
    public async Task StudentDetailedReportsArePagedOfficialAndReleaseSafe()
    {
        var nowUtc = DateTime.UtcNow.AddDays(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var organizationId = await factory.CreateOrganizationAsync(NewName("Student Reports"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var student = await CreateStudentAsync(client, organizationId, academic.ClassId);
        var firstSession = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId, nowUtc.AddHours(-4), nowUtc.AddHours(-3), SessionStatus.Completed);
        _ = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId, nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Completed);
        _ = await factory.CreateAttendanceAsync(organizationId, academic.ClassId, firstSession, student.EnrollmentId, student.MembershipId, AttendanceStatus.Present);
        var releasedAssignment = await factory.CreateDashboardAssignmentAsync(organizationId, academic.ClassId, student.EnrollmentId, student.MembershipId, nowUtc.AddHours(2), "Released report assignment", DashboardAssignmentSeedState.Released);
        var draftAssignment = await factory.CreateDashboardAssignmentAsync(organizationId, academic.ClassId, student.EnrollmentId, student.MembershipId, nowUtc.AddHours(3), "Draft grade assignment", DashboardAssignmentSeedState.DraftEvaluation);
        await factory.CloseDashboardAssignmentAsync(draftAssignment.AssignmentId);
        var otherLearnerId = (await factory.CreateOrganizationPersonsAsync(organizationId, 1))[0];
        var otherEnrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, otherLearnerId);
        var otherAssignment = await factory.CreateDashboardAssignmentAsync(organizationId, academic.ClassId, otherEnrollmentId, student.MembershipId, nowUtc.AddHours(1), "Other learner report assignment", DashboardAssignmentSeedState.Released);
        var releasedExam = await factory.CreateDashboardExamAsync(organizationId, academic.ClassId, student.EnrollmentId, student.MembershipId, nowUtc.AddHours(-1), nowUtc.AddHours(4), "Released report exam", DashboardExamSeedState.Released);
        var draftExam = await factory.CreateDashboardExamAsync(organizationId, academic.ClassId, student.EnrollmentId, student.MembershipId, nowUtc.AddHours(-1), nowUtc.AddHours(5), "Draft report exam", DashboardExamSeedState.DraftGrade);

        var attendancePage1 = await client.GetFromJsonAsync<AttendanceReportPage<StudentAttendanceReportItem>>("/api/v1/reports/student/attendance?pageNumber=1&pageSize=1&sort=occurredAsc", JsonOptions);
        var attendancePage2 = await client.GetFromJsonAsync<AttendanceReportPage<StudentAttendanceReportItem>>("/api/v1/reports/student/attendance?pageNumber=2&pageSize=1&sort=occurredAsc", JsonOptions);
        var emptyAttendance = await client.GetFromJsonAsync<AttendanceReportPage<StudentAttendanceReportItem>>($"/api/v1/reports/student/attendance?classId={Guid.NewGuid()}", JsonOptions);
        var filteredAttendance = await client.GetFromJsonAsync<AttendanceReportPage<StudentAttendanceReportItem>>($"/api/v1/reports/student/attendance?fromUtc={Uri.EscapeDataString(nowUtc.AddHours(-3).ToString("O"))}", JsonOptions);
        using var assignmentsResponse = await client.GetAsync("/api/v1/reports/student/assignments?pageSize=10");
        var assignmentsJson = await assignmentsResponse.Content.ReadAsStringAsync();
        assignmentsResponse.EnsureSuccessStatusCode();
        var assignments = JsonSerializer.Deserialize<ReportPage<StudentAssignmentReportItem>>(assignmentsJson, JsonOptions)!;
        using var examsResponse = await client.GetAsync("/api/v1/reports/student/exams?pageSize=10");
        var examsJson = await examsResponse.Content.ReadAsStringAsync();
        examsResponse.EnsureSuccessStatusCode();
        var exams = JsonSerializer.Deserialize<ReportPage<StudentExamReportItem>>(examsJson, JsonOptions)!;
        var grades = await client.GetFromJsonAsync<ReportPage<StudentReleasedGradeReportItem>>("/api/v1/reports/student/grades?pageSize=10", JsonOptions);

        Assert.NotNull(attendancePage1);
        Assert.NotNull(attendancePage2);
        Assert.Equal(nowUtc, attendancePage1.DataAsOfUtc);
        Assert.Equal(2, attendancePage1.TotalCount);
        Assert.Equal(2, attendancePage1.Summary.EligibleCount);
        Assert.Equal(1, attendancePage1.Summary.PresentCount);
        Assert.Equal(1, attendancePage1.Summary.NotRecordedCount);
        Assert.Equal(AttendanceStatus.Present, Assert.Single(attendancePage1.Items).Status);
        Assert.Equal(AttendanceStatus.NotRecorded, Assert.Single(attendancePage2!.Items).Status);
        Assert.NotNull(emptyAttendance);
        Assert.Equal(0, emptyAttendance.TotalCount);
        Assert.Empty(emptyAttendance.Items);
        Assert.NotNull(filteredAttendance);
        Assert.Equal(1, filteredAttendance.TotalCount);
        Assert.Equal(2, assignments.TotalCount);
        Assert.Equal(17.25m, assignments.Items.Single(item => item.AssignmentId == releasedAssignment.AssignmentId).ReleasedScore);
        Assert.Null(assignments.Items.Single(item => item.AssignmentId == draftAssignment.AssignmentId).ReleasedScore);
        Assert.Equal(AssignmentStatus.Closed, assignments.Items.Single(item => item.AssignmentId == draftAssignment.AssignmentId).AssignmentStatus);
        Assert.DoesNotContain(assignments.Items, item => item.AssignmentId == otherAssignment.AssignmentId);
        Assert.Equal(18m, exams.Items.Single(item => item.ExamId == releasedExam.ExamId).ReleasedScore);
        Assert.Null(exams.Items.Single(item => item.ExamId == draftExam.ExamId).ReleasedScore);
        Assert.NotNull(grades);
        Assert.Equal(2, grades.TotalCount);
        Assert.Contains(grades.Items, item => item.Kind == DetailedReportGradeKind.Assignment && item.Percentage == 86.25m);
        Assert.Contains(grades.Items, item => item.Kind == DetailedReportGradeKind.Exam && item.Percentage == 90m);
        foreach (var forbidden in new[] { "Private question content", "CorrectOption", "Teacher private note", "Evaluator private exam note" })
        {
            Assert.DoesNotContain(forbidden, assignmentsJson + examsJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DetailedReportValidationAndRoleGateReturnStableProblems()
    {
        using var anonymous = factory.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync("/api/v1/reports/student/attendance");
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Report Validation"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        using var paginationResponse = await client.GetAsync("/api/v1/reports/manager/enrollments?pageSize=101");
        using var sortResponse = await client.GetAsync("/api/v1/reports/manager/attendance?sort=dropTable");
        using var wrongRoleResponse = await client.GetAsync("/api/v1/reports/student/grades");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, paginationResponse.StatusCode);
        Assert.Equal(DetailedReportErrorCodes.PaginationInvalid, await ReadProblemCodeAsync(paginationResponse));
        Assert.Equal(DetailedReportErrorCodes.SortInvalid, await ReadProblemCodeAsync(sortResponse));
        Assert.Equal(HttpStatusCode.Forbidden, wrongRoleResponse.StatusCode);
        Assert.Equal(OrganizationErrorCodes.WorkspaceNotAllowed, await ReadProblemCodeAsync(wrongRoleResponse));
    }

    [Fact]
    public async Task ParentDetailedReportsUseSelectedChildAndGuardianVisibleFeedbackOnly()
    {
        var nowUtc = DateTime.UtcNow.AddDays(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var parent = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Parent Reports"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(parent.User.Id, organizationId, OrganizationRole.Parent);
        var child = await CreateChildAsync(parent.User.Id, organizationId, academic.ClassId);
        var otherChild = await CreateChildAsync(parent.User.Id, organizationId, academic.ClassId);
        var own = await factory.CreateDashboardAssignmentAsync(organizationId, academic.ClassId, child.EnrollmentId, membership.MembershipId, nowUtc.AddHours(2), "Selected child", DashboardAssignmentSeedState.Released);
        var other = await factory.CreateDashboardAssignmentAsync(organizationId, academic.ClassId, otherChild.EnrollmentId, membership.MembershipId, nowUtc.AddHours(2), "Other child", DashboardAssignmentSeedState.Released);
        UseBearerToken(client, parent.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        using var missingChild = await client.GetAsync("/api/v1/reports/parent/assignments");
        await SelectChildAsync(client, child.OrganizationPersonId);
        using var response = await client.GetAsync("/api/v1/reports/parent/assignments?pageSize=10");
        var raw = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var page = JsonSerializer.Deserialize<ReportPage<ParentAssignmentReportItem>>(raw, JsonOptions)!;

        Assert.Equal(HttpStatusCode.NotFound, missingChild.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, await ReadProblemCodeAsync(missingChild));
        var item = Assert.Single(page.Items);
        Assert.Equal(own.AssignmentId, item.AssignmentId);
        Assert.Equal("Guardian feedback", item.GuardianVisibleFeedback);
        Assert.DoesNotContain(other.AssignmentId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Learner feedback", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Teacher private note", raw, StringComparison.OrdinalIgnoreCase);

        var foreignOrganizationId = await factory.CreateOrganizationAsync(NewName("Parent Reports Foreign"));
        var foreignAcademic = await factory.CreateAcademicClassAsync(foreignOrganizationId);
        var foreignMembership = await factory.CreateMembershipAsync(parent.User.Id, foreignOrganizationId, OrganizationRole.Parent);
        var foreignChild = await CreateChildAsync(parent.User.Id, foreignOrganizationId, foreignAcademic.ClassId);
        var foreignAssignment = await factory.CreateDashboardAssignmentAsync(foreignOrganizationId, foreignAcademic.ClassId, foreignChild.EnrollmentId, foreignMembership.MembershipId, nowUtc.AddHours(2), "Foreign selected child", DashboardAssignmentSeedState.Released);
        await SelectWorkspaceAsync(client, foreignMembership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, foreignChild.OrganizationPersonId);
        var foreignPage = await client.GetFromJsonAsync<ReportPage<ParentAssignmentReportItem>>("/api/v1/reports/parent/assignments?pageSize=10", JsonOptions);
        Assert.Equal(foreignAssignment.AssignmentId, Assert.Single(foreignPage!.Items).AssignmentId);
        Assert.DoesNotContain(foreignPage.Items, item => item.AssignmentId == own.AssignmentId);

        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, child.OrganizationPersonId);
        await factory.RevokeGuardianRelationAsync(child.RelationId);
        using var revokedResponse = await client.GetAsync("/api/v1/reports/parent/attendance");
        Assert.Equal(HttpStatusCode.Forbidden, revokedResponse.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotAllowed, await ReadProblemCodeAsync(revokedResponse));
    }

    [Fact]
    public async Task TeacherDetailedReportsRequireActiveClassAssignmentAndScopeLearnerOverview()
    {
        var nowUtc = DateTime.UtcNow.AddDays(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var teacher = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Teacher Reports"));
        var assigned = await factory.CreateAcademicClassAsync(organizationId);
        var unassigned = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(teacher.User.Id, organizationId, OrganizationRole.Teacher);
        _ = await factory.CreateTeacherAssignmentAsync(organizationId, assigned.ClassId, membership.MembershipId);
        var learnerId = (await factory.CreateOrganizationPersonsAsync(organizationId, 1))[0];
        var enrollmentId = await factory.CreateEnrollmentAsync(organizationId, assigned.ClassId, learnerId);
        var sessionId = await factory.CreateAcademicSessionAsync(organizationId, assigned.ClassId, nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Completed);
        _ = await factory.CreateAttendanceAsync(organizationId, assigned.ClassId, sessionId, enrollmentId, membership.MembershipId, AttendanceStatus.Late);
        _ = await factory.CreateDashboardAssignmentAsync(organizationId, assigned.ClassId, enrollmentId, membership.MembershipId, nowUtc.AddHours(2), "Teacher grading", DashboardAssignmentSeedState.Submitted);
        _ = await factory.CreateDashboardExamAsync(organizationId, assigned.ClassId, enrollmentId, membership.MembershipId, nowUtc.AddHours(-1), nowUtc.AddHours(3), "Teacher exam", DashboardExamSeedState.DraftGrade);
        UseBearerToken(client, teacher.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);

        var attendance = await client.GetFromJsonAsync<AttendanceReportPage<TeacherAttendanceReportItem>>($"/api/v1/reports/teacher/classes/{assigned.ClassId}/attendance", JsonOptions);
        var assignments = await client.GetFromJsonAsync<ReportPage<TeacherAssignmentReportItem>>($"/api/v1/reports/teacher/classes/{assigned.ClassId}/assignments", JsonOptions);
        var exams = await client.GetFromJsonAsync<ReportPage<TeacherExamReportItem>>($"/api/v1/reports/teacher/classes/{assigned.ClassId}/exams", JsonOptions);
        var learner = await client.GetFromJsonAsync<TeacherLearnerOverviewDto>($"/api/v1/reports/teacher/classes/{assigned.ClassId}/learners/{learnerId}", JsonOptions);
        using var unassignedResponse = await client.GetAsync($"/api/v1/reports/teacher/classes/{unassigned.ClassId}/attendance");

        Assert.Equal(AttendanceStatus.Late, Assert.Single(attendance!.Items).Status);
        Assert.Equal(1, attendance.Summary.LateCount);
        Assert.Equal(enrollmentId, Assert.Single(assignments!.Items).EnrollmentId);
        Assert.Equal(enrollmentId, Assert.Single(exams!.Items).EnrollmentId);
        Assert.Equal(1, learner!.EligibleSessionCount);
        Assert.Equal(1, learner.LateCount);
        Assert.Equal(1, learner.SubmittedAssignmentCount);
        Assert.Equal(1, learner.FinalizedExamAttemptCount);
        Assert.Equal(HttpStatusCode.NotFound, unassignedResponse.StatusCode);
        Assert.Equal(DetailedReportErrorCodes.ResourceNotFound, await ReadProblemCodeAsync(unassignedResponse));

        var organizationB = await factory.CreateOrganizationAsync(NewName("Teacher Reports B"));
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        var membershipB = await factory.CreateMembershipAsync(teacher.User.Id, organizationB, OrganizationRole.Teacher);
        _ = await factory.CreateTeacherAssignmentAsync(organizationB, classB.ClassId, membershipB.MembershipId);
        var learnerB = (await factory.CreateOrganizationPersonsAsync(organizationB, 1))[0];
        var enrollmentB = await factory.CreateEnrollmentAsync(organizationB, classB.ClassId, learnerB);
        var assignmentB = await factory.CreateDashboardAssignmentAsync(organizationB, classB.ClassId, enrollmentB, membershipB.MembershipId, nowUtc.AddHours(2), "Second organization grading", DashboardAssignmentSeedState.Submitted);
        await SelectWorkspaceAsync(client, membershipB.MembershipId, OrganizationRole.Teacher);
        var organizationBPage = await client.GetFromJsonAsync<ReportPage<TeacherAssignmentReportItem>>($"/api/v1/reports/teacher/classes/{classB.ClassId}/assignments", JsonOptions);
        using var previousOrganizationResponse = await client.GetAsync($"/api/v1/reports/teacher/classes/{assigned.ClassId}/assignments");
        Assert.Equal(assignmentB.AssignmentId, Assert.Single(organizationBPage!.Items).AssignmentId);
        Assert.Equal(HttpStatusCode.NotFound, previousOrganizationResponse.StatusCode);
    }

    [Fact]
    public async Task ManagerDetailedReportsFilterWithinTenantAndUseNormalizedPerformance()
    {
        var nowUtc = DateTime.UtcNow.AddDays(1);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var teacher = await CreateAuthenticatedUserAsync(teacherClient);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Manager Reports"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var foreignOrganizationId = await factory.CreateOrganizationAsync(NewName("Foreign Reports"));
        var foreignAcademic = await factory.CreateAcademicClassAsync(foreignOrganizationId);
        var managerMembership = await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        var learnerId = (await factory.CreateOrganizationPersonsAsync(organizationId, 1))[0];
        var enrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learnerId);
        var foreignLearner = (await factory.CreateOrganizationPersonsAsync(foreignOrganizationId, 1))[0];
        _ = await factory.CreateEnrollmentAsync(foreignOrganizationId, foreignAcademic.ClassId, foreignLearner);
        var sessionId = await factory.CreateAcademicSessionAsync(organizationId, academic.ClassId, nowUtc.AddHours(-2), nowUtc.AddHours(-1), SessionStatus.Completed);
        _ = await factory.CreateAttendanceAsync(organizationId, academic.ClassId, sessionId, enrollmentId, managerMembership.MembershipId, AttendanceStatus.Absent);
        _ = await factory.CreateDashboardAssignmentAsync(organizationId, academic.ClassId, enrollmentId, managerMembership.MembershipId, nowUtc.AddHours(2), "Manager released assignment", DashboardAssignmentSeedState.Released);
        _ = await factory.CreateDashboardExamAsync(organizationId, academic.ClassId, enrollmentId, managerMembership.MembershipId, nowUtc.AddHours(-1), nowUtc.AddHours(3), "Manager released exam", DashboardExamSeedState.Released);
        UseBearerToken(teacherClient, teacher.AccessToken);
        using (var profileResponse = await teacherClient.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("Report", "Teacher", "Report Teacher", $"report.teacher.{Guid.NewGuid():N}"[..28]), JsonOptions))
        {
            profileResponse.EnsureSuccessStatusCode();
        }
        var teacherMembership = await factory.CreateMembershipAsync(teacher.User.Id, organizationId, OrganizationRole.Teacher);
        _ = await factory.CreateTeacherAssignmentAsync(organizationId, academic.ClassId, teacherMembership.MembershipId);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, managerMembership.MembershipId, OrganizationRole.Manager);

        var enrollments = await client.GetFromJsonAsync<ReportPage<ManagerEnrollmentReportItem>>($"/api/v1/reports/manager/enrollments?courseId={academic.CourseId}&status=Active", JsonOptions);
        var attendance = await client.GetFromJsonAsync<ReportPage<ManagerAttendanceReportItem>>($"/api/v1/reports/manager/attendance?classId={academic.ClassId}", JsonOptions);
        var assessments = await client.GetFromJsonAsync<ReportPage<ManagerAssessmentReportItem>>($"/api/v1/reports/manager/assessments?classId={academic.ClassId}", JsonOptions);
        var performance = await client.GetFromJsonAsync<ManagerClassPerformanceDto>($"/api/v1/reports/manager/classes/{academic.ClassId}", JsonOptions);
        var workload = await client.GetFromJsonAsync<ReportPage<ManagerTeacherWorkloadReportItem>>("/api/v1/reports/manager/teacher-workload", JsonOptions);
        using var foreignClass = await client.GetAsync($"/api/v1/reports/manager/classes/{foreignAcademic.ClassId}");

        Assert.Equal(enrollmentId, Assert.Single(enrollments!.Items).EnrollmentId);
        Assert.Equal(100m, Assert.Single(attendance!.Items).RecordedAbsencePercentage);
        Assert.Equal(2, assessments!.TotalCount);
        Assert.Equal(86.25m, performance!.AssignmentAveragePercentage);
        Assert.Equal(90m, performance.ExamAveragePercentage);
        Assert.Contains(workload!.Items, item => item.TeacherMembershipId == teacherMembership.MembershipId && item.ActiveClassCount == 1);
        Assert.Equal(HttpStatusCode.NotFound, foreignClass.StatusCode);
    }

    private async Task<StudentContext> CreateStudentAsync(
        HttpClient client,
        Guid organizationId,
        Guid classId)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profileResponse = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "Dashboard",
                "Student",
                "Dashboard Student",
                $"dashboard.student.{Guid.NewGuid().ToString("N")[..12]}"),
            JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        var personId = await factory.GetUserPersonIdAsync(user.User.Id);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        var enrollmentId = await factory.CreateEnrollmentAsync(
            organizationId,
            classId,
            organizationPersonId);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);
        return new StudentContext(
            user.User.Id,
            organizationPersonId,
            enrollmentId,
            membership.MembershipId);
    }

    private async Task<ChildContext> CreateChildAsync(
        Guid parentUserId,
        Guid organizationId,
        Guid classId)
    {
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        var relationId = await factory.CreateGuardianRelationAsync(
            parentUserId,
            organizationId,
            organizationPersonId);
        var enrollmentId = await factory.CreateEnrollmentAsync(
            organizationId,
            classId,
            organizationPersonId);
        return new ChildContext(organizationPersonId, enrollmentId, relationId);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98912{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge!.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
    }

    private static async Task SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SelectChildAsync(HttpClient client, Guid childId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{childId}/select",
            null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        factory.Clock.UseSystemTime();
        return Task.CompletedTask;
    }

    private sealed record StudentContext(
        Guid UserId,
        Guid OrganizationPersonId,
        Guid EnrollmentId,
        Guid MembershipId);

    private sealed record ChildContext(
        Guid OrganizationPersonId,
        Guid EnrollmentId,
        Guid RelationId);
}
