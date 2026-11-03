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
        return new(rows.Select(i => Invitation(i, clock.GetUtcNow().UtcDateTime)).ToArray(), page, size, total);
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
    public async Task<PersonalClassBackfillPlanRow> BackfillPlanAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var c = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();
        await RequireManager(actor, c, ct);
        return (await officialSpaces.PlanPersonalClassesAsync(id, ct)).Single();
    }
}
