using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private async Task<ExamEditorDto> PersonalExamAsync(PersonalAcademicScenario s, int duration = 10)
    {
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var exam = await PostAsync<ExamEditorDto>(s.Manager.Client, $"/api/v1/academic/personal-classes/{s.Class.Id}/exams",
            new CreateExamDraftCommand("آزمون مستقل", "راهنمای ساختگی", now.AddMinutes(-1), now.AddDays(1), duration, 2, 20m, ExamRandomizationPolicy.None));
        exam = await PostAsync<ExamEditorDto>(s.Manager.Client, $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(1, ExamQuestionType.ObjectiveSingleChoice, "پرسش گزینه‌ای", 5m,
                [new(1, "گزینه درست", true), new(2, "گزینه دوم", false)], exam.VersionRowVersion));
        exam = await PostAsync<ExamEditorDto>(s.Manager.Client, $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(2, ExamQuestionType.Descriptive, "پرسش تشریحی", 15m, null, exam.VersionRowVersion,
                new(true, false, ["image/png", "application/pdf"], 3, 1_000_000)));
        return await PostAsync<ExamEditorDto>(s.Manager.Client, $"/api/v1/academic/exams/{exam.Id}/publish",
            new PublishExamCommand(exam.ExamRowVersion, exam.VersionRowVersion));
    }

    private static Task<StudentExamAttemptDto> PersonalExamStartAsync(TestActor learner, Guid examId, Guid? operation = null) =>
        PostAsync<StudentExamAttemptDto>(learner.Client, $"/api/v1/academic/exams/{examId}/attempts/start", new StartExamCommand(operation ?? Guid.NewGuid()));

    [Fact]
    public async Task PersonalAcademicExamVersionLeaseAttachmentsFinalizeGradingAndCorrection()
    {
        var s = await PersonalAcademicAsync();
        var exam = await PersonalExamAsync(s);
        Assert.Null(exam.OrganizationId); Assert.Equal(AcademicClassKind.PersonalClass, exam.ClassRef!.Kind);
        var preview = await s.Learner.Client.GetStringAsync($"/api/v1/academic/exams/{exam.Id}/student-preview");
        Assert.DoesNotContain("isCorrect", preview, StringComparison.OrdinalIgnoreCase);
        using (var editor = await s.Learner.Client.GetAsync($"/api/v1/academic/exams/{exam.Id}/editor")) Assert.Equal(HttpStatusCode.NotFound, editor.StatusCode);
        var operation = Guid.NewGuid();
        var starts = await Task.WhenAll(PersonalExamStartAsync(s.Learner, exam.Id, operation), PersonalExamStartAsync(s.Learner, exam.Id, operation));
        var attempt = starts[0]; var replay = starts[1];
        Assert.Equal(attempt.ExamAttemptId, replay.ExamAttemptId); Assert.Equal(attempt.EffectiveDeadlineUtc, replay.EffectiveDeadlineUtc);
        var active = await GetAsync<StudentExamAttemptDto>(s.Learner.Client, $"/api/v1/academic/exams/{exam.Id}/attempts/me/active");
        Assert.Equal(attempt.ExamAttemptId, active.ExamAttemptId);
        Assert.Equal(attempt.EffectiveDeadlineUtc, active.EffectiveDeadlineUtc);
        var next = await PostAsync<ExamEditorDto>(s.Manager.Client, $"/api/v1/academic/exams/{exam.Id}/versions", new CreateNextExamVersionCommand(exam.ExamRowVersion, exam.VersionRowVersion));
        Assert.NotEqual(exam.VersionId, next.VersionId);
        var route = $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}";
        var lease = await PostAsync<ExamWriteLeaseDto>(s.Learner.Client, route + "/write-lease", null);
        var objective = attempt.Questions.Single(q => q.QuestionType == ExamQuestionType.ObjectiveSingleChoice);
        var descriptive = attempt.Questions.Single(q => q.QuestionType == ExamQuestionType.Descriptive);
        var correct = exam.Questions.Single(q => q.Type == ExamQuestionType.ObjectiveSingleChoice).Options.Single(o => o.IsCorrect).Id;
        var answerCommand = new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, correct, null);
        var answer = await AcademicWriteAsync<ExamAnswerReceiptDto>(s.Learner.Client, HttpMethod.Put, route + $"/answers/{objective.AttemptQuestionId}", answerCommand);
        Assert.Equal(answer, await AcademicWriteAsync<ExamAnswerReceiptDto>(s.Learner.Client, HttpMethod.Put, route + $"/answers/{objective.AttemptQuestionId}", answerCommand));
        using (var staleAnswer = await s.Learner.Client.PutAsJsonAsync(route + $"/answers/{objective.AttemptQuestionId}",
            answerCommand with { ClientOperationId = Guid.NewGuid() }, Json))
            Assert.Equal(AssessmentErrorCodes.ExamAnswerVersionConflict, await ProblemAsync(staleAnswer));
        var file = await PersonalFileAsync(s.Learner, s.Class.Id);
        var attached = await PostAsync<ExamAnswerReceiptDto>(s.Learner.Client, route + $"/answers/{descriptive.AttemptQuestionId}/attachments",
            new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), lease.WriteLeaseVersion, null));
        var device = await AnotherDeviceAsync(s.Learner);
        var transferred = await PostAsync<ExamWriteLeaseDto>(device.Client, route + "/write-lease/transfer", null);
        using (var staleWriter = await s.Learner.Client.PutAsJsonAsync(route + $"/answers/{objective.AttemptQuestionId}",
            answerCommand with { ClientOperationId = Guid.NewGuid(), ExpectedRevisionNumber = answer.RevisionNumber }, Json))
            Assert.Equal(AssessmentErrorCodes.ExamWriteLeaseStale, await ProblemAsync(staleWriter));
        var finish = new FinalizeExamCommand(Guid.NewGuid(), attached.AnswerSetVersion, transferred.WriteLeaseVersion);
        var receipt = await PostAsync<ExamFinalReceiptDto>(device.Client, route + "/finalize", finish);
        Assert.Equal(receipt, await PostAsync<ExamFinalReceiptDto>(device.Client, route + "/finalize", finish));
        Assert.Equal(exam.VersionId, receipt.ExamVersionId); Assert.Equal(2, receipt.AnsweredQuestionCount);
        Assert.Equal("Manual", receipt.FinalizationMode);
        var grade = await PostAsync<ExamGradeDto>(s.Manager.Client, route + "/grading", null);
        Assert.Equal(5m, grade.Questions.Single(q => q.QuestionType == ExamQuestionType.ObjectiveSingleChoice).AwardedScore);
        grade = await AcademicWriteAsync<ExamGradeDto>(s.Manager.Client, HttpMethod.Patch, route + $"/grading/questions/{descriptive.AttemptQuestionId}",
            new GradeExamQuestionCommand(12m, "بازخورد پرسش", "PRIVATE-QUESTION-NOTE", grade.RowVersion));
        grade = await PostAsync<ExamGradeDto>(s.Manager.Client, route + "/grading/complete", new CompleteExamGradeCommand("بازخورد آزمون", null, "PRIVATE-GRADE-NOTE", grade.RowVersion));
        Assert.Null((await GetAsync<StudentExamResultDto>(s.Learner.Client, route + "/result")).Score);
        var release = await PostAsync<ExamGradeReleaseDto>(s.Manager.Client, route + "/grade/release", new ReleaseExamGradeCommand(Guid.NewGuid(), grade.RowVersion));
        Assert.Equal(17m, (await GetAsync<StudentExamResultDto>(s.Learner.Client, route + "/result")).Score);
        var resultJson = await s.Learner.Client.GetStringAsync(route + "/result");
        Assert.DoesNotContain("PRIVATE-", resultJson); Assert.DoesNotContain("isCorrect", resultJson);
        foreach (var actor in new[] { s.Peer, s.Admin })
        {
            using var hidden = await actor.Client.GetAsync(route + "/result"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            using var guardianHidden = await actor.Client.GetAsync(route + "/guardian-result"); Assert.False(guardianHidden.IsSuccessStatusCode);
            using var fileHidden = await actor.Client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, fileHidden.StatusCode);
        }
        using (var evaluatorFile = await s.Manager.Client.GetAsync($"/api/v1/files/{file.Id}/content")) evaluatorFile.EnsureSuccessStatusCode();
        grade = await PostAsync<ExamGradeDto>(s.Manager.Client, route + "/grade/corrections", new CorrectReleasedExamGradeCommand("اصلاح مستند", release.GradeRowVersion));
        grade = await AcademicWriteAsync<ExamGradeDto>(s.Manager.Client, HttpMethod.Patch, route + $"/grading/questions/{descriptive.AttemptQuestionId}", new GradeExamQuestionCommand(14m, "بازخورد اصلاحی", null, grade.RowVersion));
        grade = await PostAsync<ExamGradeDto>(s.Manager.Client, route + "/grading/complete", new CompleteExamGradeCommand("نتیجه اصلاحی", null, null, grade.RowVersion));
        await PostAsync<ExamGradeReleaseDto>(s.Manager.Client, route + "/grade/release", new ReleaseExamGradeCommand(Guid.NewGuid(), grade.RowVersion));
        Assert.Equal(19m, (await GetAsync<StudentExamResultDto>(s.Learner.Client, route + "/result")).Score);
        await WithDb(async db =>
        {
            Assert.Equal(2, await db.ExamGradeRevisions.CountAsync(g => g.ExamAttemptId == attempt.ExamAttemptId));
            var audits = await db.AdminAuditLogs.Where(a => a.TargetId == attempt.ExamAttemptId.ToString() && a.TargetType == "PersonalClassExamAttempt").ToArrayAsync();
            Assert.Single(audits, a => a.Action == "personal_class.academic.ExamResultReleased");
            Assert.Single(audits, a => a.Action == "personal_class.academic.ExamResultCorrected");
            Assert.All(audits, a => { Assert.DoesNotContain("PRIVATE-", a.AfterSummary ?? ""); Assert.DoesNotContain("PRIVATE-", a.Reason); });
            var stored = await db.ExamAttempts.SingleAsync(a => a.Id == attempt.ExamAttemptId);
            Assert.Null(stored.OrganizationId); Assert.Null(stored.EnrollmentId); Assert.NotNull(stored.PersonalClassMembershipId);
        });
    }

    [Fact]
    public async Task PersonalAcademicExamDeadlineFinalizationIsRecoverableConcurrentAndDoesNotInventAnswers()
    {
        var s = await PersonalAcademicAsync(); var exam = await PersonalExamAsync(s, 1);
        var attempt = await PersonalExamStartAsync(s.Learner, exam.Id);
        var route = $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}";
        var lease = await PostAsync<ExamWriteLeaseDto>(s.Learner.Client, route + "/write-lease", null);
        var question = attempt.Questions.Single(q => q.QuestionType == ExamQuestionType.Descriptive);
        var saved = await AcademicWriteAsync<ExamAnswerReceiptDto>(s.Learner.Client, HttpMethod.Put, route + $"/answers/{question.AttemptQuestionId}",
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, null, "آخرین پاسخ پذیرفته‌شده"));
        factory.Clock.SetUtcNow(attempt.EffectiveDeadlineUtc);
        async Task<bool> RecoverAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IExamDeadlineFinalizer>().FinalizeDueAttemptAsync(attempt.ExamAttemptId, default);
        }
        var recovered = await Task.WhenAll(RecoverAsync(), RecoverAsync()); Assert.Single(recovered, x => x);
        var receipt = await GetAsync<ExamFinalReceiptDto>(s.Learner.Client, route + "/receipt");
        Assert.Equal("Deadline", receipt.FinalizationMode); Assert.Equal(attempt.EffectiveDeadlineUtc, receipt.FinalizedAtUtc);
        Assert.Equal(saved.AnswerSetVersion, receipt.FinalizedAnswerSetVersion); Assert.Equal(1, receipt.AnsweredQuestionCount);
        Assert.Equal(receipt, await PostAsync<ExamFinalReceiptDto>(s.Learner.Client, route + "/finalize", new FinalizeExamCommand(Guid.NewGuid(), saved.AnswerSetVersion, lease.WriteLeaseVersion)));
        using (var late = await s.Learner.Client.PutAsJsonAsync(route + $"/answers/{question.AttemptQuestionId}",
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, 1, null, "پاسخ دیررس"), Json)) Assert.False(late.IsSuccessStatusCode);
        await WithDb(async db =>
        {
            Assert.Equal(saved.AnswerRevisionId, (await db.ExamFinalAnswers.SingleAsync(a => a.ExamAttemptId == attempt.ExamAttemptId)).AnswerRevisionId);
            Assert.Single(await db.AnswerRevisions.Where(a => a.ExamAttemptId == attempt.ExamAttemptId).ToArrayAsync());
            Assert.Null((await db.ExamAttempts.SingleAsync(a => a.Id == attempt.ExamAttemptId)).FinalizeClientOperationId);
        });
    }
}
