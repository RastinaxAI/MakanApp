using MakanApp.Application.Academic;
using MakanApp.Application.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed partial class EfPersonalClassStore
{
    public async Task<PersonalClassPage<PersonalClassDto>> ClassesAsync(Guid actor, int page, int size, CancellationToken ct)
    {
        var query = db.PersonalClasses.AsNoTracking().Where(c => db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id && m.StudentUserId == actor && m.EndedAtUtc == null));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.CreatedAtUtc).ThenBy(c => c.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var results = new List<PersonalClassDto>(rows.Length);
        foreach (var row in rows) results.Add(await MapAsync(row, actor, ct));
        return new(results, page, size, total);
    }
    public async Task<PersonalClassPage<PersonalClassMembershipDto>> MembersAsync(Guid actor, Guid id, int page, int size, CancellationToken ct)
    {
        var c = await GetAsync(actor, id, ct);
        var query = db.PersonalClassParticipants.AsNoTracking().Where(m => m.PersonalClassId == id && (c.ManagerUserId == actor || m.StudentUserId == actor));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.JoinedAtUtc).ThenBy(m => m.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(Member).ToArray(), page, size, total);
    }
    public async Task<PersonalClassPage<PersonalClassInvitationDto>> InvitationsPageAsync(Guid actor, int page, int size, CancellationToken ct)
    {
        var query = db.PersonalClassInvitations.AsNoTracking().Where(i => i.DestinationUserId == actor || db.PersonalClasses.Any(c => c.Id == i.PersonalClassId && c.ManagerUserId == actor &&
            db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id && m.StudentUserId == actor && m.EndedAtUtc == null)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.CreatedAtUtc).ThenBy(i => i.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var ids = rows.Select(i => i.Id).ToArray(); var classIds = rows.Select(i => i.PersonalClassId).Distinct().ToArray();
        var titles = await db.PersonalClasses.AsNoTracking().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title, ct);
        // actor تاریخی از Audit همان دعوت گرفته می‌شود؛ مدیر فعلی جایگزین دعوت‌کنندهٔ قدیمی نمی‌شود.
        var auditActors = await db.Database.SqlQuery<InvitationAuditActor>($"SELECT TRY_CONVERT(uniqueidentifier, JSON_VALUE(CASE WHEN ISJSON([AfterSummary])=1 THEN [AfterSummary] ELSE N'{{}}' END, '$.referenceId')) AS [InvitationId], TRY_CONVERT(uniqueidentifier, [TargetId]) AS [ClassId], [ActorUserId] AS [InviterUserId] FROM [platform].[AdminAuditLogs] WHERE [Action]=N'personal_class.InvitationCreated' AND [TargetType]=N'PersonalClass'")
            .Where(a => a.InvitationId.HasValue && ids.Contains(a.InvitationId.Value) && a.ClassId.HasValue && classIds.Contains(a.ClassId.Value)).ToArrayAsync(ct);
        var actors = auditActors.Where(a => a.InviterUserId.HasValue).Select(a => a.InviterUserId!.Value).Distinct().ToArray();
        var identities = await (from u in db.Users.AsNoTracking()
            join p in db.Persons.AsNoTracking() on u.PersonId equals (Guid?)p.Id into people
            from p in people.DefaultIfEmpty() where actors.Contains(u.Id)
            select new PersonalClassInviterIdentity(u.Id, p == null ? u.Username ?? "دعوت‌کننده" : p.DisplayName ?? p.FirstName + " " + p.LastName, u.Username)).ToDictionaryAsync(i => i.UserId, ct);
        var result = rows.Select(i =>
        {
            var candidates = auditActors.Where(a => a.InvitationId == i.Id && a.ClassId == i.PersonalClassId && a.InviterUserId.HasValue)
                .Select(a => a.InviterUserId!.Value).Distinct().ToArray();
            return Invitation(i, clock.GetUtcNow().UtcDateTime) with { ClassTitle = titles.GetValueOrDefault(i.PersonalClassId),
                Inviter = candidates.Length == 1 ? identities.GetValueOrDefault(candidates[0]) : null };
        }).ToArray();
        return new(result, page, size, total);
    }
    public async Task<PersonalClassPage<PersonalClassJoinDto>> JoinsAsync(Guid actor, Guid? id, int page, int size, CancellationToken ct)
    {
        if (id.HasValue)
        {
            var c = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();
            await RequireManager(actor, c, ct);
        }
        var query = db.PersonalClassJoinRequests.AsNoTracking().Where(j => id.HasValue ? j.PersonalClassId == id : j.UserId == actor);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(j => j.CreatedAtUtc).ThenBy(j => j.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(Join).ToArray(), page, size, total);
    }
    public async Task<PersonalClassPage<PersonalClassTransferDto>> TransfersAsync(Guid actor, Guid? id, int page, int size, CancellationToken ct)
    {
        if (id.HasValue)
        {
            var c = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();
            await RequireManager(actor, c, ct);
        }
        var query = db.PersonalClassManagerTransfers.AsNoTracking().Where(t => id.HasValue ? t.PersonalClassId == id : t.TargetUserId == actor || t.RequestedByUserId == actor);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(t => t.CreatedAtUtc).ThenBy(t => t.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(Transfer).ToArray(), page, size, total);
    }
    public Task<OfficialSpacesPairResult> SpacesAsync(Guid actor, Guid id, CancellationToken ct) => officialSpaces.ReadPersonalClassAsync(actor, id, ct);

    private sealed class InvitationAuditActor
    {
        public Guid? InvitationId { get; set; }
        public Guid? ClassId { get; set; }
        public Guid? InviterUserId { get; set; }
    }
    public async Task<PersonalClassBackfillPlanRow> BackfillPlanAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var c = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();
        await RequireManager(actor, c, ct);
        return (await officialSpaces.PlanPersonalClassesAsync(id, ct)).Single();
    }
}
