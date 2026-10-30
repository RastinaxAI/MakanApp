using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Ai;
using MakanApp.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.AiIntegrationProbe;

internal sealed class LiveAiScenario(
    MakanAppWebApplicationFactory factory,
    WebApplicationFactory<Program> application,
    LiveAiObservation observation,
    JsonSerializerOptions jsonOptions,
    int timeoutSeconds = AiServiceOptions.DefaultTimeoutSeconds,
    FailureEvidenceCapture? evidence = null)
{
    private int _phoneSequence = 9_900_010;
    private Guid _organizationId;
    private Guid _studentId;
    private Guid _classId;
    private string? _onlyOperation;
    private bool _authorizationOnly;
    private bool _positiveCasesPassed = true;

    public async Task RunAsync(string? onlyOperation = null, bool authorizationOnly = false)
    {
        _onlyOperation = onlyOperation;
        _authorizationOnly = authorizationOnly;
        _organizationId = await factory.CreateOrganizationAsync("STEP10G Synthetic School");
        var foreignOrganization = await factory.CreateOrganizationAsync("STEP10G Foreign School");
        observation.OrganizationId = _organizationId;
        observation.ForeignOrganizationId = foreignOrganization;
        var academicClass = await factory.CreateAcademicClassAsync(_organizationId);
        _classId = academicClass.ClassId;
        var otherClass = await factory.CreateAcademicClassAsync(_organizationId);
        var foreignClass = await factory.CreateAcademicClassAsync(foreignOrganization);
        var unenrolledStudent = (await factory.CreateOrganizationPersonsAsync(_organizationId, 1)).Single();
        var foreignStudent = (await factory.CreateOrganizationPersonsAsync(foreignOrganization, 1)).Single();
        observation.ForbidPayload(foreignStudent.ToString("D"), foreignClass.ClassId.ToString("D"), unenrolledStudent.ToString("D"));
        var foreignEnrollment = await factory.CreateEnrollmentAsync(foreignOrganization, foreignClass.ClassId, foreignStudent);

        using var student = await CreateActorAsync(OrganizationRole.Student);
        using var parent = await CreateActorAsync(OrganizationRole.Parent);
        using var teacher = await CreateActorAsync(OrganizationRole.Teacher);
        using var manager = await CreateActorAsync(OrganizationRole.Manager);
        var studentPerson = await factory.GetUserPersonIdAsync(student.UserId);
        _studentId = await factory.CreateOrganizationPersonAsync(_organizationId, studentPerson);
        var enrollment = await factory.CreateEnrollmentAsync(_organizationId, _classId, _studentId);
        await factory.CreateTeacherAssignmentAsync(_organizationId, _classId, teacher.MembershipId);
        var guardian = await factory.CreateGuardianRelationAsync(parent.UserId, _organizationId, _studentId);
        using (var selected = await parent.Client.PostAsync($"/api/v1/guardian/children/{_studentId}/select", null))
            selected.EnsureSuccessStatusCode();
        var (assignmentId, examId, draftId) = await CreateAssessmentsAsync(teacher.MembershipId, enrollment);
        await CreateSyntheticAiContractAsync();
        var foreignTeacher = await factory.CreateMembershipAsync(teacher.UserId, foreignOrganization, OrganizationRole.Teacher);
        var (foreignAssignment, foreignExam, _) = await CreateAssessmentsAsync(
            foreignTeacher.MembershipId, foreignEnrollment, foreignOrganization, foreignClass.ClassId);
        observation.ForbidPayload(foreignAssignment.ToString("D"), foreignExam.ToString("D"));

        foreach (var actor in new[] { student, parent, teacher, manager })
            await PositiveAsync("chat_" + actor.Role, actor, "/api/v1/ai/chat",
                new SendAiChatCommand("برای یادگیری بهتر ریاضی یک راهکار کوتاه و کاربردی پیشنهاد بده.", null),
                learner: actor.Role is OrganizationRole.Student or OrganizationRole.Parent);

        await PositiveAsync("student_analyze", student, "/api/v1/ai/students/analyze", learner: true);
        await PositiveAsync("study_plan", student, "/api/v1/ai/study-plan",
            new CreateStudyPlanCommand(["مرور کسرها"], 30, null), learner: true);
        await PositiveAsync("homework_explain", student, $"/api/v1/ai/homeworks/{assignmentId}/explain",
            new ExplainHomeworkCommand("روش جمع کسرها را توضیح بده."), learner: true, classroom: true);
        await PositiveAsync("exam_prepare", student, $"/api/v1/ai/exams/{examId}/prepare", learner: true, classroom: true);
        await PositiveAsync("parent_child_summary", parent, "/api/v1/ai/parents/child-summary", learner: true);
        await PositiveAsync("teacher_class_summary", teacher, $"/api/v1/ai/teachers/classes/{_classId}/summary", classroom: true);
        await PositiveAsync("teacher_student_insight", teacher,
            $"/api/v1/ai/teachers/classes/{_classId}/students/{_studentId}/insight", learner: true, classroom: true);
        await PositiveAsync("teacher_activity_suggestions", teacher,
            $"/api/v1/ai/teachers/classes/{_classId}/activity-suggestions",
            new SuggestTeacherActivitiesCommand("تمرین کسرها", 30, ["کاغذ"], null), classroom: true);
        await PositiveAsync("manager_school_summary", manager, "/api/v1/ai/managers/school-summary");
        await PositiveAsync("manager_class_insight", manager, $"/api/v1/ai/managers/classes/{_classId}/insight", classroom: true);
        await PositiveAsync("manager_student_insight", manager,
            $"/api/v1/ai/managers/classes/{_classId}/students/{_studentId}/insight", learner: true, classroom: true);

        if (onlyOperation is not null)
        {
            Require(_positiveCasesPassed, "selected_operations_successful");
            Require(application.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot().All(observation.IsSafe)
                && observation.PayloadsSafe, "payload_and_backend_log_privacy");
            Require(observation.Measurements >= 2 && observation.MetricsSafe, "live_metrics_privacy");
            LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = evidence is null ? "diagnostic_completed" : "evidence_collection_completed",
                realAiCalls = observation.OutgoingCalls, passed = true }));
            return;
        }

        await NegativeAsync("parent_cross_child", parent, $"/api/v1/guardian/children/{unenrolledStudent}/select");
        await NegativeAsync("parent_cross_org_child", parent, $"/api/v1/guardian/children/{foreignStudent}/select");
        await NegativeAsync("student_cross_org_homework", student, $"/api/v1/ai/homeworks/{foreignAssignment}/explain",
            new ExplainHomeworkCommand("Synthetic cross-organization request"));
        await NegativeAsync("student_cross_org_exam", student, $"/api/v1/ai/exams/{foreignExam}/prepare");
        await NegativeAsync("teacher_cross_class", teacher, $"/api/v1/ai/teachers/classes/{otherClass.ClassId}/summary");
        await NegativeAsync("teacher_cross_org", teacher, $"/api/v1/ai/teachers/classes/{foreignClass.ClassId}/summary");
        await NegativeAsync("teacher_unassigned_activity", teacher, $"/api/v1/ai/teachers/classes/{otherClass.ClassId}/activity-suggestions",
            new SuggestTeacherActivitiesCommand("Synthetic objective", 10, [], null));
        await NegativeAsync("teacher_cross_student_without_enrollment", teacher,
            $"/api/v1/ai/teachers/classes/{_classId}/students/{unenrolledStudent}/insight");
        await NegativeAsync("manager_cross_school", manager, $"/api/v1/ai/managers/classes/{foreignClass.ClassId}/insight");
        await NegativeAsync("manager_foreign_learner", manager,
            $"/api/v1/ai/managers/classes/{_classId}/students/{foreignStudent}/insight");
        await NegativeAsync("role_mismatch", student, "/api/v1/ai/managers/school-summary");
        await NegativeAsync("unpublished_homework", student, $"/api/v1/ai/homeworks/{draftId}/explain",
            new ExplainHomeworkCommand("توضیح بده"));
        await factory.RevokeGuardianRelationAsync(guardian);
        await NegativeAsync("revoked_guardian", parent, "/api/v1/ai/parents/child-summary");
        await factory.EndMembershipAsync(teacher.MembershipId);
        await NegativeAsync("revoked_membership", teacher, $"/api/v1/ai/teachers/classes/{_classId}/summary");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var membership = await database.Memberships.SingleAsync(item => item.Id == manager.MembershipId);
            membership.Suspend();
            await database.SaveChangesAsync();
        }
        await NegativeAsync("suspended_membership", manager, "/api/v1/ai/managers/school-summary");
        await factory.SuspendUserAsync(student.UserId);
        await NegativeAsync("suspended_user", student, "/api/v1/ai/students/analyze", expected: HttpStatusCode.Unauthorized);

        var logs = application.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot();
        Require(logs.All(observation.IsSafe) && observation.PayloadsSafe, "payload_and_backend_log_privacy");
        if (!authorizationOnly)
            Require(observation.Measurements >= 30 && observation.MetricsSafe, "live_metrics_privacy");
        LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = authorizationOnly ? "authorization_completed" : "completed",
            realAiCalls = observation.OutgoingCalls, passed = true }));
    }

    private async Task PositiveAsync(string name, Actor actor, string route, object? body = null,
        bool learner = false, bool classroom = false)
    {
        var selected = _onlyOperation switch
        {
            "failure_evidence" => name is "parent_child_summary" or "teacher_activity_suggestions" or "manager_class_insight",
            "focused_three" => name is "parent_child_summary" or "teacher_activity_suggestions" or "manager_class_insight",
            "remaining_roles" => name.StartsWith("teacher_", StringComparison.Ordinal) || name.StartsWith("manager_", StringComparison.Ordinal),
            "remaining_managers" => name.StartsWith("manager_", StringComparison.Ordinal),
            _ => _onlyOperation is null || _onlyOperation == name
        };
        if (_authorizationOnly || !selected) return;
        observation.ExpectedRole = actor.Role.ToString();
        observation.ExpectedStudentId = learner ? _studentId : null;
        observation.ExpectedClassId = classroom ? _classId : null;
        var correlation = "step10g-" + Guid.NewGuid().ToString("N");
        actor.Client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        actor.Client.DefaultRequestHeaders.Add("X-Correlation-ID", correlation);
        var before = observation.OutgoingCalls;
        evidence?.Begin(name, route);
        var started = Stopwatch.GetTimestamp();
        using var response = await PostAsync(actor.Client, route, body);
        var responseJson = await response.Content.ReadAsStringAsync();
        if (evidence is not null)
        {
            evidence.Backend((int)response.StatusCode, responseJson, started);
            return;
        }
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var passed = response.IsSuccessStatusCode && observation.OutgoingCalls == before + 1 &&
            root.GetProperty("requestId").GetString() == observation.RequestId &&
            root.GetProperty("correlationId").GetString() == correlation && observation.CorrelationId == correlation;
        string? code = root.TryGetProperty("code", out var error) ? error.GetString() : null;
        LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = name,
            endpoint = System.Text.RegularExpressions.Regex.Replace(route, "[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}", "{id}"),
            httpStatus = (int)response.StatusCode, aiHttpStatus = observation.AiHttpStatus,
            aiErrorCode = observation.AiErrorCode, requestId = observation.RequestId,
            correlationId = observation.CorrelationId,
            elapsedMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2),
            modelAlias = observation.ModelAlias,
            errorCode = code is not null && System.Text.RegularExpressions.Regex.IsMatch(code, "^AI_[A-Z_]+$") ? code : null,
            passed }));
        _positiveCasesPassed &= passed;
        if (!passed && _onlyOperation != "focused_three") throw new InvalidOperationException("LIVE_AI_POSITIVE_GATE_FAILED");
    }

    private async Task NegativeAsync(string name, Actor actor, string route, object? body = null,
        HttpStatusCode? expected = null)
    {
        var before = observation.OutgoingCalls;
        using var response = await PostAsync(actor.Client, route, body);
        var denied = expected.HasValue ? response.StatusCode == expected :
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound;
        Require(denied && observation.OutgoingCalls == before, name);
    }

    private Task<HttpResponseMessage> PostAsync(HttpClient client, string route, object? body) =>
        body is null ? client.PostAsync(route, null) : client.PostAsJsonAsync(route, body, jsonOptions);

    private async Task<Actor> CreateActorAsync(OrganizationRole role)
    {
        var client = application.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 10);
        var phone = $"+98913{++_phoneSequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phone), jsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(jsonOptions))!;
        var otp = application.Services.GetRequiredService<OtpProviderTestControl>().GetLatestCode(phone);
        using var verification = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, otp), jsonOptions);
        verification.EnsureSuccessStatusCode();
        var user = (await verification.Content.ReadFromJsonAsync<VerifyOtpResult>(jsonOptions))!;
        observation.Forbid(phone, otp, user.AccessToken, user.RefreshToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("Synthetic", role.ToString(), "Synthetic Learner", $"probe.{role.ToString().ToLowerInvariant()}"),
            jsonOptions);
        profile.EnsureSuccessStatusCode();
        var membership = await factory.CreateMembershipAsync(user.User.Id, _organizationId, role);
        using var selected = await client.PostAsJsonAsync("/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membership.MembershipId, role), jsonOptions);
        selected.EnsureSuccessStatusCode();
        return new Actor(client, user.User.Id, membership.MembershipId, role);
    }

    private async Task CreateSyntheticAiContractAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        if (!database.Database.GetDbConnection().Database.StartsWith(MakanAppWebApplicationFactory.DatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException("ISOLATED_AI_FIXTURE_DATABASE_REQUIRED");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var plan = Plan.Create("ai-probe." + Guid.NewGuid().ToString("N"), "قرارداد ساختگی Probe؛ غیرقابل فروش", now.AddDays(-2), SubscriptionOwnerType.Organization);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-2));
        version.SetMonthlyTerms(BillingCalendar.Gregorian, "synthetic-probe-only", 80, 95); version.SetPrice(1000, CurrencyCode.Irr);
        foreach (var key in new[] { EntitlementKey.Ai, EntitlementKey.Academic, EntitlementKey.Assignments, EntitlementKey.Exams }) version.SetEntitlement(key, true);
        version.SetQuota(QuotaKey.AiTextRequests, 100); version.SetQuota(QuotaKey.ActiveClasses, 100); version.SetQuota(QuotaKey.ActiveLearners, 100);
        version.Publish(now.AddDays(-1)); database.Plans.Add(plan); database.PlanVersions.Add(version);
        database.Subscriptions.Add(Subscription.CreateMonthly(SubscriptionOwner.Organization(_organizationId), version, now.AddHours(-1), 0, now, null, true));
        await database.SaveChangesAsync();
    }

    private async Task<(Guid AssignmentId, Guid ExamId, Guid DraftId)> CreateAssessmentsAsync(
        Guid teacherId, Guid enrollment, Guid? organizationId = null, Guid? classId = null)
    {
        var organization = organizationId ?? _organizationId;
        var classroom = classId ?? _classId;
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var now = DateTime.UtcNow;
        var assignment = Assignment.CreateDraft(organization, classroom, teacherId, now);
        var version = AssignmentVersion.CreateDraft(organization, classroom, assignment.Id, 1,
            "تمرین کسرها", "جمع و تفریق کسرها را تمرین کنید.", now.AddDays(3), false, 1, 10m, teacherId, now);
        version.Publish(now);
        assignment.Publish(now);
        var draft = Assignment.CreateDraft(organization, classroom, teacherId, now);
        var draftVersion = AssignmentVersion.CreateDraft(organization, classroom, draft.Id, 1,
            "پیش‌نویس خصوصی", "این متن نباید به AI برسد.", now.AddDays(3), false, 1, 70m, teacherId, now);
        observation.ForbidPayload(draft.Id.ToString("D"), draftVersion.Title, draftVersion.Description);
        var exam = Exam.CreateDraft(organization, classroom, teacherId, now);
        var examVersion = ExamVersion.CreateDraft(organization, exam.Id, 1, "آزمون کسرها", "مرور جمع و تفریق",
            now.AddDays(1), now.AddDays(2), 30, 1, 30m, ExamRandomizationPolicy.None, teacherId, now);
        var question = QuestionVersion.CreateDraft(organization, examVersion.Id, 1,
            ExamQuestionType.Descriptive, "جمع یک دوم و یک چهارم چیست؟", 30m, null, now);
        examVersion.Publish([question], now);
        exam.PublishCurrentVersion(1);
        database.Assignments.AddRange(assignment, draft);
        database.AssignmentVersions.AddRange(version, draftVersion);
        database.AssignmentRecipients.Add(AssignmentRecipient.Create(
            organization, classroom, assignment.Id, version.Id, enrollment, now));
        database.Exams.Add(exam);
        database.ExamVersions.Add(examVersion);
        database.QuestionVersions.Add(question);
        await database.SaveChangesAsync();
        return (assignment.Id, exam.Id, draft.Id);
    }

    private static void Require(bool condition, string name)
    {
        LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = name, passed = condition }));
        if (!condition) throw new InvalidOperationException("LIVE_AI_GATE_FAILED");
    }

    private sealed record Actor(HttpClient Client, Guid UserId, Guid MembershipId, OrganizationRole Role) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
