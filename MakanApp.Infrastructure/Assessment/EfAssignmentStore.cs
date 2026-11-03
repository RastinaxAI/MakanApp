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

public sealed partial class EfAssignmentStore : IAssignmentStore
{
    public Task<bool> IsFileAvailableForAssignmentAsync(Guid fileId, Guid assignmentId, CancellationToken ct) =>
        AcademicFileBindingQuery.IsAvailableAsync(_dbContext, fileId, assignmentId, null, null, ct);
    public Task<int> CountRecipientsAsync(Guid versionId, CancellationToken ct) =>
        _dbContext.AssignmentRecipients.CountAsync(r => r.AssignmentVersionId == versionId, ct);
    private readonly MakanDbContext _dbContext;
    private readonly MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter _realtime;

    public EfAssignmentStore(MakanDbContext dbContext, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime)
    {
        _dbContext = dbContext;
        _realtime = realtime;
    }

    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfAssessmentTransaction(transaction);
    }

    public Task<AcademicClassRecord?> GetClassAsync(AcademicScope scope, Guid classId, CancellationToken ct) =>
        AcademicContextQuery.ClassAsync(_dbContext, scope, classId, false, ct);

    public Task<AcademicClassRecord?> GetClassForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken ct) =>
        AcademicContextQuery.ClassAsync(_dbContext, scope, classId, true, ct);

    public Task<bool> HasActiveTeacherAssignmentAsync(AcademicScope scope, Guid classId, Guid membershipId, CancellationToken ct) =>
        _dbContext.TeacherAssignments.AnyAsync(a => a.OrganizationId == scope.OrganizationId && a.ClassId == classId &&
            a.TeacherMembershipId == membershipId && a.Status == TeacherAssignmentStatus.Active && a.EndedAtUtc == null, ct);

    public async Task<AssignmentWithVersionRecord?> GetAssignmentForUpdateAsync(AcademicScope scope, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await _dbContext.Assignments.FromSqlInterpolated(
            $"SELECT * FROM [assessment].[Assignments] WITH (UPDLOCK,HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {assignmentId}").SingleOrDefaultAsync(ct);
        if (assignment is null) return null;
        var version = await _dbContext.AssignmentVersions.FromSqlInterpolated(
            $"SELECT * FROM [assessment].[AssignmentVersions] WITH (UPDLOCK,HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [AssignmentId] = {assignmentId} AND [VersionNumber] = {assignment.CurrentVersionNumber}").SingleAsync(ct);
        var classroom = await AcademicContextQuery.ClassAsync(_dbContext, scope, assignment.ClassId, false, ct)
            ?? throw new AssessmentException(AssessmentErrorCodes.AssignmentNotFound, "کلاس تکلیف پیدا نشد.");
        return new(assignment, version, classroom.Title, classroom.IsActive);
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveParticipantIdsForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken ct)
    {
        if (scope.Kind == AcademicClassKind.OrganizationClass)
            return await _dbContext.Enrollments.FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId] = {scope.Id} AND [ClassId] = {classId} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
                .Select(e => e.Id).ToArrayAsync(ct);
        return await _dbContext.PersonalClassParticipants.FromSqlInterpolated(
            $"SELECT * FROM [academic].[PersonalClassParticipants] WITH (UPDLOCK,HOLDLOCK) WHERE [PersonalClassId] = {scope.Id} AND [PersonalClassId] = {classId} AND [EndedAtUtc] IS NULL AND ([LocalRoles] & 1) = 1")
            .Select(m => m.Id).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveLearnerUserIdsForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken ct) =>
        await AcademicContextQuery.Participants(_dbContext, scope).Where(p => p.ContextKind == scope.Kind && p.ContextId == scope.Id &&
            p.ClassId == classId && p.IsActive && p.UserId.HasValue).Select(p => p.UserId!.Value).Distinct().ToArrayAsync(ct);

    public void SetOriginalRowVersion(Assignment assignment, byte[] rowVersion) =>
        _dbContext.Entry(assignment).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(AssignmentVersion version, byte[] rowVersion) =>
        _dbContext.Entry(version).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void Add(Assignment assignment) => _dbContext.Assignments.Add(assignment);
    public void Add(AssignmentVersion version) => _dbContext.AssignmentVersions.Add(version);
    public void AddRange(IEnumerable<AssignmentRecipient> recipients) =>
        _dbContext.AssignmentRecipients.AddRange(recipients);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _realtime.CaptureAssessmentAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "اطلاعات هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_AssignmentRecipients_Organization_Version_Enrollment",
                      StringComparison.Ordinal))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentAlreadyPublished,
                "گیرندگان این نسخه قبلاً ثبت شده‌اند.");
        }
    }

    private sealed class EfAssessmentTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
