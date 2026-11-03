using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfPersonalAcademicReadStore(MakanDbContext db) : IPersonalAcademicReadStore
{
    public async Task<AcademicPage<AssignmentWithVersionRecord>> AssignmentsAsync(AcademicAccessContext access, AcademicListQuery query, CancellationToken ct)
    {
        var staff = access.Has(PersonalClassAcademicCapabilities.AuthorAssignments) || access.Has(PersonalClassAcademicCapabilities.Grade);
        var search = query.Search?.Trim();
        var source = from a in db.Assignments.AsNoTracking()
            join v in db.AssignmentVersions on new { AssignmentId = a.Id, Number = a.CurrentVersionNumber } equals new { v.AssignmentId, Number = v.VersionNumber }
            join c in db.PersonalClasses on a.PersonalClassId equals c.Id
            where a.ContextKind == AcademicClassKind.PersonalClass && a.PersonalClassId == access.ClassId &&
                (staff || (a.Status == AssignmentStatus.Published || a.Status == AssignmentStatus.Closed) &&
                    db.AssignmentRecipients.Any(r => r.AssignmentVersionId == v.Id && r.PersonalClassMembershipId == access.PersonalMembershipId)) &&
                (search == null || search == "" || v.Title.Contains(search)) &&
                (!query.Published.HasValue || v.PublishedAtUtc.HasValue == query.Published.Value)
            select new { Assignment = a, Version = v, c.Title, c.State };
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(r => r.Assignment.UpdatedAtUtc).ThenBy(r => r.Assignment.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(rows.Select(r => new AssignmentWithVersionRecord(r.Assignment, r.Version, r.Title, r.State == PersonalClassState.Active)).ToArray(), query.Page, query.PageSize, total);
    }

    public async Task<AcademicPage<ExamSummaryRecord>> ExamsAsync(AcademicAccessContext access, AcademicListQuery query, CancellationToken ct)
    {
        var staff = access.Has(PersonalClassAcademicCapabilities.AuthorExams) || access.Has(PersonalClassAcademicCapabilities.Grade);
        var search = query.Search?.Trim();
        var source = from e in db.Exams.AsNoTracking()
            join v in db.ExamVersions on new { ExamId = e.Id, Number = staff ? (int?)e.CurrentVersionNumber : e.LatestPublishedVersionNumber }
                equals new { v.ExamId, Number = (int?)v.VersionNumber }
            join c in db.PersonalClasses on e.PersonalClassId equals c.Id
            where e.ContextKind == AcademicClassKind.PersonalClass && e.PersonalClassId == access.ClassId &&
                (staff || v.Status == ExamVersionStatus.Published) &&
                (search == null || search == "" || v.Title.Contains(search)) &&
                (!query.Published.HasValue || v.PublishedAtUtc.HasValue == query.Published.Value)
            select new { Exam = e, Version = v, c.Title };
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(r => r.Exam.CreatedAtUtc).ThenBy(r => r.Exam.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(rows.Select(r => new ExamSummaryRecord(r.Exam, r.Version, r.Title)).ToArray(), query.Page, query.PageSize, total);
    }
}
