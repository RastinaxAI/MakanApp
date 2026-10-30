using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Assessment;

public sealed partial class EfAssignmentStore : IAssignmentStore
{
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

    public Task<AcademicClass?> GetClassAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        _dbContext.Classes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                academicClass => academicClass.OrganizationId == organizationId &&
                                 academicClass.Id == classId,
                cancellationToken);

    public Task<AcademicClass?> GetClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        _dbContext.Classes
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {classId}")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        _dbContext.TeacherAssignments.AnyAsync(
            assignment => assignment.OrganizationId == organizationId &&
                          assignment.ClassId == classId &&
                          assignment.TeacherMembershipId == membershipId &&
                          assignment.Status == TeacherAssignmentStatus.Active &&
                          assignment.EndedAtUtc == null,
            cancellationToken);

    public async Task<AssignmentWithVersionRecord?> GetAssignmentForUpdateAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.Assignments
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[Assignments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {assignmentId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (assignment is null)
        {
            return null;
        }

        var version = await _dbContext.AssignmentVersions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AssignmentVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [AssignmentId] = {assignmentId} AND [VersionNumber] = {assignment.CurrentVersionNumber}")
            .SingleAsync(cancellationToken);
        var classData = await _dbContext.Classes
            .Where(academicClass => academicClass.OrganizationId == organizationId &&
                                    academicClass.Id == assignment.ClassId)
            .Select(academicClass => new
            {
                academicClass.Title,
                IsActive = academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null
            })
            .SingleAsync(cancellationToken);
        return new AssignmentWithVersionRecord(
            assignment,
            version,
            classData.Title,
            classData.IsActive);
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveEnrollmentIdsForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        await _dbContext.Enrollments
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ClassId] = {classId} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            .Select(enrollment => enrollment.Id)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetActiveLearnerUserIdsForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        await (
            from enrollment in _dbContext.Enrollments.FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ClassId] = {classId} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            join organizationPerson in _dbContext.OrganizationPersons
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in _dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select user.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);

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
