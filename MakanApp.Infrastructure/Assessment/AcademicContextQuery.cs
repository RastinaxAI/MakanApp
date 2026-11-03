using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

internal static class AcademicContextQuery
{
    internal sealed class ClassRow
    {
        public AcademicClassKind ContextKind { get; init; }
        public Guid ContextId { get; init; }
        public Guid ClassId { get; init; }
        public string Title { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }
    internal sealed class ParticipantRow
    {
        public AcademicClassKind ContextKind { get; init; }
        public Guid ContextId { get; init; }
        public Guid ClassId { get; init; }
        public Guid ParticipantId { get; init; }
        public Guid? UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool OrganizationPersonIsActive { get; init; }
        public Guid? LearnerOrganizationPersonId { get; init; }
    }

    internal static IQueryable<ClassRow> Classes(MakanDbContext db, AcademicScope scope) =>
        scope.Kind == AcademicClassKind.OrganizationClass
        ? db.Classes.Where(c => c.OrganizationId == scope.Id).Select(c => new ClassRow { ContextKind = AcademicClassKind.OrganizationClass, ContextId = c.OrganizationId,
            ClassId = c.Id, Title = c.Title, IsActive = c.Status == ClassStatus.Active && c.EndedAtUtc == null })
        : db.PersonalClasses.Where(c => c.Id == scope.Id).Select(c => new ClassRow { ContextKind = AcademicClassKind.PersonalClass, ContextId = c.Id,
            ClassId = c.Id, Title = c.Title, IsActive = c.State == PersonalClassState.Active });

    internal static IQueryable<ParticipantRow> Participants(MakanDbContext db, AcademicScope scope) =>
        scope.Kind == AcademicClassKind.OrganizationClass
        ? (from e in db.Enrollments
         join p in db.OrganizationPersons on e.LearnerOrganizationPersonId equals p.Id
         join user in db.Users on p.PersonId equals user.PersonId into users
         from u in users.DefaultIfEmpty()
         join person in db.Persons on p.PersonId equals person.Id
         where e.OrganizationId == scope.Id && e.OrganizationId == p.OrganizationId
         select new ParticipantRow { ContextKind = AcademicClassKind.OrganizationClass, ContextId = e.OrganizationId,
             ClassId = e.ClassId, ParticipantId = e.Id, UserId = (Guid?)u.Id,
             DisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName,
             IsActive = e.Status == EnrollmentStatus.Active && e.EndedAtUtc == null && p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null,
             OrganizationPersonIsActive = p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null,
             LearnerOrganizationPersonId = (Guid?)p.Id })
        : (from m in db.PersonalClassParticipants
                join u in db.Users on m.StudentUserId equals u.Id
                join profile in db.Persons on u.PersonId equals profile.Id into profiles
                from person in profiles.DefaultIfEmpty()
                where m.PersonalClassId == scope.Id
                select new ParticipantRow { ContextKind = AcademicClassKind.PersonalClass, ContextId = m.PersonalClassId,
                    ClassId = m.PersonalClassId, ParticipantId = m.Id, UserId = u.Id,
                    DisplayName = person == null ? "هنرجو" : person.DisplayName ?? person.FirstName + " " + person.LastName,
                    IsActive = m.EndedAtUtc == null && (m.LocalRoles & PersonalClassLocalRoles.Learner) != 0 && u.AccountStatus == UserAccountStatus.Active,
                    OrganizationPersonIsActive = m.EndedAtUtc == null && (m.LocalRoles & PersonalClassLocalRoles.Learner) != 0,
                    LearnerOrganizationPersonId = null });

    internal static async Task<AcademicClassRecord?> ClassAsync(MakanDbContext db, AcademicScope scope, Guid classId,
        bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            if (scope.Kind == AcademicClassKind.PersonalClass)
                _ = await db.PersonalClasses.FromSqlInterpolated($"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {classId} AND [Id] = {scope.Id}").SingleOrDefaultAsync(ct);
            else
                _ = await db.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId] = {scope.Id} AND [Id] = {classId}").SingleOrDefaultAsync(ct);
        }
        var row = await Classes(db, scope).SingleOrDefaultAsync(c => c.ContextKind == scope.Kind && c.ContextId == scope.Id && c.ClassId == classId, ct);
        return row is null ? null : new(new(scope, classId), row.Title, row.IsActive);
    }

    internal static AcademicParticipationRecord Participation(ParticipantRow row) =>
        new(new(row.ContextKind, row.ParticipantId), row.IsActive, row.LearnerOrganizationPersonId);

    internal static async Task<ParticipantRow> LockParticipantAsync(MakanDbContext db, AcademicScope scope, Guid id, CancellationToken ct)
    {
        if (scope.Kind == AcademicClassKind.OrganizationClass)
        {
            var enrollment = await db.Enrollments.FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId] = {scope.Id} AND [Id] = {id}").SingleAsync(ct);
            _ = await db.OrganizationPersons.FromSqlInterpolated(
                $"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId] = {scope.Id} AND [Id] = {enrollment.LearnerOrganizationPersonId}").SingleAsync(ct);
        }
        else
            _ = await db.PersonalClassParticipants.FromSqlInterpolated(
                $"SELECT * FROM [academic].[PersonalClassParticipants] WITH (UPDLOCK,HOLDLOCK) WHERE [PersonalClassId] = {scope.Id} AND [Id] = {id}").SingleAsync(ct);
        return await Participants(db, scope).SingleAsync(p => p.ContextKind == scope.Kind && p.ContextId == scope.Id && p.ParticipantId == id, ct);
    }
}
