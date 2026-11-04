using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfSchoolAcademicDiscoveryStore(MakanDbContext db, ISchoolReadStore authority) : ISchoolAcademicDiscoveryStore
{
    public async Task<SchoolPage<TeacherContentActivityDto>> ActivitiesAsync(AccessContext context, TeacherContentActivityQuery query, DateTime now, CancellationToken ct)
    {
        if (!await authority.CanManageOrganizationAsync(context, ct)) throw Denied();
        var organizationId = context.OrganizationId!.Value;
        // خود منبع منتشرشده مرجع گزارش است؛ نبود رویداد تاریخی نباید محتوا را از گزارش حذف کند.
        var lessons = from lesson in db.ClassLessons.AsNoTracking()
            join classroom in db.Classes.AsNoTracking() on lesson.ClassId equals classroom.Id
            join user in db.Users.AsNoTracking() on lesson.CreatedByUserId equals user.Id
            join person in db.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where lesson.OrganizationId == organizationId && classroom.OrganizationId == organizationId && lesson.PublishedAtUtc != null
            select new ContentActivityRow
            {
                EventId = db.AcademicActivityPublications.Where(p => p.OrganizationId == organizationId && p.ClassId == classroom.Id &&
                    p.LessonId == lesson.Id && p.SourceRevision == lesson.RevisionNumber).Select(p => (Guid?)p.Id).FirstOrDefault(),
                TeacherUserId = user.Id, TeacherDisplayName = person == null ? user.Username ?? "نویسنده" : person.DisplayName ?? person.FirstName + " " + person.LastName,
                ClassId = classroom.Id, ClassTitle = classroom.Title, Kind = AcademicActivityKind.Lesson,
                SourceVersionId = lesson.Id, SourceRevision = lesson.RevisionNumber, Title = lesson.Title, PublishedAtUtc = lesson.PublishedAtUtc!.Value
            };
        var assignments = from version in db.AssignmentVersions.AsNoTracking()
            join classroom in db.Classes.AsNoTracking() on version.ClassId equals classroom.Id
            join membership in db.Memberships.AsNoTracking() on version.CreatedByMembershipId equals (Guid?)membership.Id
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in db.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where version.OrganizationId == organizationId && classroom.OrganizationId == organizationId && membership.OrganizationId == organizationId && version.PublishedAtUtc != null
            select new ContentActivityRow
            {
                EventId = db.AcademicActivityPublications.Where(p => p.OrganizationId == organizationId && p.ClassId == classroom.Id &&
                    p.AssignmentVersionId == version.Id && p.SourceRevision == version.VersionNumber).Select(p => (Guid?)p.Id).FirstOrDefault(),
                TeacherUserId = user.Id, TeacherDisplayName = person == null ? user.Username ?? "نویسنده" : person.DisplayName ?? person.FirstName + " " + person.LastName,
                ClassId = classroom.Id, ClassTitle = classroom.Title, Kind = AcademicActivityKind.Assignment,
                SourceVersionId = version.Id, SourceRevision = version.VersionNumber, Title = version.Title, PublishedAtUtc = version.PublishedAtUtc!.Value
            };
        var exams = from version in db.ExamVersions.AsNoTracking()
            join exam in db.Exams.AsNoTracking() on version.ExamId equals exam.Id
            join classroom in db.Classes.AsNoTracking() on exam.ClassId equals classroom.Id
            join membership in db.Memberships.AsNoTracking() on version.CreatedByMembershipId equals (Guid?)membership.Id
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in db.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where version.OrganizationId == organizationId && exam.OrganizationId == organizationId && classroom.OrganizationId == organizationId &&
                membership.OrganizationId == organizationId && version.PublishedAtUtc != null
            select new ContentActivityRow
            {
                EventId = db.AcademicActivityPublications.Where(p => p.OrganizationId == organizationId && p.ClassId == classroom.Id &&
                    p.ExamVersionId == version.Id && p.SourceRevision == version.VersionNumber).Select(p => (Guid?)p.Id).FirstOrDefault(),
                TeacherUserId = user.Id, TeacherDisplayName = person == null ? user.Username ?? "نویسنده" : person.DisplayName ?? person.FirstName + " " + person.LastName,
                ClassId = classroom.Id, ClassTitle = classroom.Title, Kind = AcademicActivityKind.Exam,
                SourceVersionId = version.Id, SourceRevision = version.VersionNumber, Title = version.Title, PublishedAtUtc = version.PublishedAtUtc!.Value
            };
        var source = lessons.Concat(assignments).Concat(exams);
        if (query.TeacherUserId.HasValue) source = source.Where(r => r.TeacherUserId == query.TeacherUserId);
        if (query.ClassId.HasValue) source = source.Where(r => r.ClassId == query.ClassId);
        if (query.Kind.HasValue) source = source.Where(r => r.Kind == query.Kind);
        if (query.FromUtc.HasValue) source = source.Where(r => r.PublishedAtUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) source = source.Where(r => r.PublishedAtUtc < query.ToUtc);
        var count = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(r => r.PublishedAtUtc).ThenBy(r => r.Kind).ThenBy(r => r.SourceVersionId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(now, query.Page, query.PageSize, count, rows.Select(r => new TeacherContentActivityDto(r.EventId,
            r.TeacherUserId, r.TeacherDisplayName, r.ClassId, r.ClassTitle, r.Kind, r.SourceVersionId,
            r.SourceRevision, r.Title, DateTime.SpecifyKind(r.PublishedAtUtc, DateTimeKind.Utc))).ToArray());
    }
    public async Task<AcademicPage<ExamSummaryRecord>> ExamsAsync(AccessContext context, OrganizationExamQuery query, CancellationToken ct)
    {
        var organizationId = context.OrganizationId!.Value;
        if (context.ActiveRole == OrganizationRole.Manager && !await authority.CanManageOrganizationAsync(context, ct)) throw Denied();
        // همان semantics فهرست فعلی: مدرس با انتساب جاری می‌تواند Draftهای کلاس مجاز را ببیند، بدون Editor/AnswerKey.
        var source = from exam in db.Exams.AsNoTracking()
            join version in db.ExamVersions.AsNoTracking() on new { ExamId = exam.Id, Number = exam.CurrentVersionNumber }
                equals new { version.ExamId, Number = version.VersionNumber }
            join classroom in db.Classes.AsNoTracking() on exam.ClassId equals classroom.Id
            where exam.ContextKind == AcademicClassKind.OrganizationClass && exam.OrganizationId == organizationId &&
                classroom.OrganizationId == organizationId && version.OrganizationId == organizationId &&
                (context.ActiveRole == OrganizationRole.Manager || db.TeacherAssignments.Any(t => t.OrganizationId == organizationId && t.ClassId == classroom.Id &&
                    t.TeacherMembershipId == context.MembershipId && t.Status == TeacherAssignmentStatus.Active && t.EndedAtUtc == null &&
                    db.Memberships.Any(m => m.Id == t.TeacherMembershipId && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
                        db.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Role == OrganizationRole.Teacher && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null)))) &&
                (!query.ClassId.HasValue || exam.ClassId == query.ClassId) && (!query.Status.HasValue || version.Status == query.Status) &&
                (query.Search == null || query.Search == "" || version.Title.Contains(query.Search))
            select new { Exam = exam, Version = version, classroom.Title };
        var count = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(r => r.Version.UpdatedAtUtc).ThenBy(r => r.Exam.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(rows.Select(r => new ExamSummaryRecord(r.Exam, r.Version, r.Title)).ToArray(), query.Page, query.PageSize, count);
    }
    private static OrganizationException Denied() => new(OrganizationErrorCodes.WorkspaceNotAllowed, "مجوز جاری سازمان لازم است.");
    private sealed class ContentActivityRow
    {
        public Guid? EventId { get; init; }
        public Guid TeacherUserId { get; init; }
        public string TeacherDisplayName { get; init; } = string.Empty;
        public Guid ClassId { get; init; }
        public string ClassTitle { get; init; } = string.Empty;
        public AcademicActivityKind Kind { get; init; }
        public Guid SourceVersionId { get; init; }
        public int SourceRevision { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime PublishedAtUtc { get; init; }
    }
}
