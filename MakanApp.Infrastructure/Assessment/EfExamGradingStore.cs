using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Guardian;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfExamGradingStore(MakanDbContext dbContext, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IExamGradingStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamGradingTransaction(transaction);
    }

    public async Task<ExamGradeAggregateRecord?> GetAggregateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && item.Id == attemptId,
                cancellationToken);
        return attempt is null
            ? null
            : await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
    }

    public async Task<ExamGradeAggregateRecord?> GetAggregateForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        _ = await dbContext.ExamGradeRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamGradeRevisions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        _ = await dbContext.ExamQuestionGrades
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamQuestionGrades] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        _ = await dbContext.ExamGradeReleases
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamGradeReleases] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        return await LoadAggregateAsync(attempt, asNoTracking: false, cancellationToken);
    }

    public Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueForManagerAsync(
        AcademicScope scope,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(scope, null, examId, query, cancellationToken);

    public Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueForTeacherAsync(
        AcademicScope scope,
        Guid membershipId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(scope, membershipId, examId, query, cancellationToken);

    public Task<bool> HasActiveTeacherAssignmentAsync(
        AcademicScope scope,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeacherAssignments.AnyAsync(
            assignment => assignment.OrganizationId == scope.OrganizationId &&
                          assignment.ClassId == classId &&
                          assignment.TeacherMembershipId == membershipId &&
                          assignment.Status == TeacherAssignmentStatus.Active &&
                          assignment.EndedAtUtc == null,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetActiveGuardianUserIdsAsync(
        AcademicScope scope,
        Guid learnerOrganizationPersonId,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        await dbContext.GuardianRelations
            .Where(relation => relation.OrganizationId == scope.OrganizationId &&
                               relation.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                               relation.Status == GuardianRelationStatus.Active &&
                               relation.ValidFromUtc <= nowUtc &&
                               relation.EndedAtUtc == null)
            .Select(relation => relation.GuardianUserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    public void Add(ExamGradeRevision revision) => dbContext.ExamGradeRevisions.Add(revision);

    public void AddRange(IEnumerable<ExamQuestionGrade> questionGrades) =>
        dbContext.ExamQuestionGrades.AddRange(questionGrades);

    public void Add(ExamGradeRelease release) => dbContext.ExamGradeReleases.Add(release);

    public void SetOriginalRowVersion(ExamGradeRevision revision, byte[] rowVersion) =>
        dbContext.Entry(revision).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await realtime.CaptureAssessmentAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "نمره آزمون هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeReleaseConflict,
                "نمره یا انتشار هم‌زمان دیگری برای این آزمون ثبت شده است.");
        }
    }

    private async Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueAsync(
        AcademicScope scope,
        Guid? teacherMembershipId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<ExamAttempt> attemptsQuery = (
            from attempt in dbContext.ExamAttempts
            join exam in dbContext.Exams
                on new { attempt.ContextKind, attempt.ContextId, attempt.ExamId }
                equals new { exam.ContextKind, exam.ContextId, ExamId = exam.Id }
            where attempt.ContextKind == scope.Kind && attempt.ContextId == scope.Id &&
                  attempt.ExamId == examId &&
                  attempt.Status == ExamAttemptStatus.Finalized &&
                  (!query.Status.HasValue || query.Status.Value ==
                      (dbContext.ExamGradeRevisions.Where(g => g.ExamAttemptId == attempt.Id).OrderByDescending(g => g.RevisionNumber)
                          .Select(g => (ExamGradingQueueStatus?)(g.Status == ExamGradeRevisionStatus.ReadyForRelease ? ExamGradingQueueStatus.ReadyForRelease :
                              g.Status == ExamGradeRevisionStatus.Released ? ExamGradingQueueStatus.Released :
                              g.SupersedesExamGradeRevisionId.HasValue ? ExamGradingQueueStatus.CorrectionDraft : ExamGradingQueueStatus.Draft))
                          .FirstOrDefault() ?? ExamGradingQueueStatus.AwaitingGrading)) &&
                  (!query.ClassId.HasValue || exam.ClassId == query.ClassId.Value) &&
                  (!teacherMembershipId.HasValue || dbContext.TeacherAssignments.Any(assignment =>
                      assignment.OrganizationId == scope.OrganizationId &&
                      assignment.ClassId == exam.ClassId &&
                      assignment.TeacherMembershipId == teacherMembershipId.Value &&
                      assignment.Status == TeacherAssignmentStatus.Active &&
                      assignment.EndedAtUtc == null))
            orderby attempt.FinalizedAtUtc, attempt.Id
            select attempt);
        if (scope.Kind == AcademicClassKind.PersonalClass || query.Page.HasValue || query.PageSize.HasValue)
            attemptsQuery = attemptsQuery.Skip(((query.Page ?? 1) - 1) * (query.PageSize ?? 25)).Take(query.PageSize ?? 25);
        var attempts = await attemptsQuery.AsNoTracking().ToArrayAsync(cancellationToken);
        var results = new List<ExamGradingQueueRecord>(attempts.Length);
        foreach (var attempt in attempts)
        {
            var aggregate = await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
            results.Add(new ExamGradingQueueRecord(
                aggregate.Source,
                aggregate.LatestRevision,
                aggregate.CurrentReleasedRevision));
        }

        return results;
    }

    private async Task<ExamGradeAggregateRecord> LoadAggregateAsync(
        ExamAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var source = await LoadSourceAsync(attempt, asNoTracking, cancellationToken);
        IQueryable<ExamGradeRevision> revisionsQuery = dbContext.ExamGradeRevisions.Where(
            revision => revision.ContextKind == attempt.ContextKind && revision.ContextId == attempt.ContextId &&
                        revision.ExamAttemptId == attempt.Id);
        IQueryable<ExamQuestionGrade> gradesQuery = dbContext.ExamQuestionGrades.Where(
            grade => grade.ContextKind == attempt.ContextKind && grade.ContextId == attempt.ContextId &&
                     grade.ExamAttemptId == attempt.Id);
        IQueryable<ExamGradeRelease> releasesQuery = dbContext.ExamGradeReleases.Where(
            release => release.ContextKind == attempt.ContextKind && release.ContextId == attempt.ContextId &&
                       release.ExamAttemptId == attempt.Id);
        if (asNoTracking)
        {
            revisionsQuery = revisionsQuery.AsNoTracking();
            gradesQuery = gradesQuery.AsNoTracking();
            releasesQuery = releasesQuery.AsNoTracking();
        }

        var revisions = await revisionsQuery
            .OrderBy(revision => revision.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var latestRevision = revisions.LastOrDefault();
        var currentReleased = revisions.SingleOrDefault(
            revision => revision.Status == ExamGradeRevisionStatus.Released);
        var releases = await releasesQuery
            .OrderBy(release => release.ReleasedAtUtc)
            .ToArrayAsync(cancellationToken);
        var currentRelease = currentReleased is null
            ? null
            : releases.SingleOrDefault(release =>
                release.ExamGradeRevisionId == currentReleased.Id);
        var latestGrades = latestRevision is null
            ? []
            : await gradesQuery
                .Where(grade => grade.ExamGradeRevisionId == latestRevision.Id)
                .ToArrayAsync(cancellationToken);
        return new ExamGradeAggregateRecord(
            source,
            revisions,
            latestRevision,
            currentReleased,
            currentRelease,
            latestGrades,
            releases);
    }

    private async Task<ExamGradingSourceRecord> LoadSourceAsync(
        ExamAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Exam> examQuery = dbContext.Exams.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.ExamId);
        IQueryable<ExamVersion> versionQuery = dbContext.ExamVersions.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.ExamVersionId);
        IQueryable<ExamAttemptQuestion> attemptQuestionsQuery = dbContext.ExamAttemptQuestions.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.ExamAttemptId == attempt.Id);
        if (asNoTracking)
        {
            examQuery = examQuery.AsNoTracking();
            versionQuery = versionQuery.AsNoTracking();
            attemptQuestionsQuery = attemptQuestionsQuery.AsNoTracking();
        }

        var exam = await examQuery.SingleAsync(cancellationToken);
        var version = await versionQuery.SingleAsync(cancellationToken);
        var participant = await AcademicContextQuery.Participants(dbContext, attempt.Scope)
            .SingleAsync(item => item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId &&
                item.ClassId == attempt.ClassId && item.ParticipantId == attempt.ParticipantId, cancellationToken);
        var attemptQuestions = await attemptQuestionsQuery
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var questionIds = attemptQuestions.Select(question => question.QuestionVersionId).ToArray();
        var questions = await dbContext.QuestionVersions
            .AsNoTracking()
            .Include(question => question.Options)
            .Where(question => question.ContextKind == attempt.ContextKind && question.ContextId == attempt.ContextId &&
                               questionIds.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, cancellationToken);
        var finalAnswers = await dbContext.ExamFinalAnswers
            .AsNoTracking()
            .Where(answer => answer.ContextKind == attempt.ContextKind && answer.ContextId == attempt.ContextId &&
                             answer.ExamAttemptId == attempt.Id)
            .ToDictionaryAsync(answer => answer.ExamAttemptQuestionId, cancellationToken);
        var revisionIds = finalAnswers.Values.Select(answer => answer.AnswerRevisionId).ToArray();
        var finalRevisions = await dbContext.AnswerRevisions
            .AsNoTracking()
            .Include(revision => revision.Attachments)
            .Where(revision => revision.ContextKind == attempt.ContextKind && revision.ContextId == attempt.ContextId &&
                               revision.ExamAttemptId == attempt.Id &&
                               revisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        var attachments = await ExamAttachmentQuery.LoadAsync(dbContext, revisionIds, cancellationToken);
        var gradingQuestions = attemptQuestions.Select(attemptQuestion =>
        {
            finalAnswers.TryGetValue(attemptQuestion.Id, out var finalAnswer);
            AnswerRevision? finalRevision = null;
            if (finalAnswer is not null)
            {
                finalRevision = finalRevisions[finalAnswer.AnswerRevisionId];
            }

            return new ExamGradingQuestionSourceRecord(
                attemptQuestion,
                questions[attemptQuestion.QuestionVersionId],
                finalAnswer,
                finalRevision,
                finalRevision is not null && attachments.TryGetValue(finalRevision.Id, out var files) ? files : []);
        }).ToArray();
        return new ExamGradingSourceRecord(
            attempt,
            exam,
            version,
            AcademicContextQuery.Participation(participant),
            participant.UserId ?? throw new AssessmentException(AssessmentErrorCodes.ExamAttemptNotFound, "تلاش آزمون یافت نشد."),
            participant.DisplayName,
            gradingQuestions);
    }

    private sealed class EfExamGradingTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
