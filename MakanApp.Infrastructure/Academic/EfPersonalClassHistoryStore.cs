using MakanApp.Application.Academic;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfPersonalClassHistoryStore(MakanDbContext db, IPlatformPermissionResolver permissions) : IPersonalClassHistoryStore
{
    public async Task<PersonalClassPage<PersonalClassHistoryItem>> ReadAsync(PersonalClassActor actor, Guid classId,
        bool platform, int page, int pageSize, CancellationToken ct)
    {
        var classroom = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classId, ct)
            ?? throw EfPersonalClassFormationStore.NotFound();
        if (!platform && (classroom.ManagerUserId != actor.UserId || !await db.PersonalClassParticipants.AnyAsync(m =>
            m.PersonalClassId == classId && m.StudentUserId == actor.UserId && m.EndedAtUtc == null &&
            (m.LocalRoles & PersonalClassLocalRoles.Manager) != 0, ct))) throw EfPersonalClassFormationStore.NotFound();
        if (platform && !await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesRead, ct))
            throw EfPersonalClassFormationStore.NotFound();
        var canGrade = !platform && await PersonalClassAcademicAccessQuery.Effective(db, classId)
            .AnyAsync(g => (g.Capabilities & PersonalClassAcademicCapabilities.Grade) != 0 &&
                db.PersonalClassParticipants.Any(m => m.Id == g.MembershipId && m.StudentUserId == actor.UserId), ct);
        var canReadGrants = !platform || await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesAcademicPermissions, ct);
        var id = classId.ToString(); var requestId = classroom.FormationRequestId.ToString();
        // مدیر محلی review پلتفرم را نمی‌بیند؛ اختیار Platform نیز مجوز نتیجهٔ خصوصی ایجاد نمی‌کند.
        var query = db.AdminAuditLogs.AsNoTracking().Where(a => a.Result == AdminAuditResult.Succeeded &&
            (a.TargetType == "PersonalClass" && a.TargetId == id ||
             platform && a.TargetType == "PersonalClassRequest" && a.TargetId == requestId ||
             canReadGrants && a.TargetType == "PersonalClassAcademicGrant" && db.PersonalClassAcademicGrants.Any(g => g.PersonalClassId == classId && g.Id.ToString() == a.TargetId) ||
             a.TargetType == "PersonalClassAssignment" && db.Assignments.Any(x => x.PersonalClassId == classId && x.Id.ToString() == a.TargetId) ||
             a.TargetType == "PersonalClassExam" && db.Exams.Any(x => x.PersonalClassId == classId && x.Id.ToString() == a.TargetId) ||
             canGrade && a.TargetType == "PersonalClassSubmission" && db.SubmissionAttempts.Any(x => x.PersonalClassId == classId && x.Id.ToString() == a.TargetId) ||
             canGrade && a.TargetType == "PersonalClassExamAttempt" && db.ExamAttempts.Any(x => x.PersonalClassId == classId && x.Id.ToString() == a.TargetId)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.OccurredAtUtc).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(a => new
                { a.Id, a.Action, a.TargetType, a.TargetId, a.OccurredAtUtc, ActorUserId = platform ? a.ActorUserId : null, CorrelationId = platform ? a.CorrelationId : null }).ToArrayAsync(ct);
        // Reason و Before/AfterSummary عمداً جزو این projection نیستند.
        return new(rows.Select(a => new PersonalClassHistoryItem(a.Id, a.Action, a.TargetType,
            Guid.TryParse(a.TargetId, out var resource) ? resource : null, a.OccurredAtUtc, a.ActorUserId, a.CorrelationId)).ToArray(), page, pageSize, total);
    }
}
