using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<DashboardAssignmentSeed> CreateDashboardAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid enrollmentId,
        Guid evaluatorMembershipId,
        DateTime dueAtUtc,
        string title,
        DashboardAssignmentSeedState state = DashboardAssignmentSeedState.NotStarted)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        var assignment = Assignment.CreateDraft(
            organizationId,
            classId,
            evaluatorMembershipId,
            nowUtc);
        var version = AssignmentVersion.CreateDraft(
            organizationId,
            classId,
            assignment.Id,
            1,
            title,
            "Dashboard integration-test assignment",
            dueAtUtc,
            true,
            3,
            20m,
            evaluatorMembershipId,
            nowUtc);
        version.Publish(nowUtc);
        assignment.Publish(nowUtc);
        var recipient = AssignmentRecipient.Create(
            organizationId,
            classId,
            assignment.Id,
            version.Id,
            enrollmentId,
            nowUtc);

        dbContext.Assignments.Add(assignment);
        dbContext.AssignmentVersions.Add(version);
        dbContext.AssignmentRecipients.Add(recipient);
        await dbContext.SaveChangesAsync();

        if (state == DashboardAssignmentSeedState.NotStarted)
        {
            return new DashboardAssignmentSeed(
                assignment.Id,
                version.Id,
                recipient.Id,
                null,
                null);
        }

        var attempt = SubmissionAttempt.CreateDraft(
            organizationId,
            assignment.Id,
            version.Id,
            recipient.Id,
            enrollmentId,
            1,
            nowUtc);
        attempt.SaveAnswer("Dashboard answer", nowUtc);
        attempt.Submit(nowUtc, dueAtUtc, true, false);
        dbContext.SubmissionAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        if (state == DashboardAssignmentSeedState.Submitted)
        {
            return new DashboardAssignmentSeed(
                assignment.Id,
                version.Id,
                recipient.Id,
                attempt.Id,
                null);
        }

        var evaluation = EvaluationRevision.CreateDraft(
            organizationId,
            attempt.Id,
            1,
            17.25m,
            version.MaxScore,
            "Learner feedback",
            "Guardian feedback",
            "Teacher private note",
            evaluatorMembershipId,
            nowUtc);
        dbContext.EvaluationRevisions.Add(evaluation);

        if (state == DashboardAssignmentSeedState.Released)
        {
            evaluation.Release(nowUtc);
            dbContext.GradeReleases.Add(GradeRelease.Create(
                organizationId,
                attempt.Id,
                evaluation.Id,
                evaluatorMembershipId,
                nowUtc));
        }

        await dbContext.SaveChangesAsync();
        return new DashboardAssignmentSeed(
            assignment.Id,
            version.Id,
            recipient.Id,
            attempt.Id,
            evaluation.Id);
    }

    public async Task<DashboardExamSeed> CreateDashboardExamAsync(
        Guid organizationId,
        Guid classId,
        Guid enrollmentId,
        Guid evaluatorMembershipId,
        DateTime availableFromUtc,
        DateTime availableUntilUtc,
        string title,
        DashboardExamSeedState state = DashboardExamSeedState.Available)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        var exam = Exam.CreateDraft(organizationId, classId, evaluatorMembershipId, nowUtc);
        var version = ExamVersion.CreateDraft(
            organizationId,
            exam.Id,
            1,
            title,
            "Dashboard integration-test exam",
            availableFromUtc,
            availableUntilUtc,
            45,
            2,
            20m,
            ExamRandomizationPolicy.None,
            evaluatorMembershipId,
            nowUtc);
        var question = QuestionVersion.CreateDraft(
            organizationId,
            version.Id,
            1,
            ExamQuestionType.Descriptive,
            "Private question content",
            20m,
            null,
            nowUtc);
        version.Publish([question], nowUtc);
        exam.PublishCurrentVersion(1);

        dbContext.Exams.Add(exam);
        dbContext.ExamVersions.Add(version);
        dbContext.QuestionVersions.Add(question);
        await dbContext.SaveChangesAsync();

        if (state == DashboardExamSeedState.Available)
        {
            return new DashboardExamSeed(exam.Id, version.Id, null, null);
        }

        var attemptStartUtc = availableFromUtc > nowUtc
            ? availableFromUtc.AddMinutes(1)
            : nowUtc;
        var attempt = ExamAttempt.Start(
            organizationId,
            exam.Id,
            version.Id,
            classId,
            enrollmentId,
            Guid.NewGuid(),
            1,
            version.MaxAttempts,
            availableFromUtc,
            availableUntilUtc,
            version.DurationMinutes,
            attemptStartUtc);
        var writerSessionId = await (
            from membership in dbContext.Memberships
            join session in dbContext.UserSessions on membership.UserId equals session.UserId
            where membership.Id == evaluatorMembershipId
            orderby session.CreatedAtUtc descending
            select session.Id)
            .FirstAsync();
        attempt.AcquireWriteLease(writerSessionId, attemptStartUtc);
        var attemptQuestion = ExamAttemptQuestion.Create(attempt, question.Id, 1);
        attempt.Finalize(
            writerSessionId,
            attempt.WriteLeaseVersion,
            attempt.AnswerSetVersion,
            Guid.NewGuid(),
            new string('a', ExamAttempt.FinalizeRequestHashLength),
            attemptStartUtc);
        dbContext.ExamAttempts.Add(attempt);
        dbContext.ExamAttemptQuestions.Add(attemptQuestion);
        await dbContext.SaveChangesAsync();

        var gradeRevision = ExamGradeRevision.CreateDraft(
            organizationId,
            attempt.Id,
            evaluatorMembershipId,
            attemptStartUtc);
        var questionGrade = ExamQuestionGrade.Create(
            gradeRevision,
            attempt,
            attemptQuestion,
            question,
            null,
            null,
            evaluatorMembershipId,
            attemptStartUtc);
        dbContext.ExamGradeRevisions.Add(gradeRevision);
        dbContext.ExamQuestionGrades.Add(questionGrade);

        if (state == DashboardExamSeedState.Released)
        {
            questionGrade.ReviewManually(
                18m,
                "Learner exam feedback",
                "Evaluator private exam note",
                evaluatorMembershipId,
                attemptStartUtc);
            gradeRevision.CompleteReview(
                [questionGrade],
                version.MaxScore,
                "Learner exam feedback",
                "Guardian exam feedback",
                "Evaluator private exam note",
                attemptStartUtc);
            gradeRevision.Release([questionGrade], version.MaxScore, attemptStartUtc);
            dbContext.ExamGradeReleases.Add(ExamGradeRelease.Create(
                gradeRevision,
                evaluatorMembershipId,
                Guid.NewGuid(),
                new string('b', ExamGradeRelease.RequestHashLength),
                attemptStartUtc));
        }

        await dbContext.SaveChangesAsync();
        return new DashboardExamSeed(exam.Id, version.Id, attempt.Id, gradeRevision.Id);
    }
}

public enum DashboardAssignmentSeedState
{
    NotStarted = 1,
    Submitted = 2,
    DraftEvaluation = 3,
    Released = 4
}

public enum DashboardExamSeedState
{
    Available = 1,
    DraftGrade = 2,
    Released = 3
}

public sealed record DashboardAssignmentSeed(
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid AssignmentRecipientId,
    Guid? SubmissionAttemptId,
    Guid? EvaluationRevisionId);

public sealed record DashboardExamSeed(
    Guid ExamId,
    Guid ExamVersionId,
    Guid? ExamAttemptId,
    Guid? ExamGradeRevisionId);
