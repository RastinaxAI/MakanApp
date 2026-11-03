using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

public sealed class ExamDeadlineFinalizer(MakanDbContext db, IExamFinalizationStore finalization, TimeProvider clock)
    : IExamDeadlineFinalizer
{
    public async Task FinalizeDueForExamAsync(Guid examId, Guid learnerUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.ExamAttempts.AsNoTracking().Where(a => a.ExamId == examId &&
                a.ContextKind == AcademicClassKind.PersonalClass && a.Status == ExamAttemptStatus.InProgress && a.EffectiveDeadlineUtc <= now &&
                db.PersonalClassParticipants.Any(m => m.Id == a.PersonalClassMembershipId && m.PersonalClassId == a.PersonalClassId &&
                    m.StudentUserId == learnerUserId && m.EndedAtUtc == null && (m.LocalRoles & PersonalClassLocalRoles.Learner) != 0))
            .OrderBy(a => a.EffectiveDeadlineUtc).ThenBy(a => a.Id).Select(a => a.Id).ToArrayAsync(ct);
        foreach (var id in ids) await FinalizeDueAttemptAsync(id, ct);
    }

    public async Task<bool> FinalizeDueAttemptAsync(Guid attemptId, CancellationToken ct)
    {
        var candidate = await (from a in db.ExamAttempts.AsNoTracking()
            join c in db.PersonalClasses on a.PersonalClassId equals c.Id
            where a.Id == attemptId && a.ContextKind == AcademicClassKind.PersonalClass
            select new { ClassId = c.Id, c.HostUserId }).SingleOrDefaultAsync(ct);
        if (candidate is null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={candidate.HostUserId}")
            .AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        var classroom = await db.PersonalClasses.FromSqlInterpolated(
            $"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={candidate.ClassId}").AsNoTracking().SingleAsync(ct);
        // پایان تلاشِ کلاس بایگانی/معلق تا تصمیم محصول تغییر نمی‌کند؛ پاسخ پذیرفته‌شده حفظ می‌شود.
        if (classroom.State != PersonalClassState.Active) return false;
        var attempt = await db.ExamAttempts.FromSqlInterpolated(
            $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={attemptId}").SingleAsync(ct);
        // ممکن است همین DbContext پیش از گرفتن قفل، تلاش را خوانده باشد.
        await db.Entry(attempt).ReloadAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (!attempt.IsInProgress || now < attempt.EffectiveDeadlineUtc) return false;
        var questions = await finalization.GetQuestionsForFinalizationAsync(attempt.Scope, attempt.Id, ct);
        var answers = questions.Where(q => q.CurrentRevision is not null)
            .Select(q => ExamFinalAnswer.Create(attempt, q.AttemptQuestion, q.CurrentRevision!)).ToArray();
        // همان revision ذخیره‌شده قفل می‌شود؛ بی‌پاسخ‌ها خالی می‌مانند و هیچ پاسخ جدید ساخته نمی‌شود.
        attempt.FinalizeAtDeadline(now);
        finalization.AddRange(answers);
        await finalization.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
