using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfSubmissionStore(MakanDbContext dbContext, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : ISubmissionStore
{
    public Task<bool> IsFileAvailableForAttemptAsync(Guid fileId, Guid attemptId, CancellationToken ct) =>
        AcademicFileBindingQuery.IsAvailableAsync(dbContext, fileId, null, attemptId, null, ct);
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfSubmissionTransaction(transaction);
    }

    public async Task<SubmissionEligibilityRecord?> GetEligibilityForStudentForUpdateAsync(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await CreateEligibilityQuery(scope, assignmentId, studentUserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var assignment = await dbContext.Assignments
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[Assignments] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {assignmentId}")
            .SingleAsync(cancellationToken);
        var version = await dbContext.AssignmentVersions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AssignmentVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {candidate.Version.Id}")
            .SingleAsync(cancellationToken);
        var recipient = await dbContext.AssignmentRecipients
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AssignmentRecipients] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {candidate.Recipient.Id}")
            .SingleAsync(cancellationToken);
        var participant = await AcademicContextQuery.LockParticipantAsync(dbContext, scope, candidate.Participation.Id, cancellationToken);

        return new SubmissionEligibilityRecord(
            assignment,
            version,
            recipient,
            AcademicContextQuery.Participation(participant),
            studentUserId,
            participant.OrganizationPersonIsActive);
    }

    public Task<SubmissionEligibilityRecord?> GetEligibilityForStudentAsync(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken) =>
        CreateEligibilityQuery(scope, assignmentId, studentUserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Assignment?> GetAssignmentAsync(
        AcademicScope scope,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        dbContext.Assignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                assignment => assignment.ContextKind == scope.Kind && assignment.ContextId == scope.Id &&
                              assignment.Id == assignmentId,
                cancellationToken);

    public async Task<SubmissionAttemptRecord?> GetAttemptForUpdateAsync(
        AcademicScope scope,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        return attempt is null
            ? null
            : await LoadAttemptRecordAsync(attempt, asNoTracking: false, cancellationToken);
    }

    public async Task<SubmissionAttemptRecord?> GetAttemptAsync(
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
            : await LoadAttemptRecordAsync(attempt, asNoTracking: true, cancellationToken);
    }

    public Task<SubmissionAttempt?> GetDraftForUpdateAsync(
        AcademicScope scope,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken) =>
        dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [AssignmentRecipientId] = {assignmentRecipientId} AND [AssignmentVersionId] = {assignmentVersionId} AND [Status] = 1")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountSubmittedAttemptsForUpdateAsync(
        AcademicScope scope,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken) =>
        dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [AssignmentRecipientId] = {assignmentRecipientId} AND [AssignmentVersionId] = {assignmentVersionId} AND [Status] = 2")
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetAttemptsForStudentAsync(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken)
    {
        var attempts = await StudentAttemptsQuery(scope, assignmentId, studentUserId)
            .AsNoTracking().ToArrayAsync(cancellationToken);
        return await LoadAttemptRecordsAsync(attempts, cancellationToken);
    }

    private IQueryable<SubmissionAttempt> StudentAttemptsQuery(AcademicScope scope, Guid assignmentId, Guid studentUserId) => (
            from attempt in dbContext.SubmissionAttempts
            join participant in AcademicContextQuery.Participants(dbContext, scope)
                on new { attempt.ContextKind, attempt.ContextId, attempt.ParticipantId }
                equals new { participant.ContextKind, participant.ContextId, participant.ParticipantId }
            where attempt.ContextKind == scope.Kind && attempt.ContextId == scope.Id &&
                  attempt.AssignmentId == assignmentId &&
                  participant.UserId == studentUserId && (scope.Kind == AcademicClassKind.OrganizationClass || participant.IsActive)
            orderby attempt.AttemptNumber, attempt.Id
            select attempt);

    public async Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetSubmittedAttemptsAsync(
        AcademicScope scope,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var attempts = await SubmittedAttemptsQuery(scope, assignmentId).AsNoTracking().ToArrayAsync(cancellationToken);
        return await LoadAttemptRecordsAsync(attempts, cancellationToken);
    }

    private IQueryable<SubmissionAttempt> SubmittedAttemptsQuery(AcademicScope scope, Guid assignmentId) => dbContext.SubmissionAttempts
            .Where(attempt => attempt.ContextKind == scope.Kind && attempt.ContextId == scope.Id &&
                              attempt.AssignmentId == assignmentId &&
                              attempt.Status == SubmissionAttemptStatus.Submitted)
            .OrderBy(attempt => attempt.SubmittedAtUtc)
            .ThenBy(attempt => attempt.Id);

    public async Task<AcademicPage<SubmissionAttemptRecord>> GetAttemptsPageAsync(AcademicScope scope, Guid assignmentId,
        Guid? studentUserId, int page, int pageSize, CancellationToken ct)
    {
        var query = studentUserId.HasValue ? StudentAttemptsQuery(scope, assignmentId, studentUserId.Value) : SubmittedAttemptsQuery(scope, assignmentId);
        var total = await query.CountAsync(ct);
        var attempts = await query.AsNoTracking().Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(await LoadAttemptRecordsAsync(attempts, ct), page, pageSize, total);
    }

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

    public void Add(SubmissionAttempt attempt) => dbContext.SubmissionAttempts.Add(attempt);
    public void Add(SubmissionAttachment attachment) => dbContext.SubmissionAttachments.Add(attachment);
    public void Remove(SubmissionAttachment attachment) => dbContext.SubmissionAttachments.Remove(attachment);

    public void SetOriginalRowVersion(SubmissionAttempt attempt, byte[] rowVersion) =>
        dbContext.Entry(attempt).Property(item => item.RowVersion).OriginalValue = rowVersion;

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
                "اطلاعات پیش‌نویس هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  (sqlException.Message.Contains("UX_SubmissionAttempts_OneDraft", StringComparison.Ordinal) ||
                   sqlException.Message.Contains("UX_SubmissionAttempts_Recipient_Version_Number", StringComparison.Ordinal)))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "یک تلاش هم‌زمان برای این تکلیف ثبت شده است؛ داده را دوباره دریافت کنید.");
        }
    }

    private IQueryable<SubmissionEligibilityRecord> CreateEligibilityQuery(
        AcademicScope scope,
        Guid assignmentId,
        Guid studentUserId) =>
        from assignment in dbContext.Assignments
        join version in dbContext.AssignmentVersions
            on new
            {
                assignment.ContextKind, assignment.ContextId,
                AssignmentId = assignment.Id,
                VersionNumber = assignment.CurrentVersionNumber
            }
            equals new
            {
                version.ContextKind, version.ContextId,
                version.AssignmentId,
                version.VersionNumber
            }
        join recipient in dbContext.AssignmentRecipients
            on new
            {
                version.ContextKind, version.ContextId,
                AssignmentVersionId = version.Id
            }
            equals new
            {
                recipient.ContextKind, recipient.ContextId,
                recipient.AssignmentVersionId
            }
        join participant in AcademicContextQuery.Participants(dbContext, scope)
            on new { recipient.ContextKind, recipient.ContextId, recipient.ClassId, recipient.ParticipantId }
            equals new { participant.ContextKind, participant.ContextId, participant.ClassId, participant.ParticipantId }
        where assignment.ContextKind == scope.Kind && assignment.ContextId == scope.Id &&
              assignment.Id == assignmentId &&
              participant.UserId == studentUserId && (scope.Kind == AcademicClassKind.OrganizationClass || participant.IsActive)
        select new SubmissionEligibilityRecord(
            assignment,
            version,
            recipient,
            new AcademicParticipationRecord(new AcademicParticipantRef(participant.ContextKind, participant.ParticipantId),
                participant.IsActive, participant.LearnerOrganizationPersonId),
            participant.UserId!.Value,
            participant.OrganizationPersonIsActive);

    private async Task<IReadOnlyCollection<SubmissionAttemptRecord>> LoadAttemptRecordsAsync(
        IEnumerable<SubmissionAttempt> attempts,
        CancellationToken cancellationToken)
    {
        var results = new List<SubmissionAttemptRecord>();
        foreach (var attempt in attempts)
        {
            results.Add(await LoadAttemptRecordAsync(attempt, asNoTracking: true, cancellationToken));
        }

        return results;
    }

    private async Task<SubmissionAttemptRecord> LoadAttemptRecordAsync(
        SubmissionAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var assignmentQuery = dbContext.Assignments.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.AssignmentId);
        var versionQuery = dbContext.AssignmentVersions.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.AssignmentVersionId);
        var recipientQuery = dbContext.AssignmentRecipients.Where(item =>
            item.ContextKind == attempt.ContextKind && item.ContextId == attempt.ContextId && item.Id == attempt.AssignmentRecipientId);
        if (asNoTracking)
        {
            assignmentQuery = assignmentQuery.AsNoTracking();
            versionQuery = versionQuery.AsNoTracking();
            recipientQuery = recipientQuery.AsNoTracking();
        }

        var assignment = await assignmentQuery.SingleAsync(cancellationToken);
        var version = await versionQuery.SingleAsync(cancellationToken);
        var recipient = await recipientQuery.SingleAsync(cancellationToken);
        var participant = await AcademicContextQuery.Participants(dbContext, attempt.Scope).SingleAsync(p =>
            p.ContextKind == attempt.ContextKind && p.ContextId == attempt.ContextId && p.ParticipantId == attempt.ParticipantId, cancellationToken);
        var attachments = await (
            from attachment in dbContext.SubmissionAttachments
            join fileAsset in dbContext.FileAssets on attachment.FileAssetId equals fileAsset.Id
            where attachment.ContextKind == attempt.ContextKind && attachment.ContextId == attempt.ContextId &&
                  attachment.SubmissionAttemptId == attempt.Id
            orderby attachment.AttachedAtUtc, attachment.Id
            select new SubmissionAttachmentWithFileRecord(attachment, fileAsset))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return new SubmissionAttemptRecord(
            attempt,
            assignment,
            version,
            recipient,
            AcademicContextQuery.Participation(participant),
            participant.UserId ?? throw new AssessmentException(AssessmentErrorCodes.SubmissionNotFound, "مالک تلاش پیدا نشد."),
            participant.OrganizationPersonIsActive,
            attachments);
    }

    private sealed class EfSubmissionTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
