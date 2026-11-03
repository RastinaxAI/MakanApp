using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed partial class EfPersonalClassStore
{
    public async Task<PersonalClassDto> ConfigureAsync(PersonalClassActor actor, Guid id, ConfigurePersonalClassCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); await RequireManager(actor.UserId, c, ct);
        Version(c.RowVersion, command.ExpectedVersion);
        var count = await ActiveMembers(id).CountAsync(m => (m.LocalRoles & PersonalClassLocalRoles.Learner) != 0, ct);
        try { c.Configure(command.Capacity, command.AcceptingJoinRequests, count); } catch (InvalidOperationException) { throw Conflict(); }
        Audit(actor, id, "Configured", command.Reason, now); await SaveClass(c, now, ct); await tx.CommitAsync(ct); return await MapAsync(c, actor.UserId, ct);
    }

    public async Task<PersonalClassDto> StateAsync(PersonalClassActor actor, Guid id, PersonalClassReasonCommand command, PersonalClassState state, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); await RequireManager(actor.UserId, c, ct);
        if (c.State == state) { await tx.CommitAsync(ct); return await MapAsync(c, actor.UserId, ct); }
        Version(c.RowVersion, command.ExpectedVersion);
        try
        {
            if (state == PersonalClassState.Archived) c.Archive(now);
            else if (state == PersonalClassState.Suspended) c.Suspend();
            else if (state == PersonalClassState.Active)
            {
                var ready = await db.OfficialSpaceProvisions.CountAsync(p => p.PersonalClassId == id && p.State == OfficialSpaceState.Ready, ct) == 2 &&
                    await db.Conversations.CountAsync(s => s.PersonalClassId == id && s.Status == ConversationStatus.Active, ct) == 2;
                c.Activate(ready);
            }
            else throw Conflict();
        }
        catch (InvalidOperationException) { throw Error(AcademicErrorCodes.PersonalClassNotReady, "گذار وضعیت با آمادگی کلاس سازگار نیست."); }
        Audit(actor, id, state.ToString(), command.Reason, now); await SaveClass(c, now, ct); await tx.CommitAsync(ct); return await MapAsync(c, actor.UserId, ct);
    }

    public async Task<PersonalClassMembershipDto> RolesAsync(PersonalClassActor actor, Guid id, Guid memberId, ChangePersonalClassRolesCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); await RequireManager(actor.UserId, c, ct);
        if (c.State == PersonalClassState.Archived) throw Conflict();
        var member = await ActiveMembers(id).SingleOrDefaultAsync(m => m.Id == memberId, ct) ?? throw NotFound();
        Version(member.RowVersion, command.ExpectedVersion);
        // مسئولیت Manager فقط از مسیر انتقالِ پذیرفته‌شده تغییر می‌کند؛ مدیر اضافی حدس زده نمی‌شود.
        if ((member.LocalRoles & PersonalClassLocalRoles.Manager) != (command.Roles & PersonalClassLocalRoles.Manager))
            throw Error(AcademicErrorCodes.PersonalClassLastManagerRequired, "تغییر مدیر از مسیر پذیرش انتقال مسئولیت انجام می‌شود.");
        if (!member.LocalRoles.HasFlag(PersonalClassLocalRoles.Learner) && command.Roles.HasFlag(PersonalClassLocalRoles.Learner))
            await RequireLearnerSeat(c, member.StudentUserId, now, ct);
        var lostEducationalRole = (member.LocalRoles & (PersonalClassLocalRoles.Manager | PersonalClassLocalRoles.Teacher) & ~command.Roles) != 0;
        try { member.ChangeRoles(command.Roles, command.DelegatedCapabilities); } catch (InvalidOperationException) { throw Conflict(); }
        if (lostEducationalRole) await RevokeAcademicGrantsAsync(member.Id, now, ct);
        Audit(actor, id, "LocalRolesChanged", command.Reason, now); await SaveClass(c, now, ct); await tx.CommitAsync(ct); return Member(member);
    }

    public async Task EndAsync(PersonalClassActor actor, Guid id, Guid memberId, PersonalClassReasonCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var member = await db.PersonalClassParticipants.SingleOrDefaultAsync(m => m.PersonalClassId == id && m.Id == memberId, ct) ?? throw NotFound();
        if (actor.UserId != member.StudentUserId) await RequireManager(actor.UserId, c, ct);
        if (member.EndedAtUtc.HasValue) { await tx.CommitAsync(ct); return; }
        Version(member.RowVersion, command.ExpectedVersion);
        if (member.LocalRoles.HasFlag(PersonalClassLocalRoles.Manager) && c.State != PersonalClassState.Archived)
            throw Error(AcademicErrorCodes.PersonalClassLastManagerRequired, "مدیر مسئول ابتدا باید انتقال معتبر یا بایگانی انجام دهد.");
        member.End(now, command.Reason); Audit(actor, id, "MembershipEnded", command.Reason, now);
        await RevokeAcademicGrantsAsync(member.Id, now, ct);
        await SaveClass(c, now, ct); await tx.CommitAsync(ct);
    }

    public async Task<PersonalClassInvitationDto> CloseInvitationAsync(PersonalClassActor actor, Guid id, PersonalClassReasonCommand command, bool decline, DateTime now, CancellationToken ct)
    {
        var classId = await db.PersonalClassInvitations.AsNoTracking().Where(i => i.Id == id).Select(i => (Guid?)i.PersonalClassId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(classId, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var invitation = await db.PersonalClassInvitations.SingleAsync(i => i.Id == id, ct);
        if (decline) { if (invitation.DestinationUserId != actor.UserId) throw NotFound(); } else await RequireManager(actor.UserId, c, ct);
        if (decline ? invitation.DeclinedAtUtc.HasValue : invitation.RevokedAtUtc.HasValue) { await tx.CommitAsync(ct); return Invitation(invitation, now); }
        Version(invitation.RowVersion, command.ExpectedVersion);
        try { invitation.Close(decline, command.Reason, now); } catch (InvalidOperationException) { throw Conflict(); }
        await SaveClass(c, now, ct); await tx.CommitAsync(ct); return Invitation(invitation, now);
    }

    public async Task RepairAsync(PersonalClassActor actor, Guid id, RepairPersonalClassSpacesCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); await RequireManager(actor.UserId, c, ct);
        Version(c.RowVersion, command.ExpectedVersion);
        if (c.State == PersonalClassState.Archived) throw Conflict();
        if (!c.FormationRequestId.HasValue)
        {
            var plan = (await officialSpaces.PlanPersonalClassesAsync(id, ct)).Single();
            if (plan.Conflict || !string.Equals(plan.PlanSha256, command.ApprovedPlanSha256, StringComparison.OrdinalIgnoreCase)) throw Conflict();
            foreach (var purpose in new[] { ConversationType.Group, ConversationType.Channel })
                if (!await db.OfficialSpaceProvisions.AnyAsync(p => p.PersonalClassId == id && p.Purpose == purpose, ct))
                    db.OfficialSpaceProvisions.Add(OfficialSpaceProvision.RequestPersonalClass(id, purpose, now));
            spaces.PersonalClassChanged(id, now);
        }
        Audit(actor, id, "SpacesRepairRequested", command.Reason, now); await SaveClass(c, now, ct); await tx.CommitAsync(ct);
    }

    private async Task RevokeAcademicGrantsAsync(Guid membershipId, DateTime now, CancellationToken ct)
    {
        var grants = await db.PersonalClassAcademicGrants.Where(g => g.MembershipId == membershipId && g.RevokedAtUtc == null).ToArrayAsync(ct);
        var ids = grants.Select(g => g.Id).ToArray();
        foreach (var grant in grants) grant.Revoke(now);
        foreach (var child in await db.PersonalClassAcademicGrants.Where(g => g.ParentGrantId.HasValue && ids.Contains(g.ParentGrantId.Value) && g.RevokedAtUtc == null).ToArrayAsync(ct))
            child.Revoke(now);
    }
}
