using MakanApp.Application.Ai;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class EfAiScopeStore(MakanDbContext dbContext) : IAiScopeStore
{
    public Task<Guid?> GetActiveLearnerOrganizationPersonIdAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from organizationPerson in dbContext.OrganizationPersons
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.OrganizationId == organizationId &&
                  user.Id == userId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select (Guid?)organizationPerson.Id)
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AiSelectedHomework?> GetPublishedHomeworkAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        (
            from recipient in dbContext.AssignmentRecipients
            join enrollment in dbContext.Enrollments
                on new { recipient.OrganizationId, Id = recipient.EnrollmentId }
                equals new { enrollment.OrganizationId, enrollment.Id }
            join assignment in dbContext.Assignments
                on new { recipient.OrganizationId, Id = recipient.AssignmentId }
                equals new { assignment.OrganizationId, assignment.Id }
            join version in dbContext.AssignmentVersions
                on new { recipient.OrganizationId, Id = recipient.AssignmentVersionId }
                equals new { version.OrganizationId, version.Id }
            where recipient.OrganizationId == organizationId &&
                  recipient.AssignmentId == assignmentId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  dbContext.Classes.Any(c => c.Id == assignment.ClassId && c.OrganizationId == organizationId &&
                      c.Status == ClassStatus.Active && c.EndedAtUtc == null) &&
                  dbContext.OrganizationPersons.Any(p => p.Id == learnerOrganizationPersonId && p.OrganizationId == organizationId &&
                      p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null) &&
                  assignment.CurrentVersionNumber == version.VersionNumber &&
                  (assignment.Status == AssignmentStatus.Published ||
                   assignment.Status == AssignmentStatus.Closed) &&
                  version.PublishedAtUtc != null
            select new AiSelectedHomework(
                assignment.Id,
                assignment.ClassId,
                version.Title,
                version.Description,
                version.DueAtUtc,
                version.MaxScore))
        .AsNoTracking()
        .FirstOrDefaultAsync(cancellationToken);

    public Task<AiSelectedExam?> GetPublishedExamAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        Guid examId,
        CancellationToken cancellationToken) =>
        (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.LatestPublishedVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    VersionNumber = (int?)version.VersionNumber
                }
            join enrollment in dbContext.Enrollments
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            where exam.OrganizationId == organizationId &&
                  exam.Id == examId &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null
                  && dbContext.Classes.Any(c => c.Id == exam.ClassId && c.OrganizationId == organizationId &&
                      c.Status == ClassStatus.Active && c.EndedAtUtc == null)
                  && dbContext.OrganizationPersons.Any(p => p.Id == learnerOrganizationPersonId && p.OrganizationId == organizationId &&
                      p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null)
            select new AiSelectedExam(
                exam.Id,
                exam.ClassId,
                version.Title,
                version.Description,
                version.AvailableFromUtc,
                version.AvailableUntilUtc,
                version.DurationMinutes,
                version.MaxScore))
        .AsNoTracking()
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AiLearnerClassScope?> GetActiveLearnerClassAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        Guid classId,
        CancellationToken cancellationToken) =>
        (
            from enrollment in dbContext.Enrollments
            join academicClass in dbContext.Classes
                on new { enrollment.OrganizationId, Id = enrollment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join course in dbContext.Courses
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            where enrollment.OrganizationId == organizationId &&
                  enrollment.ClassId == classId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null &&
                  dbContext.OrganizationPersons.Any(p => p.Id == learnerOrganizationPersonId && p.OrganizationId == organizationId &&
                      p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null)
            select new AiLearnerClassScope(
                academicClass.Id,
                academicClass.Title,
                course.Title))
        .AsNoTracking()
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AiLearnerClassScope>> GetActiveLearnerClassesAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        int maximumCount,
        CancellationToken cancellationToken) =>
        await (
            from enrollment in dbContext.Enrollments
            join academicClass in dbContext.Classes
                on new { enrollment.OrganizationId, Id = enrollment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join course in dbContext.Courses
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            where enrollment.OrganizationId == organizationId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null &&
                  dbContext.OrganizationPersons.Any(p => p.Id == learnerOrganizationPersonId && p.OrganizationId == organizationId &&
                      p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null)
            orderby academicClass.Title, academicClass.Id
            select new AiLearnerClassScope(
                academicClass.Id,
                academicClass.Title,
                course.Title))
        .AsNoTracking()
        .Take(maximumCount)
        .ToArrayAsync(cancellationToken);
}
