using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ReportExportEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 9_000_000;

    [Fact]
    public async Task StudentExportRunsAsynchronouslyAndDownloadsPrivateSafeCsv()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(5);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using (var profileResponse = await client.PatchAsJsonAsync(
                   "/api/v1/me/profile",
                   new CompleteProfileCommand(
                       "Report",
                       "Student",
                       "Report Student",
                       ($"report.student.{Guid.NewGuid():N}")[..28]),
                   JsonOptions))
        {
            profileResponse.EnsureSuccessStatusCode();
        }

        var organizationId = await factory.CreateOrganizationAsync(NewName("Student Export"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var personId = await factory.GetUserPersonIdAsync(user.User.Id);
        var learnerId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, OrganizationRole.Student);
        var enrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, learnerId);
        var assignment = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            enrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(2),
            "=HYPERLINK(\"https://invalid\")",
            DashboardAssignmentSeedState.Released);
        var otherLearnerId = (await factory.CreateOrganizationPersonsAsync(organizationId, 1))[0];
        var otherEnrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, otherLearnerId);
        _ = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            otherEnrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(2),
            "Other learner private row",
            DashboardAssignmentSeedState.Released);
        _ = await factory.CreateDashboardExamAsync(
            organizationId,
            academic.ClassId,
            enrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(-1),
            nowUtc.AddHours(3),
            "Safe exam metadata",
            DashboardExamSeedState.Released);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/reports/exports",
            new CreateReportExportCommand(
                ReportExportType.StudentAssignments,
                ReportExportFormat.Csv,
                new ReportExportFilters(ClassId: academic.ClassId)),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<ReportExportJobResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.Equal(ReportExportJobStatus.Queued, created!.Status);
        Assert.Equal(1, created.ExpectedRowCount);
        using var earlyDownload = await client.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");
        Assert.Equal(HttpStatusCode.Conflict, earlyDownload.StatusCode);

        Assert.Equal(1, await factory.ProcessAllReportExportsAsync());
        var completed = await client.GetFromJsonAsync<ReportExportJobResult>(
            $"/api/v1/reports/exports/{created.JobId}",
            JsonOptions);
        using var download = await client.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");
        var csv = await download.Content.ReadAsStringAsync();

        Assert.Equal(ReportExportJobStatus.Completed, completed!.Status);
        Assert.True(completed.DownloadAvailable);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("text/csv", download.Content.Headers.ContentType!.MediaType);
        Assert.Contains(assignment.AssignmentId.ToString(), csv, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'=HYPERLINK", csv);
        Assert.Contains("17.25", csv);
        Assert.Contains("20", csv);
        Assert.DoesNotContain("Teacher private note", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Other learner private row", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private question content", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Otp", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.StorageRootPath, csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", download.Content.Headers.ContentDisposition!.FileNameStar ?? string.Empty);
        Assert.Equal(1, await factory.CountReportOutputFilesAsync(created.JobId));
        Assert.Equal(0, await factory.ProcessAllReportExportsAsync());
        Assert.Equal(1, await factory.CountReportOutputFilesAsync(created.JobId));
        var persisted = await factory.GetReportExportJobAsync(created.JobId);
        Assert.Equal(created.JobId, persisted.OutputFileAssetId);

        _ = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            enrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(4),
            "Unreleased grade must stay hidden",
            DashboardAssignmentSeedState.DraftEvaluation);
        var grades = await CreateExportAsync(client, ReportExportType.StudentGrades);
        Assert.Equal(1, await factory.ProcessAllReportExportsAsync());
        var gradeCsv = await client.GetStringAsync($"/api/v1/reports/exports/{grades.JobId}/download");
        Assert.DoesNotContain("Unreleased grade must stay hidden", gradeCsv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Teacher private note", gradeCsv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoleAndGuessedJobBoundariesRejectWithoutLeakingAnotherUsersJob()
    {
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(10));
        using var ownerClient = factory.CreateClient();
        using var attackerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        var attacker = await CreateAuthenticatedUserAsync(attackerClient);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Manager Export"));
        var ownerMembership = await factory.CreateMembershipAsync(owner.User.Id, organizationId, OrganizationRole.Manager);
        var attackerMembership = await factory.CreateMembershipAsync(attacker.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(ownerClient, owner.AccessToken);
        UseBearerToken(attackerClient, attacker.AccessToken);
        await SelectWorkspaceAsync(ownerClient, ownerMembership.MembershipId, OrganizationRole.Manager);
        await SelectWorkspaceAsync(attackerClient, attackerMembership.MembershipId, OrganizationRole.Manager);

        using var wrongRole = await ownerClient.PostAsJsonAsync(
            "/api/v1/reports/exports",
            new CreateReportExportCommand(ReportExportType.StudentGrades, ReportExportFormat.Csv, null),
            JsonOptions);
        var created = await CreateExportAsync(ownerClient, ReportExportType.ManagerAttendance);
        using var guessed = await attackerClient.GetAsync($"/api/v1/reports/exports/{created.JobId}");
        using var guessedDownload = await attackerClient.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");

        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, guessedDownload.StatusCode);
        Assert.Equal(ReportExportErrorCodes.NotFound, await ReadProblemCodeAsync(guessed));
        Assert.Equal(1, await factory.ProcessAllReportExportsAsync());
    }

    [Fact]
    public async Task ParentGuardianRevocationBlocksCompletedDownload()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(15);
        factory.Clock.SetUtcNow(nowUtc);
        using var client = factory.CreateClient();
        var parent = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Parent Export"));
        var academic = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(parent.User.Id, organizationId, OrganizationRole.Parent);
        var childPersonId = await factory.CreatePersonWithoutUserAsync();
        var childId = await factory.CreateOrganizationPersonAsync(organizationId, childPersonId);
        var unrelatedPersonId = await factory.CreatePersonWithoutUserAsync();
        var unrelatedChildId = await factory.CreateOrganizationPersonAsync(organizationId, unrelatedPersonId);
        var relationId = await factory.CreateGuardianRelationAsync(parent.User.Id, organizationId, childId);
        var enrollmentId = await factory.CreateEnrollmentAsync(organizationId, academic.ClassId, childId);
        _ = await factory.CreateDashboardAssignmentAsync(
            organizationId,
            academic.ClassId,
            enrollmentId,
            membership.MembershipId,
            nowUtc.AddHours(1),
            "Child visible assignment",
            DashboardAssignmentSeedState.Released);
        UseBearerToken(client, parent.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        using (var unrelatedSelection = await client.PostAsync(
                   $"/api/v1/guardian/children/{unrelatedChildId}/select",
                   null))
        {
            Assert.Equal(HttpStatusCode.NotFound, unrelatedSelection.StatusCode);
        }

        await SelectChildAsync(client, childId);
        var created = await CreateExportAsync(client, ReportExportType.ParentChildAssignments);
        Assert.Equal(1, await factory.ProcessAllReportExportsAsync());
        using var beforeRevocation = await client.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");
        beforeRevocation.EnsureSuccessStatusCode();

        await factory.RevokeGuardianRelationAsync(relationId);
        using var afterRevocation = await client.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");

        Assert.Equal(HttpStatusCode.Forbidden, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task TeacherCannotCreateExportForUnassignedClass()
    {
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(20));
        using var client = factory.CreateClient();
        var teacher = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Teacher Export"));
        var assigned = await factory.CreateAcademicClassAsync(organizationId);
        var unassigned = await factory.CreateAcademicClassAsync(organizationId);
        var membership = await factory.CreateMembershipAsync(teacher.User.Id, organizationId, OrganizationRole.Teacher);
        _ = await factory.CreateTeacherAssignmentAsync(organizationId, assigned.ClassId, membership.MembershipId);
        UseBearerToken(client, teacher.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/reports/exports",
            new CreateReportExportCommand(
                ReportExportType.TeacherClassAttendance,
                ReportExportFormat.Csv,
                new ReportExportFilters(ClassId: unassigned.ClassId)),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ManagerExportIsTenantScopedAndDoesNotContainForeignRows()
    {
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(25));
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationA = await factory.CreateOrganizationAsync(NewName("Manager Tenant A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("Manager Tenant B"));
        var classA = await factory.CreateAcademicClassAsync(organizationA);
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        var learnerA = (await factory.CreateOrganizationPersonsAsync(organizationA, 1))[0];
        var learnerB = (await factory.CreateOrganizationPersonsAsync(organizationB, 1))[0];
        _ = await factory.CreateEnrollmentAsync(organizationA, classA.ClassId, learnerA);
        _ = await factory.CreateEnrollmentAsync(organizationB, classB.ClassId, learnerB);
        var membership = await factory.CreateMembershipAsync(manager.User.Id, organizationA, OrganizationRole.Manager);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var created = await CreateExportAsync(client, ReportExportType.ManagerEnrollments);
        Assert.Equal(1, await factory.ProcessAllReportExportsAsync());

        var csv = await client.GetStringAsync($"/api/v1/reports/exports/{created.JobId}/download");

        Assert.Contains(classA.ClassId.ToString(), csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(classB.ClassId.ToString(), csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqlClaimIsExclusiveAndExpiredLeaseIsRecoverable()
    {
        var nowUtc = DateTime.UtcNow.AddMinutes(30);
        factory.Clock.SetUtcNow(nowUtc);
        _ = await factory.ProcessAllReportExportsAsync();
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Claim Export"));
        var membership = await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var created = await CreateExportAsync(client, ReportExportType.ManagerAttendance);
        var reread = await factory.GetReportExportJobAsync(created.JobId);

        Assert.Equal(ReportExportJobStatus.Queued, reread.Status);
        var firstClaims = await factory.ClaimReportExportsConcurrentlyAsync();
        Assert.Equal(1, firstClaims.Claims);
        Assert.Equal(created.JobId, Assert.Single(firstClaims.ClaimedJobIds));

        factory.Clock.SetUtcNow(nowUtc.AddMinutes(3));
        var recoveryClaims = await factory.ClaimReportExportsConcurrentlyAsync();
        var recovered = await factory.GetReportExportJobAsync(created.JobId);

        Assert.Equal(1, recoveryClaims.Claims);
        Assert.Equal(created.JobId, Assert.Single(recoveryClaims.ClaimedJobIds));
        Assert.Equal(2, recovered.AttemptCount);
        Assert.Equal(ReportExportJobStatus.Processing, recovered.Status);
        using var processingDownload = await client.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");
        Assert.Equal(HttpStatusCode.Conflict, processingDownload.StatusCode);
    }

    [Fact]
    public async Task RevokedProcessingScopeProducesOnlySafeFailureCode()
    {
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(35));
        using var client = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("Failed Export"));
        var membership = await factory.CreateMembershipAsync(manager.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(client, manager.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var created = await CreateExportAsync(client, ReportExportType.ManagerAttendance);
        await SelectPersonalWorkspaceAsync(client);

        Assert.Equal(1, await factory.ProcessAllReportExportsAsync());
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        var failed = await client.GetFromJsonAsync<ReportExportJobResult>(
            $"/api/v1/reports/exports/{created.JobId}",
            JsonOptions);

        Assert.Equal(ReportExportJobStatus.Failed, failed!.Status);
        Assert.Equal(ReportExportErrorCodes.DataNoLongerAuthorized, failed.FailureCode);
        Assert.DoesNotContain("Exception", JsonSerializer.Serialize(failed), StringComparison.OrdinalIgnoreCase);
        using var failedDownload = await client.GetAsync($"/api/v1/reports/exports/{created.JobId}/download");
        Assert.Equal(HttpStatusCode.Conflict, failedDownload.StatusCode);
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
            new VerifyOtpCommand(challenge!.ChallengeId, phoneNumber, factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
    }

    private static async Task<ReportExportJobResult> CreateExportAsync(
        HttpClient client,
        ReportExportType reportType)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/reports/exports",
            new CreateReportExportCommand(reportType, ReportExportFormat.Csv, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportExportJobResult>(JsonOptions))!;
    }

    private static async Task SelectWorkspaceAsync(HttpClient client, Guid membershipId, OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SelectPersonalWorkspaceAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Personal, null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SelectChildAsync(HttpClient client, Guid childId)
    {
        using var response = await client.PostAsync($"/api/v1/guardian/children/{childId}/select", null);
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
}
