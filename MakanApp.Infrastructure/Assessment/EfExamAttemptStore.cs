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

public sealed class EfExamAttemptStore(
    MakanDbContext dbContext,
    ILogger<EfExamAttemptStore> logger, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IExamAttemptStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamAttemptTransaction(transaction);
    }

    public async Task<ExamStartContextRecord?> GetStartContextForUpdateAsync(
        AcademicScope scope,
        Guid examId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[Exams] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {examId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (exam is null)
        {
            return null;
        }

        var participantId = await AcademicContextQuery.Participants(dbContext, scope)
            .Where(item => item.ContextKind == scope.Kind && item.ContextId == scope.Id &&
                item.ClassId == exam.ClassId && item.IsActive && item.UserId == userId)
            .Select(item => (Guid?)item.ParticipantId).SingleOrDefaultAsync(cancellationToken);
        if (!participantId.HasValue)
        {
            return new ExamStartContextRecord(exam, null, null, []);
        }

        var participant = await AcademicContextQuery.LockParticipantAsync(dbContext, scope,
            participantId.Value, cancellationToken);
        var participation = participant is null ? null : AcademicContextQuery.Participation(participant);
        if (participation is not { IsActive: true }) return new ExamStartContextRecord(exam, null, null, []);
        if (!exam.LatestPublishedVersionNumber.HasValue)
        {
            return new ExamStartContextRecord(exam, participation, null, []);
        }

        var version = await dbContext.ExamVersions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamId] = {examId} AND [VersionNumber] = {exam.LatestPublishedVersionNumber.Value}")
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return new ExamStartContextRecord(exam, participation, null, []);
        }

        var questions = await dbContext.QuestionVersions
            .Include(question => question.Options)
            .Where(question => question.ContextKind == scope.Kind && question.ContextId == scope.Id &&
                               question.ExamVersionId == version.Id)
            .OrderBy(question => question.Order)
            .ToArrayAsync(cancellationToken);
        return new ExamStartContextRecord(exam, participation, version, questions);
    }

    public async Task<IReadOnlyCollection<ExamAttempt>> GetAttemptsForUpdateAsync(
        AcademicScope scope,
        Guid examId,
        Guid enrollmentId,
        CancellationToken cancellationToken) =>
        (await dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamId] = {examId} AND [ParticipantId] = {enrollmentId}")
            .ToArrayAsync(cancellationToken))
        .OrderBy(attempt => attempt.AttemptNumber)
        .ToArray();

    public Task<ExamAttempt?> GetByClientOperationForUpdateAsync(
        AcademicScope scope,
        Guid enrollmentId,
        Guid clientOperationId,
        CancellationToken cancellationToken) =>
        dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ParticipantId] = {enrollmentId} AND [ClientOperationId] = {clientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ExamAttemptRecord?> GetByIdAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .SingleOrDefaultAsync(
                item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && item.Id == attemptId,
                cancellationToken);
        return attempt is null
            ? null
            : await BuildRecordAsync(attempt, cancellationToken);
    }

    public async Task<ExamAttemptRecord?> GetOwnedByIdAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var ownedAttemptId = await CreateOwnedAttemptQuery(scope, userId)
            .Where(attempt => attempt.Id == attemptId)
            .Select(attempt => (Guid?)attempt.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return ownedAttemptId.HasValue
            ? await GetByIdAsync(scope, ownedAttemptId.Value, cancellationToken)
            : null;
    }

    public async Task<ExamAttemptRecord?> GetOwnedActiveAsync(
        AcademicScope scope,
        Guid examId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var attemptId = await CreateOwnedAttemptQuery(scope, userId)
            .Where(attempt => attempt.ExamId == examId &&
                              attempt.Status == ExamAttemptStatus.InProgress &&
                              attempt.EffectiveDeadlineUtc > nowUtc)
            .Select(attempt => (Guid?)attempt.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return attemptId.HasValue
            ? await GetByIdAsync(scope, attemptId.Value, cancellationToken)
            : null;
    }

    public async Task<IReadOnlyCollection<ExamAttemptRecord>> GetOwnedAttemptsAsync(
        AcademicScope scope,
        Guid examId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var attemptIds = await CreateOwnedAttemptQuery(scope, userId)
            .Where(attempt => attempt.ExamId == examId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .Select(attempt => attempt.Id)
            .ToArrayAsync(cancellationToken);
        var records = new List<ExamAttemptRecord>(attemptIds.Length);
        foreach (var attemptId in attemptIds)
        {
            var record = await GetByIdAsync(scope, attemptId, cancellationToken);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    public void Add(ExamAttempt attempt) => dbContext.ExamAttempts.Add(attempt);

    public void AddRange(IEnumerable<ExamAttemptQuestion> questions) =>
        dbContext.ExamAttemptQuestions.AddRange(questions);

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
                "Exam attempt optimistic concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "تلاش آزمون هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            logger.LogWarning("شروع تلاش آزمون با تعارض یکتایی مواجه شد؛ جزئیات SQL ثبت نمی‌شود.");
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "شروع آزمون با درخواست هم‌زمان تداخل داشت؛ دوباره تلاش کنید.");
        }
    }

    private IQueryable<ExamAttempt> CreateOwnedAttemptQuery(AcademicScope scope, Guid userId) =>
        from attempt in dbContext.ExamAttempts.AsNoTracking()
        join participant in AcademicContextQuery.Participants(dbContext, scope)
            on new { attempt.ContextKind, attempt.ContextId, attempt.ClassId, attempt.ParticipantId }
            equals new { participant.ContextKind, participant.ContextId, participant.ClassId, participant.ParticipantId }
        where attempt.ContextKind == scope.Kind && attempt.ContextId == scope.Id && participant.IsActive && participant.UserId == userId
        select attempt;

    private async Task<ExamAttemptRecord> BuildRecordAsync(
        ExamAttempt attempt,
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.ExamAttemptQuestions
            .AsNoTracking()
            .Where(mapping => mapping.ContextKind == attempt.ContextKind && mapping.ContextId == attempt.ContextId &&
                              mapping.ExamAttemptId == attempt.Id)
            .OrderBy(mapping => mapping.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var questionIds = mappings.Select(mapping => mapping.QuestionVersionId).ToArray();
        var questions = await dbContext.QuestionVersions
            .AsNoTracking()
            .Include(question => question.Options)
            .Where(question => question.ContextKind == attempt.ContextKind && question.ContextId == attempt.ContextId &&
                               questionIds.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, cancellationToken);
        return new ExamAttemptRecord(
            attempt,
            mappings.Select(mapping => new FrozenExamQuestionRecord(
                mapping,
                questions[mapping.QuestionVersionId]))
                .ToArray());
    }

    private sealed class EfExamAttemptTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
