using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class AiChatEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 9_100_000;

    [Fact]
    public async Task ParentSummary_MultipleChildren_OnlySelectedChildEducationalDataIsProjected()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Two Children"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organizationId, OrganizationRole.Parent);
        var childA = await factory.CreateOrganizationPersonAsync(organizationId, await factory.CreatePersonWithoutUserAsync());
        var childB = await factory.CreateOrganizationPersonAsync(organizationId, await factory.CreatePersonWithoutUserAsync());
        await factory.CreateGuardianRelationAsync(user.User.Id, organizationId, childA);
        await factory.CreateGuardianRelationAsync(user.User.Id, organizationId, childB);
        var classA = await factory.CreateAcademicClassAsync(organizationId);
        var classB = await factory.CreateAcademicClassAsync(organizationId);
        var enrollmentA = await factory.CreateEnrollmentAsync(organizationId, classA.ClassId, childA);
        var enrollmentB = await factory.CreateEnrollmentAsync(organizationId, classB.ClassId, childB);
        using var teacherClient = factory.CreateClient();
        var teacher = await CreateAuthenticatedUserAsync(teacherClient);
        var teacherMembership = await factory.CreateMembershipAsync(teacher.User.Id, organizationId, OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(organizationId, classA.ClassId, teacherMembership.MembershipId);
        await factory.CreateTeacherAssignmentAsync(organizationId, classB.ClassId, teacherMembership.MembershipId);
        var homeworkA = await SeedHomeworkAsync(classA.ClassId, enrollmentA, "AI_CHILD_A_ONLY", 10m);
        var homeworkB = await SeedHomeworkAsync(classB.ClassId, enrollmentB, "AI_CHILD_B_ONLY", 30m);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);

        foreach (var child in new[] { childA, childB, childA })
        {
            using var selected = await client.PostAsync($"/api/v1/guardian/children/{child}/select", null);
            selected.EnsureSuccessStatusCode();
            using var response = await client.PostAsync("/api/v1/ai/parents/child-summary", null);
            response.EnsureSuccessStatusCode();
            var request = Assert.IsType<AiParentChildSummaryRequest>(factory.AiService.LastRoleRequest);
            Assert.Equal(child.ToString("D"), request.Scope.StudentId);
            Assert.Equal(organizationId.ToString("D"), request.Scope.OrganizationId);
            var homework = Assert.Single(request.ChildData.Homeworks);
            Assert.Equal((child == childA ? homeworkA : homeworkB).ToString("D"), homework["assignmentId"].GetString());
            Assert.Equal(child == childA ? 10m : 30m, homework["maxScore"].GetDecimal());
            var serialized = JsonSerializer.Serialize(request, JsonOptions);
            Assert.DoesNotContain((child == childA ? childB : childA).ToString("D"), serialized);
            Assert.DoesNotContain(child == childA ? "AI_CHILD_B_ONLY" : "AI_CHILD_A_ONLY", serialized);
            Assert.DoesNotContain((child == childA ? homeworkB : homeworkA).ToString("D"), serialized);
            Assert.DoesNotContain("conversation", serialized, StringComparison.OrdinalIgnoreCase);
        }

        async Task<Guid> SeedHomeworkAsync(Guid classId, Guid enrollment, string title, decimal maximumScore)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var now = DateTime.UtcNow;
            var assignment = Assignment.CreateDraft(organizationId, classId, teacherMembership.MembershipId, now);
            var version = AssignmentVersion.CreateDraft(organizationId, classId, assignment.Id, 1,
                title, "Synthetic educational fixture", now.AddDays(1), false, 1, maximumScore,
                teacherMembership.MembershipId, now);
            version.Publish(now);
            assignment.Publish(now);
            database.Assignments.Add(assignment);
            database.AssignmentVersions.Add(version);
            database.AssignmentRecipients.Add(AssignmentRecipient.Create(organizationId, classId,
                assignment.Id, version.Id, enrollment, now));
            await database.SaveChangesAsync();
            return assignment.Id;
        }
    }

    [Fact]
    public async Task TeacherSummary_MultipleSchools_ReResolvesWorkspaceAndClassOnEveryRequest()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationA = await factory.CreateOrganizationAsync(NewName("AI Teacher A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("AI Teacher B"));
        var membershipA = await factory.CreateMembershipAsync(user.User.Id, organizationA, OrganizationRole.Teacher);
        var membershipB = await factory.CreateMembershipAsync(user.User.Id, organizationB, OrganizationRole.Teacher);
        var classA = await factory.CreateAcademicClassAsync(organizationA);
        var classB = await factory.CreateAcademicClassAsync(organizationB);
        await factory.CreateTeacherAssignmentAsync(organizationA, classA.ClassId, membershipA.MembershipId);
        await factory.CreateTeacherAssignmentAsync(organizationB, classB.ClassId, membershipB.MembershipId);
        UseBearerToken(client, user.AccessToken);

        foreach (var item in new[]
        {
            (Organization: organizationA, Membership: membershipA.MembershipId, Class: classA.ClassId, ForeignClass: classB.ClassId),
            (Organization: organizationB, Membership: membershipB.MembershipId, Class: classB.ClassId, ForeignClass: classA.ClassId),
            (Organization: organizationA, Membership: membershipA.MembershipId, Class: classA.ClassId, ForeignClass: classB.ClassId)
        })
        {
            await SelectWorkspaceAsync(client, item.Membership, OrganizationRole.Teacher);
            using var response = await client.PostAsync($"/api/v1/ai/teachers/classes/{item.Class}/summary", null);
            response.EnsureSuccessStatusCode();
            var request = Assert.IsType<AiTeacherClassSummaryRequest>(factory.AiService.LastRoleRequest);
            Assert.Equal(item.Organization.ToString("D"), request.Scope.OrganizationId);
            Assert.Equal(item.Organization.ToString("D"), request.Scope.SchoolId);
            Assert.Equal(item.Class.ToString("D"), request.Scope.ClassId);
            Assert.DoesNotContain(item.ForeignClass.ToString("D"), JsonSerializer.Serialize(request, JsonOptions));
            factory.AiService.Reset();
            using var denied = await client.PostAsync($"/api/v1/ai/teachers/classes/{item.ForeignClass}/summary", null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Null(factory.AiService.LastRoleRequest);
        }
    }

    [Fact]
    public async Task Chat_SuspendedMembership_IsDeniedBeforeAiCall()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organization = await factory.CreateOrganizationAsync(NewName("AI Suspended Membership"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organization, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var stored = await database.Memberships.SingleAsync(item => item.Id == membership.MembershipId);
            stored.Suspend();
            await database.SaveChangesAsync();
        }
        factory.AiService.Reset();
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("آزمون", null), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
        Assert.Null(factory.AiService.LastRoleRequest);
    }

    [Theory]
    [InlineData(OrganizationRole.Teacher, true)]
    [InlineData(OrganizationRole.Teacher, false)]
    [InlineData(OrganizationRole.Manager, true)]
    [InlineData(OrganizationRole.Manager, false)]
    public async Task StudentInsight_DeniesForeignClassOrStudentWithoutEnrollmentBeforeAiCall(
        OrganizationRole role, bool foreignClass)
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organization = await factory.CreateOrganizationAsync(NewName("AI Scope Allowed"));
        var otherOrganization = await factory.CreateOrganizationAsync(NewName("AI Scope Foreign"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organization, role);
        var allowedClass = await factory.CreateAcademicClassAsync(organization);
        var otherClass = await factory.CreateAcademicClassAsync(otherOrganization);
        var learner = await factory.CreateOrganizationPersonAsync(organization, await factory.CreatePersonWithoutUserAsync());
        if (role == OrganizationRole.Teacher)
            await factory.CreateTeacherAssignmentAsync(organization, allowedClass.ClassId, membership.MembershipId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, role);
        factory.AiService.Reset();
        var prefix = role == OrganizationRole.Teacher ? "teachers" : "managers";
        var selectedClass = foreignClass ? otherClass.ClassId : allowedClass.ClassId;
        using var response = await client.PostAsync($"/api/v1/ai/{prefix}/classes/{selectedClass}/students/{learner}/insight", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.AiService.LastRoleRequest);
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Teacher)]
    public async Task ManagerSummary_DeniesRoleMismatchBeforeAiCall(OrganizationRole role)
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organization = await factory.CreateOrganizationAsync(NewName("AI Role Mismatch"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organization, role);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, role);
        factory.AiService.Reset();
        using var response = await client.PostAsync("/api/v1/ai/managers/school-summary", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.AiService.LastRoleRequest);
    }

    [Fact]
    public async Task Chat_RequiresAuthenticatedSession()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
    }

    [Fact]
    public async Task Chat_Manager_UsesServerWorkspaceAndPropagatesCorrelation()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Manager"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "corr-ai-manager");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("  وضعیت چیست؟  ", null),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiChatResult>(JsonOptions);
        var request = Assert.IsType<AiChatRequest>(factory.AiService.LastRequest);
        Assert.Equal("پاسخ آزمایشی", result!.Answer.Text);
        Assert.Equal("corr-ai-manager", request.CorrelationId);
        Assert.Equal("Manager", request.Actor.Role);
        Assert.Equal(user.User.Id.ToString("D"), request.Actor.UserId);
        Assert.Equal(organizationId.ToString("D"), request.Scope.OrganizationId);
        Assert.Equal(organizationId.ToString("D"), request.Scope.SchoolId);
        Assert.Null(request.Scope.StudentId);
        Assert.Equal("وضعیت چیست؟", request.Message.Text);
        Assert.Equal("corr-ai-manager", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.False(string.IsNullOrWhiteSpace(result.RequestId));
        var logs = string.Join("\n", factory.SensitiveLogs.Snapshot());
        Assert.DoesNotContain(request.Message.Text, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(user.AccessToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Answer.Text, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManagerSchoolSummary_UsesAuthorizedOrganizationAndRecordingAi()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Manager Summary"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "corr-manager-summary");

        using var response = await client.PostAsync(
            "/api/v1/ai/managers/school-summary",
            null);

        response.EnsureSuccessStatusCode();
        var request = Assert.IsType<AiManagerSchoolSummaryRequest>(factory.AiService.LastRoleRequest);
        Assert.Equal("Manager", request.Actor.Role);
        Assert.Equal(organizationId.ToString("D"), request.Scope.OrganizationId);
        Assert.Equal(organizationId.ToString("D"), request.SchoolData.SchoolId);
        Assert.Equal("corr-manager-summary", request.CorrelationId);
    }

    [Fact]
    public async Task Chat_Student_UsesOrganizationPersonNotUserId()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profile = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "AI",
                "Student",
                "AI Student",
                $"ai.student.{Guid.NewGuid():N}"[..28]),
            JsonOptions);
        profile.EnsureSuccessStatusCode();
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Student Org"));
        var personId = await factory.GetUserPersonIdAsync(user.User.Id);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        var academicClass = await factory.CreateAcademicClassAsync(organizationId);
        await factory.CreateEnrollmentAsync(
            organizationId,
            academicClass.ClassId,
            organizationPersonId);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("برنامه مطالعه", null),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var request = Assert.IsType<AiChatRequest>(factory.AiService.LastRequest);
        Assert.Equal(organizationPersonId.ToString("D"), request.Scope.StudentId);
        Assert.NotEqual(user.User.Id.ToString("D"), request.Scope.StudentId);
        Assert.Equal("Student", request.Actor.Role);
    }

    [Fact]
    public async Task StudentAnalyze_UsesOwnOrganizationPersonOnly()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profile = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "AI", "Student", "AI Student", $"ai.student.{Guid.NewGuid():N}"[..28]),
            JsonOptions);
        profile.EnsureSuccessStatusCode();
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Analyze Student"));
        var personId = await factory.GetUserPersonIdAsync(user.User.Id);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        var academicClass = await factory.CreateAcademicClassAsync(organizationId);
        await factory.CreateEnrollmentAsync(
            organizationId,
            academicClass.ClassId,
            organizationPersonId);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id, organizationId, OrganizationRole.Student);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);

        using var response = await client.PostAsync("/api/v1/ai/students/analyze", null);

        response.EnsureSuccessStatusCode();
        var request = Assert.IsType<AiStudentAnalysisRequest>(factory.AiService.LastRoleRequest);
        Assert.Equal(organizationPersonId.ToString("D"), request.Scope.StudentId);
        Assert.Equal("Student", request.Actor.Role);
    }

    [Fact]
    public async Task ParentSummary_RequiresSelectedActiveRelation()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Parent"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id, organizationId, OrganizationRole.Parent);
        var childPersonId = await factory.CreatePersonWithoutUserAsync();
        var childId = await factory.CreateOrganizationPersonAsync(organizationId, childPersonId);
        var relationId = await factory.CreateGuardianRelationAsync(
            user.User.Id, organizationId, childId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        using var select = await client.PostAsync(
            $"/api/v1/guardian/children/{childId}/select",
            null);
        select.EnsureSuccessStatusCode();

        using var success = await client.PostAsync("/api/v1/ai/parents/child-summary", null);
        success.EnsureSuccessStatusCode();
        var request = Assert.IsType<AiParentChildSummaryRequest>(factory.AiService.LastRoleRequest);
        Assert.Equal(childId.ToString("D"), request.Scope.StudentId);

        factory.AiService.Reset();
        await factory.RevokeGuardianRelationAsync(relationId);
        using var revoked = await client.PostAsync("/api/v1/ai/parents/child-summary", null);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Null(factory.AiService.LastRoleRequest);
    }

    [Fact]
    public async Task TeacherSummary_RequiresActiveAssignmentToRequestedClass()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Teacher"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id, organizationId, OrganizationRole.Teacher);
        var assignedClass = await factory.CreateAcademicClassAsync(organizationId);
        var unassignedClass = await factory.CreateAcademicClassAsync(organizationId);
        await factory.CreateTeacherAssignmentAsync(
            organizationId, assignedClass.ClassId, membership.MembershipId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);

        using var success = await client.PostAsync(
            $"/api/v1/ai/teachers/classes/{assignedClass.ClassId}/summary",
            null);
        success.EnsureSuccessStatusCode();
        var request = Assert.IsType<AiTeacherClassSummaryRequest>(factory.AiService.LastRoleRequest);
        Assert.Equal(assignedClass.ClassId.ToString("D"), request.Scope.ClassId);

        factory.AiService.Reset();
        using var denied = await client.PostAsync(
            $"/api/v1/ai/teachers/classes/{unassignedClass.ClassId}/summary",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Null(factory.AiService.LastRoleRequest);
    }

    [Fact]
    public async Task ManagerClassInsight_RejectsCrossOrganizationClass()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var allowedOrganization = await factory.CreateOrganizationAsync(NewName("AI Manager Allowed"));
        var otherOrganization = await factory.CreateOrganizationAsync(NewName("AI Manager Other"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id, allowedOrganization, OrganizationRole.Manager);
        var otherClass = await factory.CreateAcademicClassAsync(otherOrganization);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);

        using var response = await client.PostAsync(
            $"/api/v1/ai/managers/classes/{otherClass.ClassId}/insight",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.AiService.LastRoleRequest);
    }

    [Fact]
    public async Task ManagerSchoolSummary_EndToEnd_UsesHmacCredentialHttpAdapterAndFakeAi()
    {
        var handler = new RecordingAiHttpHandler();
        var options = new AiServiceOptions
        {
            Enabled = true,
            BaseUrl = "http://ai.test",
            TimeoutSeconds = 5,
            ContractVersion = "v1",
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerBreakSeconds = 30
        };
        using var application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiServiceClient>();
                services.RemoveAll<IAiServiceCredentialProvider>();
                services.RemoveAll<AiServiceOptions>();
                services.RemoveAll<AiServiceAuthenticationOptions>();
                services.RemoveAll<AiServiceCircuitBreaker>();
                services.AddSingleton(options);
                services.AddSingleton(new AiServiceAuthenticationOptions
                {
                    Issuer = "makan-backend",
                    Audience = "makan-ai",
                    ServiceName = "makan-backend",
                    Scope = "makan.ai.invoke",
                    CurrentKeyId = "integration-current",
                    CurrentKey = "integration-only-ai-signing-key-32-bytes-minimum",
                    TokenLifetimeSeconds = 120
                });
                services.AddSingleton<IAiServiceCredentialProvider, HmacAiServiceCredentialProvider>();
                services.AddSingleton<AiServiceCircuitBreaker>();
                services.AddSingleton(handler);
                services.AddSingleton<IAiServiceClient>(serviceProvider => new AiServiceClient(
                    new HttpClient(serviceProvider.GetRequiredService<RecordingAiHttpHandler>())
                    {
                        Timeout = Timeout.InfiniteTimeSpan
                    },
                    serviceProvider.GetRequiredService<IAiServiceCredentialProvider>(),
                    serviceProvider.GetRequiredService<AiServiceOptions>(),
                    serviceProvider.GetRequiredService<AiServiceCircuitBreaker>()));
            }));
        using var client = application.CreateClient();
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98913{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions);
        var otp = application.Services
            .GetRequiredService<OtpProviderTestControl>()
            .GetLatestCode(phoneNumber);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge!.ChallengeId, phoneNumber, otp),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        var user = (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI HTTP E2E"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id, organizationId, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "corr-http-e2e");

        using var response = await client.PostAsync("/api/v1/ai/managers/school-summary", null);

        response.EnsureSuccessStatusCode();
        Assert.Equal("/api/v1/ai/managers/school-summary", handler.Path);
        Assert.Equal("corr-http-e2e", handler.CorrelationId);
        Assert.Equal("v1", handler.ContractVersion);
        Assert.NotNull(handler.Authorization);
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal(3, handler.Authorization.Parameter!.Split('.').Length);
        Assert.Contains("\"role\":\"Manager\"", handler.Body);
        Assert.DoesNotContain(user.AccessToken, handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_PersonalOrRevokedWorkspace_IsDeniedWithoutCallingAi()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);

        using var personalResponse = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, personalResponse.StatusCode);
        Assert.Equal(
            AiServiceErrorCodes.ContextNotAllowed,
            await ReadProblemCodeAsync(personalResponse));
        Assert.Null(factory.AiService.LastRequest);

        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Revoked"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        await factory.EndMembershipAsync(membership.MembershipId);
        using var revokedResponse = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, revokedResponse.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
    }

    [Fact]
    public async Task Chat_MultiOrganizationScope_FollowsServerSelectedWorkspaceOnly()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationA = await factory.CreateOrganizationAsync(NewName("AI Scope A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("AI Scope B"));
        var membershipA = await factory.CreateMembershipAsync(
            user.User.Id, organizationA, OrganizationRole.Manager);
        var membershipB = await factory.CreateMembershipAsync(
            user.User.Id, organizationB, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);

        await SelectWorkspaceAsync(client, membershipA.MembershipId, OrganizationRole.Manager);
        using var responseA = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("سازمان جاری", null),
            JsonOptions);
        responseA.EnsureSuccessStatusCode();
        Assert.Equal(
            organizationA.ToString("D"),
            factory.AiService.LastRequest?.Scope.OrganizationId);

        await SelectWorkspaceAsync(client, membershipB.MembershipId, OrganizationRole.Manager);
        using var responseB = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("سازمان جاری", null),
            JsonOptions);
        responseB.EnsureSuccessStatusCode();
        Assert.Equal(
            organizationB.ToString("D"),
            factory.AiService.LastRequest?.Scope.OrganizationId);
    }

    [Fact]
    public async Task Chat_SuspendedUser_IsDeniedByAuthenticationBeforeAiCall()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Suspended"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        factory.AiService.Reset();
        await factory.SuspendUserAsync(user.User.Id);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
    }

    [Theory]
    [InlineData(AiServiceErrorCodes.InvalidRequest, HttpStatusCode.BadRequest)]
    [InlineData(AiServiceErrorCodes.ServiceUnauthorized, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AiServiceErrorCodes.ServiceForbidden, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AiServiceErrorCodes.FeatureDisabled, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AiServiceErrorCodes.ServiceUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AiServiceErrorCodes.ServiceTimeout, HttpStatusCode.GatewayTimeout)]
    [InlineData(AiServiceErrorCodes.ResponseInvalid, HttpStatusCode.BadGateway)]
    [InlineData(AiServiceErrorCodes.RateLimited, HttpStatusCode.TooManyRequests)]
    public async Task Chat_MapsDependencyFailuresToSafeProblemDetails(
        string code,
        HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Failure"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        factory.AiService.FailNext(new AiServiceException(code, "خطای امن"));

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(code, await ReadProblemCodeAsync(response));
        Assert.DoesNotContain("FastAPI", await response.Content.ReadAsStringAsync());
        using var profile = await client.GetAsync("/api/v1/me");
        profile.EnsureSuccessStatusCode();
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98913{sequence:D7}";
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

    private async Task SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var context = (await response.Content.ReadFromJsonAsync<AccessContext>(JsonOptions))!;
        await EnsureAiContractAsync(context.OrganizationId!.Value);
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string NewName(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class RecordingAiHttpHandler : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? ContractVersion { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            CorrelationId = request.Headers.TryGetValues("X-Correlation-ID", out var correlation)
                ? correlation.Single()
                : null;
            ContractVersion = request.Headers.TryGetValues("X-AI-Contract-Version", out var contract)
                ? contract.Single()
                : null;
            Authorization = request.Headers.Authorization;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(Body);
            var requestId = document.RootElement.GetProperty("requestId").GetString();
            var correlationId = document.RootElement.GetProperty("correlationId").GetString();
            var responseBody = JsonSerializer.Serialize(new
            {
                requestId,
                correlationId,
                summary = "خلاصه آزمایشی",
                strengths = Array.Empty<object>(),
                attentionAreas = Array.Empty<object>(),
                risks = Array.Empty<object>(),
                recommendations = Array.Empty<object>(),
                sources = Array.Empty<object>(),
                metadata = new { modelAlias = "education-fast", processingTimeMs = 1 }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    public Task InitializeAsync()
    {
        factory.AiService.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        factory.AiService.Reset();
        return Task.CompletedTask;
    }
}
