using System.Text.Json;
using MakanApp.Application.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task AccountRealtimeExamDraftAndAnswerKeyStayPrivateAndPublishedExamInvalidatesEligibleStudent()
    {
        using var staff = _factory.CreateClient(); using var student = _factory.CreateClient();
        var manager = await CreateManagerAsync(staff);
        var classroom = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        _ = await CreateStudentAsync(student, manager.OrganizationId, classroom.ClassId);
        var boundary = await AccountReplayTestClient.ReadAsync(student, JsonOptions);
        var draft = await CreateDraftAsync(staff, classroom.ClassId);
        draft = await AddObjectiveQuestionAsync(staff, draft);
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(student, JsonOptions, boundary.NextCursor)).Values, x => x.Invalidation?.ResourceId == draft.Id);
        _ = await PublishAsync(staff, draft);
        var changes = await AccountReplayTestClient.DrainAsync(student, JsonOptions, boundary.NextCursor, 1);
        var published = Assert.Single(changes.Values, x => x.Invalidation?.Key == "assessment.exam" && x.Invalidation.ResourceId == draft.Id);
        var json = JsonSerializer.Serialize(published);
        Assert.DoesNotContain("correctOption", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountRealtimeExamGradeInvalidationWaitsForReleaseAndReusesCurrentGuardianAuthorization()
    {
        var scenario = await CreateFinalizedDescriptiveScenarioAsync(10m);
        using var student = scenario.StudentClient; using var teacher = _factory.CreateClient(); using var parent = _factory.CreateClient();
        _ = await CreateTeacherAsync(teacher, scenario.Manager.OrganizationId, scenario.ClassId, true);
        Guid learner;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            learner = await (from attempt in db.ExamAttempts join enrollment in db.Enrollments on attempt.EnrollmentId equals enrollment.Id
                where attempt.Id == scenario.Attempt.ExamAttemptId select enrollment.LearnerOrganizationPersonId).SingleAsync();
        }
        var relation = await CreateGradingParentAsync(parent, scenario.Manager.OrganizationId, learner);
        var studentCursor = (await AccountReplayTestClient.ReadAsync(student, JsonOptions)).NextCursor;
        var parentCursor = (await AccountReplayTestClient.ReadAsync(parent, JsonOptions)).NextCursor;
        var grade = await InitializeGradeAsync(teacher, scenario.Attempt.ExamAttemptId);
        grade = await GradeQuestionAsync(teacher, scenario.Attempt.ExamAttemptId, scenario.Question.AttemptQuestionId,
            new GradeExamQuestionCommand(7.25m, null, PrivateEvaluatorNote, grade.RowVersion));
        grade = await CompleteGradeAsync(teacher, scenario.Attempt.ExamAttemptId,
            new CompleteExamGradeCommand(null, null, PrivateEvaluatorNote, grade.RowVersion));
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(student, JsonOptions, studentCursor)).Values, x => x.Invalidation?.Key == "assessment.exam-grade");
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(parent, JsonOptions, parentCursor)).Values, x => x.Invalidation?.Key == "assessment.exam-grade");
        _ = await ReleaseGradeAsync(teacher, scenario.Attempt.ExamAttemptId, new ReleaseExamGradeCommand(Guid.NewGuid(), grade.RowVersion));
        foreach (var actor in new[] { (student, studentCursor), (parent, parentCursor) })
        {
            var changes = await AccountReplayTestClient.DrainAsync(actor.Item1, JsonOptions, actor.Item2);
            var released = Assert.Single(changes.Values, x => x.Invalidation?.Key == "assessment.exam-grade");
            Assert.Equal(scenario.Attempt.ExamAttemptId, released.Invalidation!.ResourceId);
            AssertSafeResult(JsonSerializer.Serialize(released));
        }
        await _factory.RevokeGuardianRelationAsync(relation.GuardianRelationId);
        using var denied = await parent.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(parentCursor));
        Assert.Equal(System.Net.HttpStatusCode.Conflict, denied.StatusCode);
    }
}
