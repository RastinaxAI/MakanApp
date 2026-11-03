using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

public sealed partial class EfAssignmentStore
{
    public Task<AssignmentWithVersionRecord?> GetAssignmentAsync(
        AcademicScope scope,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        (
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions
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
            join academicClass in AcademicContextQuery.Classes(_dbContext, scope)
                on new { assignment.ContextKind, assignment.ContextId, assignment.ClassId }
                equals new { academicClass.ContextKind, academicClass.ContextId, academicClass.ClassId }
            where assignment.ContextKind == scope.Kind && assignment.ContextId == scope.Id &&
                  assignment.Id == assignmentId
            select new AssignmentWithVersionRecord(
                assignment,
                version,
                academicClass.Title,
                academicClass.IsActive))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsRecipientForStudentAsync(
        AcademicScope scope,
        Guid assignmentVersionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from recipient in _dbContext.AssignmentRecipients
            join participant in AcademicContextQuery.Participants(_dbContext, scope)
                on new { recipient.ContextKind, recipient.ContextId, recipient.ClassId, recipient.ParticipantId }
                equals new { participant.ContextKind, participant.ContextId, participant.ClassId, participant.ParticipantId }
            where recipient.ContextKind == scope.Kind && recipient.ContextId == scope.Id &&
                  recipient.AssignmentVersionId == assignmentVersionId &&
                  participant.UserId == userId && participant.OrganizationPersonIsActive &&
                  (scope.Kind == AcademicClassKind.OrganizationClass || participant.IsActive)
            select recipient.Id)
            .AnyAsync(cancellationToken);

    public Task<bool> IsRecipientForLearnerAsync(
        AcademicScope scope,
        Guid assignmentVersionId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        (
            from recipient in _dbContext.AssignmentRecipients
            join enrollment in _dbContext.Enrollments
                on new
                {
                    recipient.OrganizationId,
                    recipient.ClassId,
                    Id = recipient.EnrollmentId
                }
                equals new { OrganizationId = (Guid?)enrollment.OrganizationId, enrollment.ClassId, Id = (Guid?)enrollment.Id }
            where scope.Kind == AcademicClassKind.OrganizationClass && recipient.OrganizationId == scope.OrganizationId &&
                  recipient.AssignmentVersionId == assignmentVersionId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId
            select recipient.Id)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForManagerAsync(
        AcademicScope scope,
        Guid classId,
        CancellationToken cancellationToken) =>
        await CreateClassQuery(scope, classId)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForTeacherAsync(
        AcademicScope scope,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        if (!await HasActiveTeacherAssignmentAsync(
                scope,
                classId,
                membershipId,
                cancellationToken))
        {
            return [];
        }

        return await CreateClassQuery(scope, classId)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForStudentAsync(
        AcademicScope scope,
        Guid classId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions
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
            join academicClass in AcademicContextQuery.Classes(_dbContext, scope)
                on new { assignment.ContextKind, assignment.ContextId, assignment.ClassId }
                equals new { academicClass.ContextKind, academicClass.ContextId, academicClass.ClassId }
            join recipient in _dbContext.AssignmentRecipients
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
            join participant in AcademicContextQuery.Participants(_dbContext, scope)
                on new { recipient.ContextKind, recipient.ContextId, recipient.ClassId, recipient.ParticipantId }
                equals new { participant.ContextKind, participant.ContextId, participant.ClassId, participant.ParticipantId }
            where assignment.ContextKind == scope.Kind && assignment.ContextId == scope.Id &&
                  assignment.ClassId == classId &&
                  (assignment.Status == AssignmentStatus.Published ||
                   assignment.Status == AssignmentStatus.Closed) &&
                  participant.UserId == userId && participant.OrganizationPersonIsActive &&
                  (scope.Kind == AcademicClassKind.OrganizationClass || participant.IsActive)
            orderby version.DueAtUtc, assignment.Id
            select new AssignmentWithVersionRecord(
                assignment,
                version,
                academicClass.Title,
                academicClass.IsActive))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForLearnerAsync(
        AcademicScope scope,
        Guid classId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        await (
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions
                on new
                {
                    assignment.OrganizationId,
                    AssignmentId = assignment.Id,
                    VersionNumber = assignment.CurrentVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.AssignmentId,
                    version.VersionNumber
                }
            join academicClass in _dbContext.Classes
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { OrganizationId = (Guid?)academicClass.OrganizationId, academicClass.Id }
            join recipient in _dbContext.AssignmentRecipients
                on new
                {
                    version.OrganizationId,
                    AssignmentVersionId = version.Id
                }
                equals new
                {
                    recipient.OrganizationId,
                    recipient.AssignmentVersionId
                }
            join enrollment in _dbContext.Enrollments
                on new
                {
                    recipient.OrganizationId,
                    recipient.ClassId,
                    Id = recipient.EnrollmentId
                }
                equals new { OrganizationId = (Guid?)enrollment.OrganizationId, enrollment.ClassId, Id = (Guid?)enrollment.Id }
            where scope.Kind == AcademicClassKind.OrganizationClass && assignment.OrganizationId == scope.OrganizationId &&
                  assignment.ClassId == classId &&
                  (assignment.Status == AssignmentStatus.Published ||
                   assignment.Status == AssignmentStatus.Closed) &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId
            orderby version.DueAtUtc, assignment.Id
            select new AssignmentWithVersionRecord(
                assignment,
                version,
                academicClass.Title,
                academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    private IQueryable<AssignmentWithVersionRecord> CreateClassQuery(
        AcademicScope scope,
        Guid classId) =>
        from assignment in _dbContext.Assignments
        join version in _dbContext.AssignmentVersions
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
        join academicClass in AcademicContextQuery.Classes(_dbContext, scope)
            on new { assignment.ContextKind, assignment.ContextId, assignment.ClassId }
            equals new { academicClass.ContextKind, academicClass.ContextId, academicClass.ClassId }
        where assignment.ContextKind == scope.Kind && assignment.ContextId == scope.Id && assignment.ClassId == classId
        orderby assignment.UpdatedAtUtc descending, assignment.Id
        select new AssignmentWithVersionRecord(
            assignment,
            version,
            academicClass.Title,
            academicClass.IsActive);
}
