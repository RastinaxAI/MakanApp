using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfEvaluationStore(MakanDbContext dbContext, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IEvaluationStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfEvaluationTransaction(transaction);
    }

    public async Task<EvaluationAggregateRecord?> GetAggregateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.SubmissionAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && item.Id == attemptId,
                cancellationToken);
        return attempt is null
            ? null
            : await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
    }

    public async Task<EvaluationAggregateRecord?> GetAggregateForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        _ = await dbContext.EvaluationRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[EvaluationRevisions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [SubmissionAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        return await LoadAggregateAsync(attempt, asNoTracking: false, cancellationToken);
    }

    public Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueForManagerAsync(
        AcademicScope scope,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(scope, null, query, cancellationToken);

    public Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueForTeacherAsync(
        AcademicScope scope,
        Guid membershipId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(scope, membershipId, query, cancellationToken);

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
            .Where(relation => scope.Kind == AcademicClassKind.OrganizationClass && relation.OrganizationId == scope.OrganizationId &&
                               relation.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                               relation.Status == GuardianRelationStatus.Active &&
                               relation.ValidFromUtc <= nowUtc &&
                               relation.EndedAtUtc == null)
            .Select(relation => relation.GuardianUserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    public void Add(EvaluationRevision evaluationRevision) =>
        dbContext.EvaluationRevisions.Add(evaluationRevision);

    public void Add(GradeRelease gradeRelease) =>
        dbContext.GradeReleases.Add(gradeRelease);

    public void SetOriginalRowVersion(EvaluationRevision evaluationRevision, byte[] rowVersion) =>
        dbContext.Entry(evaluationRevision).Property(item => item.RowVersion).OriginalValue = rowVersion;

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
                "ارزیابی هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AssessmentException(
                AssessmentErrorCodes.GradeReleaseConflict,
                "ارزیابی یا انتشار هم‌زمان دیگری ثبت شده است.");
        }
    }

    private async Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueAsync(
        AcademicScope scope,
        Guid? teacherMembershipId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken)
    {
        var attemptsQuery = QueueQuery(scope, teacherMembershipId, query);
        if (scope.Kind == AcademicClassKind.PersonalClass || query.Page.HasValue || query.PageSize.HasValue)
            attemptsQuery = attemptsQuery.Skip(((query.Page ?? 1) - 1) * (query.PageSize ?? 25)).Take(query.PageSize ?? 25);
        var attempts = await attemptsQuery.AsNoTracking().ToArrayAsync(cancellationToken);
        var results = new List<EvaluationQueueRecord>(attempts.Length);
        foreach (var attempt in attempts)
        {
            var aggregate = await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
            results.Add(new EvaluationQueueRecord(aggregate.Submission, aggregate.LatestEvaluation, aggregate.CurrentReleasedEvaluation));
        }
        return results;
    }

    public async Task<AcademicPage<EvaluationQueueRecord>> GetQueuePageAsync(AcademicScope scope, Guid? teacherMembershipId,
        EvaluationQueueQuery query, CancellationToken ct)
    {
        query = query with { Page = query.Page ?? 1, PageSize = query.PageSize ?? 25 };
        var total = await QueueQuery(scope, teacherMembershipId, query).CountAsync(ct);
        return new(await GetQueueAsync(scope, teacherMembershipId, query, ct), query.Page.Value, query.PageSize.Value, total);
    }

    private IQueryable<SubmissionAttempt> QueueQuery(AcademicScope scope, Guid? teacherMembershipId, EvaluationQueueQuery query) =>
            from attempt in dbContext.SubmissionAttempts
            join assignment in dbContext.Assignments
                on new { attempt.ContextKind, attempt.ContextId, Id = attempt.AssignmentId }
                equals new { assignment.ContextKind, assignment.ContextId, assignment.Id }
            where attempt.ContextKind == scope.Kind && attempt.ContextId == scope.Id &&
                  attempt.Status == SubmissionAttemptStatus.Submitted &&
                  (!query.AssignmentId.HasValue || attempt.AssignmentId == query.AssignmentId.Value) &&
                  (!query.ClassId.HasValue || assignment.ClassId == query.ClassId.Value) &&
                  (!query.IsLate.HasValue || attempt.IsLate == query.IsLate.Value) &&
                  (!query.ReviewStatus.HasValue || query.ReviewStatus.Value ==
                      (dbContext.EvaluationRevisions.Where(e => e.SubmissionAttemptId == attempt.Id).OrderByDescending(e => e.RevisionNumber)
                          .Select(e => (EvaluationReviewStatus?)(e.Status == EvaluationRevisionStatus.Draft
                              ? e.SupersedesEvaluationRevisionId.HasValue ? EvaluationReviewStatus.CorrectionDraft : EvaluationReviewStatus.Draft
                              : EvaluationReviewStatus.Released)).FirstOrDefault() ?? EvaluationReviewStatus.AwaitingEvaluation)) &&
                  (!teacherMembershipId.HasValue || dbContext.TeacherAssignments.Any(teacher =>
                      teacher.OrganizationId == scope.OrganizationId &&
                      teacher.ClassId == assignment.ClassId &&
                      teacher.TeacherMembershipId == teacherMembershipId.Value &&
                      teacher.Status == TeacherAssignmentStatus.Active &&
                      teacher.EndedAtUtc == null))
            orderby attempt.SubmittedAtUtc, attempt.Id
            select attempt;

    private async Task<EvaluationAggregateRecord> LoadAggregateAsync(
        SubmissionAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var submission = await LoadSubmissionAsync(attempt, asNoTracking, cancellationToken);
        IQueryable<EvaluationRevision> evaluations = dbContext.EvaluationRevisions.Where(
            evaluation => evaluation.ContextKind == attempt.ContextKind && evaluation.ContextId == attempt.ContextId &&
                          evaluation.SubmissionAttemptId == attempt.Id);
        IQueryable<GradeRelease> releases = dbContext.GradeReleases.Where(
            release => release.ContextKind == attempt.ContextKind && release.ContextId == attempt.ContextId &&
                       release.SubmissionAttemptId == attempt.Id);
        if (asNoTracking)
        {
            evaluations = evaluations.AsNoTracking();
            releases = releases.AsNoTracking();
        }

        var allEvaluations = await evaluations
            .OrderByDescending(evaluation => evaluation.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var latestEvaluation = allEvaluations.FirstOrDefault();
        var currentReleasedEvaluation = allEvaluations.SingleOrDefault(
            evaluation => evaluation.Status == EvaluationRevisionStatus.Released);
        GradeRelease? currentGradeRelease = null;
        if (currentReleasedEvaluation is not null)
        {
            currentGradeRelease = await releases.SingleOrDefaultAsync(
                release => release.EvaluationRevisionId == currentReleasedEvaluation.Id,
                cancellationToken);
        }

        return new EvaluationAggregateRecord(
            submission,
            latestEvaluation,
            currentReleasedEvaluation,
            currentGradeRelease);
    }

    private async Task<EvaluationSubmissionRecord> LoadSubmissionAsync(
        SubmissionAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Assignment> assignmentQuery = dbContext.Assignments.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.AssignmentId);
        IQueryable<AssignmentVersion> versionQuery = dbContext.AssignmentVersions.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.AssignmentVersionId);
        if (asNoTracking)
        {
            assignmentQuery = assignmentQuery.AsNoTracking();
            versionQuery = versionQuery.AsNoTracking();
        }

        var assignment = await assignmentQuery.SingleAsync(cancellationToken);
        var version = await versionQuery.SingleAsync(cancellationToken);
        var student = await AcademicContextQuery.Participants(dbContext, attempt.Scope).SingleAsync(p => p.ContextKind == attempt.ContextKind &&
            p.ContextId == attempt.ContextId && p.ParticipantId == attempt.ParticipantId, cancellationToken);
        var attachments = await (
            from attachment in dbContext.SubmissionAttachments
            join fileAsset in dbContext.FileAssets on attachment.FileAssetId equals fileAsset.Id
            where attachment.ContextKind == attempt.ContextKind && attachment.ContextId == attempt.ContextId &&
                  attachment.SubmissionAttemptId == attempt.Id
            orderby attachment.AttachedAtUtc, attachment.Id
            select new SubmissionAttachmentWithFileRecord(attachment, fileAsset))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return new EvaluationSubmissionRecord(
            attempt,
            assignment,
            version,
            AcademicContextQuery.Participation(student),
            student.UserId ?? throw new AssessmentException(AssessmentErrorCodes.EvaluationNotFound, "مالک پاسخ پیدا نشد."),
            student.DisplayName,
            attachments);
    }

    private sealed class EfEvaluationTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
