using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfSchoolReadStore(MakanDbContext db, TimeProvider clock) : ISchoolReadStore
{
    private IQueryable<Membership> CurrentMembership(AccessContext c) => db.Memberships.Where(m =>
        c.WorkspaceType == WorkspaceType.Organization && m.Id == c.MembershipId && m.UserId == c.UserId &&
        m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
        db.Users.Any(u => u.Id == m.UserId && u.AccountStatus == UserAccountStatus.Active) &&
        db.Organizations.Any(o => o.Id == m.OrganizationId && o.Status == OrganizationStatus.Active) &&
        db.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Role == c.ActiveRole && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null));

    public Task<bool> CanManageOrganizationAsync(AccessContext c, CancellationToken ct) =>
        CurrentMembership(c).Where(_ => c.ActiveRole == OrganizationRole.Manager).AnyAsync(ct);

    public Task<bool> CanManageClassAsync(AccessContext c, Guid classId, CancellationToken ct)
    {
        var membership = CurrentMembership(c);
        return db.Classes.AnyAsync(k => k.Id == classId && k.OrganizationId == c.OrganizationId && k.Status == ClassStatus.Active && k.EndedAtUtc == null &&
            membership.Any() && (c.ActiveRole == OrganizationRole.Manager || c.ActiveRole == OrganizationRole.Teacher &&
                db.TeacherAssignments.Any(t => t.OrganizationId == k.OrganizationId && t.ClassId == k.Id && t.TeacherMembershipId == c.MembershipId &&
                    t.Status == TeacherAssignmentStatus.Active && t.EndedAtUtc == null)), ct);
    }

    public Task<bool> CanStudyClassAsync(AccessContext c, Guid classId, CancellationToken ct)
    {
        var membership = CurrentMembership(c);
        return db.Classes.AnyAsync(k => k.Id == classId && k.OrganizationId == c.OrganizationId && k.Status == ClassStatus.Active && k.EndedAtUtc == null &&
            c.ActiveRole == OrganizationRole.Student && membership.Any() &&
            (from e in db.Enrollments join p in db.OrganizationPersons on e.LearnerOrganizationPersonId equals p.Id
             join u in db.Users on p.PersonId equals u.PersonId
             where e.OrganizationId == k.OrganizationId && e.ClassId == k.Id && e.Status == EnrollmentStatus.Active && e.EndedAtUtc == null &&
                 p.OrganizationId == k.OrganizationId && p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null && u.Id == c.UserId
             select e.Id).Any(), ct);
    }

    public async Task<SchoolPage<SchoolMemberResult>> GetMembersAsync(AccessContext c, SchoolMemberQuery q, DateTime now, CancellationToken ct)
    {
        var query = from m in db.Memberships.AsNoTracking()
                    join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                    join p0 in db.Persons.AsNoTracking() on u.PersonId equals p0.Id into people
                    from p in people.DefaultIfEmpty()
                    where m.OrganizationId == c.OrganizationId && (!q.Status.HasValue || m.Status == q.Status) &&
                        (!q.Role.HasValue || db.RoleAssignments.Any(a => a.MembershipId == m.Id && a.Role == q.Role && a.Status == RoleAssignmentStatus.Active && a.EndedAtUtc == null)) &&
                        (q.Search == null || q.Search == "" || u.Username != null && u.Username.Contains(q.Search) ||
                         p != null && (p.FirstName.Contains(q.Search) || p.LastName.Contains(q.Search) || p.DisplayName != null && p.DisplayName.Contains(q.Search)))
                    select new { Membership = m, User = u, Person = p, Name = p == null ? u.Username ?? "کاربر" : p.DisplayName ?? p.FirstName + " " + p.LastName };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Membership.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Membership.Id).ToArray();
        var roles = await db.RoleAssignments.AsNoTracking().Where(x => ids.Contains(x.MembershipId)).OrderBy(x => x.Role).ThenBy(x => x.Id).ToArrayAsync(ct);
        var personIds = rows.Where(x => x.Person != null).Select(x => x.Person!.Id).ToArray();
        var records = await db.OrganizationPersons.AsNoTracking().Where(x => x.OrganizationId == c.OrganizationId && personIds.Contains(x.PersonId) && x.Status == OrganizationPersonStatus.Active && x.EndedAtUtc == null)
            .OrderBy(x => x.Id).ToArrayAsync(ct);
        var photos = await PhotosAsync(c, rows.Select(x => x.User.Id).ToArray(), ct);
        var items = rows.Select(x => new SchoolMemberResult(x.User.Id, x.User.PersonId,
            records.FirstOrDefault(p => p.PersonId == x.User.PersonId)?.Id, x.Name, x.User.Username, photos.GetValueOrDefault(x.User.Id), x.Membership.Id, x.Membership.Status,
            roles.Where(a => x.Membership.IsActive && a.MembershipId == x.Membership.Id && a.IsActive).Select(a => a.Role).ToArray(),
            roles.Where(a => a.MembershipId == x.Membership.Id).Select(a => new SchoolRoleAssignmentResult(a.Id, a.Role, a.Status, Utc(a.AssignedAtUtc), UtcOrNull(a.EndedAtUtc), Convert.ToBase64String(a.RowVersion))).ToArray(),
            Convert.ToBase64String(x.Membership.RowVersion))).ToArray();
        return new(now, q.Page, q.PageSize, total, items);
    }

    public async Task<SchoolPage<ClassLearnerResult>> GetLearnersAsync(AccessContext c, Guid classId, SchoolPageQuery q, EnrollmentStatus? status, DateTime now, CancellationToken ct)
    {
        var query = from e in db.Enrollments.AsNoTracking()
                    join op in db.OrganizationPersons.AsNoTracking() on e.LearnerOrganizationPersonId equals op.Id
                    join p in db.Persons.AsNoTracking() on op.PersonId equals p.Id
                    join u0 in db.Users.AsNoTracking() on p.Id equals u0.PersonId into users
                    from u in users.DefaultIfEmpty()
                    where e.OrganizationId == c.OrganizationId && op.OrganizationId == c.OrganizationId && e.ClassId == classId &&
                        (!status.HasValue || e.Status == status) && (q.Search == null || q.Search == "" || p.FirstName.Contains(q.Search) || p.LastName.Contains(q.Search) || p.DisplayName != null && p.DisplayName.Contains(q.Search) || u != null && u.Username != null && u.Username.Contains(q.Search))
                    select new { Enrollment = e, Record = op, Person = p, User = u, Name = p.DisplayName ?? p.FirstName + " " + p.LastName };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Enrollment.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        var photos = await PhotosAsync(c, rows.Where(x => x.User != null).Select(x => x.User!.Id).ToArray(), ct);
        return new(now, q.Page, q.PageSize, total, rows.Select(x => new ClassLearnerResult(x.Enrollment.Id, x.Record.Id, x.Person.Id, x.User?.Id,
            x.Name, x.User?.Username, x.User is null ? null : photos.GetValueOrDefault(x.User.Id), x.Enrollment.Status, Utc(x.Enrollment.EnrolledAtUtc), UtcOrNull(x.Enrollment.EndedAtUtc), Convert.ToBase64String(x.Enrollment.RowVersion))).ToArray());
    }

    public async Task<SchoolPage<ClassTeacherResult>> GetTeachersAsync(AccessContext c, Guid classId, SchoolPageQuery q, TeacherAssignmentStatus? status, DateTime now, CancellationToken ct)
    {
        var query = from t in db.TeacherAssignments.AsNoTracking()
                    join m in db.Memberships.AsNoTracking() on t.TeacherMembershipId equals m.Id
                    join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                    join p0 in db.Persons.AsNoTracking() on u.PersonId equals p0.Id into people
                    from p in people.DefaultIfEmpty()
                    where t.OrganizationId == c.OrganizationId && m.OrganizationId == c.OrganizationId && t.ClassId == classId &&
                        (!status.HasValue || t.Status == status) && (q.Search == null || q.Search == "" || u.Username != null && u.Username.Contains(q.Search) ||
                         p != null && (p.FirstName.Contains(q.Search) || p.LastName.Contains(q.Search) || p.DisplayName != null && p.DisplayName.Contains(q.Search)))
                    select new { Assignment = t, User = u, Name = p == null ? u.Username ?? "کاربر" : p.DisplayName ?? p.FirstName + " " + p.LastName };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Assignment.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        var photos = await PhotosAsync(c, rows.Select(x => x.User.Id).ToArray(), ct);
        return new(now, q.Page, q.PageSize, total, rows.Select(x => new ClassTeacherResult(x.Assignment.Id, x.Assignment.TeacherMembershipId, x.User.Id, x.User.PersonId,
            x.Name, x.User.Username, photos.GetValueOrDefault(x.User.Id), x.Assignment.Status, Utc(x.Assignment.AssignedAtUtc), UtcOrNull(x.Assignment.EndedAtUtc), Convert.ToBase64String(x.Assignment.RowVersion))).ToArray());
    }

    public async Task<SchoolPage<CourseResult>> GetCoursesAsync(Guid organizationId, SchoolPageQuery q, CourseStatus? status, DateTime now, CancellationToken ct)
    {
        var query = db.Courses.AsNoTracking().Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status) && (q.Search == null || q.Search == "" || x.Title.Contains(q.Search)));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Title).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new CourseResult(x.Id, x.OrganizationId, x.Title, x.Status)).ToArrayAsync(ct);
        return new(now, q.Page, q.PageSize, total, items);
    }

    public async Task<SchoolPage<AcademicPeriodResult>> GetPeriodsAsync(Guid organizationId, SchoolPageQuery q, AcademicPeriodStatus? status, DateTime now, CancellationToken ct)
    {
        var query = db.AcademicPeriods.AsNoTracking().Where(x => x.OrganizationId == organizationId && (!status.HasValue || x.Status == status) && (q.Search == null || q.Search == "" || x.Title.Contains(q.Search)));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Title).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new AcademicPeriodResult(x.Id, x.OrganizationId, x.Title, x.StartDate, x.EndDate, x.Status)).ToArrayAsync(ct);
        return new(now, q.Page, q.PageSize, total, items);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? UtcOrNull(DateTime? value) => value.HasValue ? Utc(value.Value) : null;

    private async Task<Dictionary<Guid, ProfilePhotoResult>> PhotosAsync(AccessContext c, Guid[] ids, CancellationToken ct)
    {
        // مجوز عکس توسعه نمی‌یابد؛ descriptor فقط برای Avatar قابل کشف طبق سیاست جاری برگردانده می‌شود.
        var visible = MessengerRelationshipQuery.DiscoverableAccounts(db, c.UserId, clock.GetUtcNow().UtcDateTime).Select(x => x.Id);
        return await (from u in db.Users.AsNoTracking() join f in ProfilePhotoQuery.ReadyImages(db) on u.ProfilePhotoFileAssetId equals f.Id
                      where ids.Contains(u.Id) && f.UploadedByUserId == u.Id && (u.Id == c.UserId || visible.Contains(u.Id))
                      select new { u.Id, Photo = new ProfilePhotoResult(f.Id, f.ContentType) }).ToDictionaryAsync(x => x.Id, x => x.Photo, ct);
    }
}
