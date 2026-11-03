using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed partial class EfPersonalClassStore
{
    public async Task<PersonalClassJoinDto> RequestJoinAsync(PersonalClassActor actor, Guid id, CreatePersonalClassJoinCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // شناسهٔ عملیات در سطح حساب یکتا است؛ دو مقصد متفاوت نیز باید همین قفل را بگیرند.
        var operationLock = $"PersonalClassJoin:{actor.UserId:N}:{command.ClientOperationId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={operationLock}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51062, 'Personal class operation lock unavailable', 1;", ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var existing = await db.PersonalClassJoinRequests.SingleOrDefaultAsync(j => j.UserId == actor.UserId && j.ClientOperationId == command.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.PersonalClassId != id) throw Conflict();
            await tx.CommitAsync(ct); return Join(existing);
        }
        if (!c.AcceptingJoinRequests) throw NotFound();
        RequireAdmissions(c);
        if (await ActiveMembers(id).AnyAsync(m => m.StudentUserId == actor.UserId, ct)) throw Conflict();
        var pending = await db.PersonalClassJoinRequests.SingleOrDefaultAsync(j => j.PersonalClassId == id && j.UserId == actor.UserId && j.Status == PersonalClassJoinRequestStatus.Pending, ct);
        if (pending is not null) throw Conflict();
        var request = PersonalClassJoinRequest.Create(id, actor.UserId, command.ClientOperationId, now);
        db.PersonalClassJoinRequests.Add(request); await SaveClass(c, now, ct); await tx.CommitAsync(ct); return Join(request);
    }

    public async Task<PersonalClassJoinDto> DecideJoinAsync(PersonalClassActor actor, Guid id, DecidePersonalClassJoinCommand command, DateTime now, CancellationToken ct)
    {
        var classId = await db.PersonalClassJoinRequests.AsNoTracking().Where(j => j.Id == id).Select(j => (Guid?)j.PersonalClassId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(classId, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var request = await db.PersonalClassJoinRequests.SingleAsync(j => j.Id == id, ct);
        if (command.Decision == PersonalClassJoinRequestStatus.Cancelled) { if (request.UserId != actor.UserId) throw NotFound(); }
        else await RequireManager(actor.UserId, c, ct);
        if (request.Status == command.Decision) { await tx.CommitAsync(ct); return Join(request); }
        Version(request.RowVersion, command.ExpectedVersion);
        if (request.Status != PersonalClassJoinRequestStatus.Pending) throw Conflict();
        if (command.Decision == PersonalClassJoinRequestStatus.Approved)
        {
            RequireAdmissions(c);
            var member = await ActiveMembers(classId).SingleOrDefaultAsync(m => m.StudentUserId == request.UserId, ct);
            if (member is null)
            {
                await RequireLearnerSeat(c, request.UserId, now, ct);
                member = PersonalClassParticipant.Admit(classId, request.UserId, PersonalClassLocalRoles.Learner, now);
                db.PersonalClassParticipants.Add(member);
            }
            request.Close(command.Decision, command.Reason, now, member.Id);
            await CloseAdmissionIntents(classId, request.UserId, member.Id, null, now, ct);
        }
        else request.Close(command.Decision, command.Reason, now);
        Audit(actor, classId, "JoinRequest" + command.Decision, command.Reason, now);
        await SaveClass(c, now, ct); await tx.CommitAsync(ct); return Join(request);
    }

    public async Task<PersonalClassTransferDto> TransferAsync(PersonalClassActor actor, Guid id, CreatePersonalClassTransferCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); await RequireManager(actor.UserId, c, ct);
        var existing = await db.PersonalClassManagerTransfers.SingleOrDefaultAsync(t => t.PersonalClassId == id && t.ClientOperationId == command.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.TargetUserId != command.TargetUserId || existing.Reason != command.Reason.Trim() || existing.RequestedByUserId != actor.UserId) throw Conflict();
            await tx.CommitAsync(ct); return Transfer(existing);
        }
        Version(c.RowVersion, command.ExpectedVersion);
        if (c.State == PersonalClassState.Archived || await db.PersonalClassManagerTransfers.AnyAsync(t => t.PersonalClassId == id && t.Status == PersonalClassManagerTransferStatus.Pending, ct)) throw Conflict();
        if (!await db.Users.AnyAsync(u => u.Id == command.TargetUserId && u.AccountStatus == UserAccountStatus.Active, ct)) throw NotFound();
        var transfer = PersonalClassManagerTransfer.Create(id, actor.UserId, c.ManagerUserId, command.TargetUserId, command.ClientOperationId, command.Reason, now);
        db.PersonalClassManagerTransfers.Add(transfer); Audit(actor, id, "ManagerTransferRequested", command.Reason, now,
            System.Text.Json.JsonSerializer.Serialize(new { transferId = transfer.Id, transfer.PreviousManagerUserId, transfer.TargetUserId }));
        await SaveClass(c, now, ct); await tx.CommitAsync(ct); return Transfer(transfer);
    }

    public async Task<PersonalClassTransferDto> DecideTransferAsync(PersonalClassActor actor, Guid id, DecidePersonalClassTransferCommand command, DateTime now, CancellationToken ct)
    {
        var classId = await db.PersonalClassManagerTransfers.AsNoTracking().Where(t => t.Id == id).Select(t => (Guid?)t.PersonalClassId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var c = await LockClass(classId, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var transfer = await db.PersonalClassManagerTransfers.SingleAsync(t => t.Id == id, ct);
        if (command.Decision == PersonalClassManagerTransferStatus.Cancelled ? transfer.RequestedByUserId != actor.UserId : transfer.TargetUserId != actor.UserId) throw NotFound();
        if (transfer.Status == command.Decision) { await tx.CommitAsync(ct); return Transfer(transfer); }
        Version(transfer.RowVersion, command.ExpectedVersion);
        if (c.State == PersonalClassState.Archived || c.ManagerUserId != transfer.PreviousManagerUserId) throw Conflict();
        try { transfer.Decide(command.Decision, actor.UserId, now); } catch (InvalidOperationException) { throw Conflict(); }
        if (command.Decision == PersonalClassManagerTransferStatus.Accepted)
        {
            var previous = await ActiveMembers(classId).SingleOrDefaultAsync(m => m.StudentUserId == c.ManagerUserId, ct);
            if (previous is not null)
            {
                await RevokeAcademicGrantsAsync(previous.Id, now, ct);
                var remaining = previous.LocalRoles & ~PersonalClassLocalRoles.Manager;
                if (remaining == PersonalClassLocalRoles.None) previous.End(now, "ManagementTransferred");
                else previous.ChangeRoles(remaining, previous.DelegatedCapabilities);
                // قید یکتای مدیر ابتدا در همان تراکنش آزاد می‌شود؛ هیچ وضعیت میانی Commit نمی‌شود.
                await realtime.CapturePersonalClassesAsync(ct);
                await db.SaveChangesAsync(ct);
            }
            var target = await ActiveMembers(classId).SingleOrDefaultAsync(m => m.StudentUserId == actor.UserId, ct);
            if (target is null) db.PersonalClassParticipants.Add(PersonalClassParticipant.Admit(classId, actor.UserId, PersonalClassLocalRoles.Manager, now));
            else
            {
                await RevokeAcademicGrantsAsync(target.Id, now, ct);
                target.ChangeRoles(target.LocalRoles | PersonalClassLocalRoles.Manager, target.DelegatedCapabilities);
            }
            c.SetManager(actor.UserId);
        }
        Audit(actor, classId, "ManagerTransfer" + command.Decision, transfer.Reason, now,
            System.Text.Json.JsonSerializer.Serialize(new { transferId = transfer.Id, transfer.PreviousManagerUserId, transfer.TargetUserId }));
        await SaveClass(c, now, ct); await tx.CommitAsync(ct); return Transfer(transfer);
    }
}
