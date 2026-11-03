using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfExamFinalizationStore(
    MakanDbContext dbContext,
    ILogger<EfExamFinalizationStore> logger, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IExamFinalizationStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamFinalizationTransaction(transaction);
    }

    public async Task<ExamAttempt?> GetOwnedAttemptForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null ||
            !await IsOwnedAsync(scope, attemptId, userId, cancellationToken))
        {
            return null;
        }

        return attempt;
    }

    public async Task<bool> IsSessionActiveForUpdateAsync(
        Guid sessionId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.UserSessions
            .FromSqlInterpolated(
                $"SELECT * FROM [identity].[UserSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sessionId} AND [UserId] = {userId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return session?.IsActive(nowUtc) == true;
    }

    public async Task<IReadOnlyCollection<ExamFinalizationQuestionRecord>> GetQuestionsForFinalizationAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var questions = await dbContext.ExamAttemptQuestions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttemptQuestions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamAttemptId] = {attemptId}")
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var revisionIds = questions
            .Where(question => question.CurrentAnswerRevisionId.HasValue)
            .Select(question => question.CurrentAnswerRevisionId!.Value)
            .ToArray();
        var revisions = await dbContext.AnswerRevisions
            .AsNoTracking()
            .Include(revision => revision.Attachments)
            .Where(revision => revision.ContextKind == scope.Kind && revision.ContextId == scope.Id &&
                               revision.ExamAttemptId == attemptId &&
                               revisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        var questionIds = questions.Select(item => item.QuestionVersionId).ToArray();
        var versions = await dbContext.QuestionVersions.AsNoTracking()
            .Where(item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && questionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return questions
            .Select(question => new ExamFinalizationQuestionRecord(
                question,
                question.CurrentAnswerRevisionId.HasValue
                    ? revisions[question.CurrentAnswerRevisionId.Value]
                    : null,
                versions[question.QuestionVersionId]))
            .ToArray();
    }

    public async Task<ExamFinalizationRecord?> GetOwnedFinalizationAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsOwnedAsync(scope, attemptId, userId, cancellationToken))
        {
            return null;
        }

        var attempt = await dbContext.ExamAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && item.Id == attemptId,
                cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var questions = await dbContext.ExamAttemptQuestions
            .AsNoTracking()
            .Where(question => question.ContextKind == scope.Kind && question.ContextId == scope.Id &&
                               question.ExamAttemptId == attemptId)
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var finalAnswers = await (
            from finalAnswer in dbContext.ExamFinalAnswers.AsNoTracking()
            join revision in dbContext.AnswerRevisions.AsNoTracking()
                on new
                {
                    finalAnswer.ContextKind,
                    finalAnswer.ContextId,
                    finalAnswer.ExamAttemptId,
                    finalAnswer.ExamAttemptQuestionId,
                    Id = finalAnswer.AnswerRevisionId
                }
                equals new
                {
                    revision.ContextKind,
                    revision.ContextId,
                    revision.ExamAttemptId,
                    revision.ExamAttemptQuestionId,
                    Id = revision.Id
                }
            where finalAnswer.ContextKind == scope.Kind && finalAnswer.ContextId == scope.Id &&
                  finalAnswer.ExamAttemptId == attemptId
            select new ExamFinalAnswerRecord(finalAnswer, revision))
            .ToArrayAsync(cancellationToken);
        var finalIds = finalAnswers.Select(item => item.Revision.Id).ToArray();
        var finalRevisions = await dbContext.AnswerRevisions.AsNoTracking().Include(item => item.Attachments)
            .Where(item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && finalIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        finalAnswers = finalAnswers.Select(item => item with { Revision = finalRevisions[item.Revision.Id] }).ToArray();
        return new ExamFinalizationRecord(attempt, questions, finalAnswers);
    }

    public void AddRange(IEnumerable<ExamFinalAnswer> finalAnswers) =>
        dbContext.ExamFinalAnswers.AddRange(finalAnswers);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await realtime.CaptureAssessmentAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Exam finalization concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "وضعیت تلاش آزمون هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            logger.LogWarning(
                "Exam finalization uniqueness conflict; no answer payload was logged.");
            throw new AssessmentException(
                AssessmentErrorCodes.ExamFinalizeConflict,
                "نهایی‌سازی هم‌زمان آزمون با تداخل مواجه شد.");
        }
    }

    private Task<bool> IsOwnedAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from attempt in dbContext.ExamAttempts
            join participant in AcademicContextQuery.Participants(dbContext, scope)
                on new { attempt.ContextKind, attempt.ContextId, attempt.ClassId, attempt.ParticipantId }
                equals new { participant.ContextKind, participant.ContextId, participant.ClassId, participant.ParticipantId }
            where attempt.ContextKind == scope.Kind && attempt.ContextId == scope.Id &&
                  attempt.Id == attemptId && participant.IsActive && participant.UserId == userId
            select attempt.Id)
            .AnyAsync(cancellationToken);

    private sealed class EfExamFinalizationTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
