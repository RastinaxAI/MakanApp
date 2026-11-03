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

public sealed class EfExamAnswerStore(
    MakanDbContext dbContext,
    ILogger<EfExamAnswerStore> logger, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IExamAnswerStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamAnswerTransaction(transaction);
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

    public async Task<ExamAnswersRecord?> GetOwnedAnswersAsync(
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

        var mappings = await dbContext.ExamAttemptQuestions
            .AsNoTracking()
            .Where(question => question.ContextKind == scope.Kind && question.ContextId == scope.Id &&
                               question.ExamAttemptId == attemptId)
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        return new ExamAnswersRecord(
            attempt,
            await BuildQuestionRecordsAsync(mappings, cancellationToken));
    }

    public async Task<ExamAnswerQuestionRecord?> GetQuestionForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid attemptQuestionId,
        CancellationToken cancellationToken)
    {
        var attemptQuestion = await dbContext.ExamAttemptQuestions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttemptQuestions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamAttemptId] = {attemptId} AND [Id] = {attemptQuestionId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attemptQuestion is null)
        {
            return null;
        }

        var question = await dbContext.QuestionVersions
            .AsNoTracking()
            .Include(item => item.Options)
            .SingleAsync(
                item => item.ContextKind == scope.Kind && item.ContextId == scope.Id &&
                        item.Id == attemptQuestion.QuestionVersionId,
                cancellationToken);
        AnswerRevision? currentRevision = null;
        if (attemptQuestion.CurrentAnswerRevisionId.HasValue)
        {
            currentRevision = await dbContext.AnswerRevisions
                .AsNoTracking()
                .Include(revision => revision.Attachments)
                .SingleAsync(
                    revision => revision.ContextKind == scope.Kind && revision.ContextId == scope.Id &&
                                revision.Id == attemptQuestion.CurrentAnswerRevisionId.Value,
                    cancellationToken);
        }

        var attachments = await ExamAttachmentQuery.LoadAsync(dbContext, currentRevision is null ? [] : [currentRevision.Id], cancellationToken);
        return new ExamAnswerQuestionRecord(attemptQuestion, question, currentRevision,
            currentRevision is not null && attachments.TryGetValue(currentRevision.Id, out var files) ? files : []);
    }

    public Task<AnswerRevision?> GetByClientOperationForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        Guid clientOperationId,
        CancellationToken cancellationToken) =>
        dbContext.AnswerRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AnswerRevisions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamAttemptId] = {attemptId} AND [ClientOperationId] = {clientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(AnswerRevision revision) => dbContext.AnswerRevisions.Add(revision);

    public async Task<bool> IsFileUsableForAttemptAsync(Guid fileAssetId, Guid attemptId, CancellationToken ct) =>
        !await dbContext.AssignmentVersionAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.EducationalSourceVersions.AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.AnswerRevisionAttachments.AnyAsync(x => x.FileAssetId == fileAssetId && x.ExamAttemptId != attemptId, ct) &&
        !await dbContext.SubmissionAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.ClassLessonAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.MessageAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.Set<MakanApp.Domain.Messaging.ScheduledMessageAttachment>().AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.ModerationEvidenceAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, ct) &&
        !await dbContext.Users.AnyAsync(x => x.ProfilePhotoFileAssetId == fileAssetId, ct) &&
        !await dbContext.Conversations.AnyAsync(x => x.PhotoFileAssetId == fileAssetId, ct) &&
        !await dbContext.ReportExportJobs.AnyAsync(x => x.Id == fileAssetId || x.OutputFileAssetId == fileAssetId, ct);

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
                "Exam answer concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "پاسخ یا وضعیت تلاش هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            logger.LogWarning(
                "Exam answer uniqueness conflict for attempt/question metadata; no answer payload was logged.");
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "ذخیره هم‌زمان پاسخ با تداخل مواجه شد؛ وضعیت جاری را دوباره دریافت کنید.");
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

    private async Task<IReadOnlyCollection<ExamAnswerQuestionRecord>> BuildQuestionRecordsAsync(
        IReadOnlyCollection<ExamAttemptQuestion> mappings,
        CancellationToken cancellationToken)
    {
        var questionIds = mappings.Select(mapping => mapping.QuestionVersionId).ToArray();
        var currentRevisionIds = mappings
            .Where(mapping => mapping.CurrentAnswerRevisionId.HasValue)
            .Select(mapping => mapping.CurrentAnswerRevisionId!.Value)
            .ToArray();
        var questions = await dbContext.QuestionVersions
            .AsNoTracking()
            .Where(question => questionIds.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, cancellationToken);
        var revisions = await dbContext.AnswerRevisions
            .AsNoTracking()
            .Include(revision => revision.Attachments)
            .Where(revision => currentRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        var attachments = await ExamAttachmentQuery.LoadAsync(dbContext, currentRevisionIds, cancellationToken);
        return mappings.Select(mapping => new ExamAnswerQuestionRecord(
            mapping,
            questions[mapping.QuestionVersionId],
            mapping.CurrentAnswerRevisionId.HasValue
                ? revisions[mapping.CurrentAnswerRevisionId.Value]
                : null,
            mapping.CurrentAnswerRevisionId.HasValue && attachments.TryGetValue(mapping.CurrentAnswerRevisionId.Value, out var files) ? files : []))
            .ToArray();
    }

    private sealed class EfExamAnswerTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
