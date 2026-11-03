using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfPersonalClassAcademicPermissionStore(MakanDbContext db, CommercialActorAccess actors,
    IPlatformPermissionResolver permissions, RealtimeInvalidationWriter realtime) : IPersonalClassAcademicPermissionStore
{
    public async Task<PersonalClassAcademicPermissionDto> SetAsync(PersonalClassActor actor, Guid classId, Guid membershipId,
        SetPersonalClassAcademicPermissionCommand command, bool platform, DateTime now, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { classId, membershipId, platform, command.Capabilities, command.ExpectedMembershipVersion, command.ExpectedGrantVersion, Reason = command.Reason.Trim() })));
        // این خواندن فقط کلید قفل را پیدا می‌کند؛ مجوز و وضعیت پس از قفل دوباره بررسی می‌شوند.
        var host = await db.PersonalClasses.AsNoTracking().Where(c => c.Id == classId).Select(c => (Guid?)c.HostUserId)
            .SingleOrDefaultAsync(ct) ?? throw EfPersonalClassFormationStore.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={host}")
            .AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        var classroom = await db.PersonalClasses.FromSqlInterpolated(
            $"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={classId}").SingleOrDefaultAsync(ct)
            ?? throw EfPersonalClassFormationStore.NotFound();
        await Authorize(actor, classroom, platform, ct);
        var replay = await db.PersonalClassAcademicGrants.SingleOrDefaultAsync(g => g.GrantedByUserId == actor.UserId && g.ClientOperationId == command.ClientOperationId, ct);
        if (replay is not null)
        {
            if (replay.RequestHash != hash) throw EfPersonalClassFormationStore.Conflict();
            var result = await ResultAsync(replay, ct); await tx.CommitAsync(ct); return result;
        }
        if (classroom.State == PersonalClassState.Archived && command.Capabilities != PersonalClassAcademicCapabilities.None)
            throw EfPersonalClassFormationStore.Conflict();
        var member = await db.PersonalClassParticipants.SingleOrDefaultAsync(m => m.PersonalClassId == classId && m.Id == membershipId && m.EndedAtUtc == null, ct)
            ?? throw EfPersonalClassFormationStore.NotFound();
        EfPersonalClassFormationStore.Version(member.RowVersion, command.ExpectedMembershipVersion);
        Guid? parentId = null;
        if (platform)
        {
            if (classroom.ManagerUserId != member.StudentUserId || !member.LocalRoles.HasFlag(PersonalClassLocalRoles.Manager))
                throw EfPersonalClassFormationStore.NotFound();
        }
        else
        {
            if (!member.LocalRoles.HasFlag(PersonalClassLocalRoles.Teacher) || member.LocalRoles.HasFlag(PersonalClassLocalRoles.Manager))
                throw EfPersonalClassFormationStore.NotFound();
            var parent = await (from grant in PersonalClassAcademicAccessQuery.Effective(db, classId)
                join manager in db.PersonalClassParticipants on grant.MembershipId equals manager.Id
                where manager.StudentUserId == actor.UserId select grant).SingleOrDefaultAsync(ct);
            if (command.Capabilities != PersonalClassAcademicCapabilities.None &&
                (parent is null || (parent.Capabilities & command.Capabilities) != command.Capabilities))
                throw EfPersonalClassFormationStore.NotFound();
            parentId = parent?.GrantId;
        }
        var current = await db.PersonalClassAcademicGrants.SingleOrDefaultAsync(g => g.PersonalClassId == classId && g.MembershipId == membershipId && g.RevokedAtUtc == null, ct);
        if (current is null ? command.ExpectedGrantVersion is not null : command.ExpectedGrantVersion is null)
            throw EfPersonalClassFormationStore.Conflict();
        if (current is not null)
        {
            EfPersonalClassFormationStore.Version(current.RowVersion, command.ExpectedGrantVersion!);
            current.Revoke(now);
            foreach (var child in await db.PersonalClassAcademicGrants.Where(g => g.ParentGrantId == current.Id && g.RevokedAtUtc == null).ToArrayAsync(ct)) child.Revoke(now);
            // ابتدا یکتایی مجوز فعال آزاد می‌شود؛ هر دو ذخیره همچنان در یک تراکنش هستند.
            await realtime.CapturePersonalClassesAsync(ct); await db.SaveChangesAsync(ct);
        }
        var created = PersonalClassAcademicGrant.Create(classId, membershipId, command.Capabilities, parentId,
            actor.UserId, command.ClientOperationId, hash, now);
        db.PersonalClassAcademicGrants.Add(created);
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, null, actor.SessionId,
            platform ? "personal_class.AcademicPermissionSet" : "personal_class.AcademicPermissionDelegated",
            "PersonalClassAcademicGrant", created.Id.ToString(), null, command.Reason.Trim(), now,
            actor.CorrelationId, AdminAuditResult.Succeeded, null, null));
        await realtime.CapturePersonalClassesAsync(ct); await db.SaveChangesAsync(ct);
        var dto = await ResultAsync(created, ct); await tx.CommitAsync(ct); return dto;
    }

    public async Task<PersonalClassPage<PersonalClassAcademicPermissionDto>> ListAsync(PersonalClassActor actor, Guid classId,
        bool platform, int page, int pageSize, CancellationToken ct)
    {
        var classroom = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classId, ct) ?? throw EfPersonalClassFormationStore.NotFound();
        await Authorize(actor, classroom, platform, ct);
        var query = db.PersonalClassAcademicGrants.AsNoTracking().Where(g => g.PersonalClassId == classId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(g => g.CreatedAtUtc).ThenBy(g => g.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var effective = await PersonalClassAcademicAccessQuery.Effective(db, classId).Select(g => g.GrantId).ToArrayAsync(ct);
        return new(rows.Select(g => Map(g, effective.Contains(g.Id))).ToArray(), page, pageSize, count);
    }
    private async Task Authorize(PersonalClassActor actor, PersonalClass classroom, bool platform, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (platform)
        {
            if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesAcademicPermissions, ct))
                throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری Platform لازم است.");
        }
        else if (classroom.ManagerUserId != actor.UserId || !await db.PersonalClassParticipants.AnyAsync(m => m.PersonalClassId == classroom.Id &&
            m.StudentUserId == actor.UserId && m.EndedAtUtc == null && (m.LocalRoles & PersonalClassLocalRoles.Manager) != 0, ct))
            throw EfPersonalClassFormationStore.NotFound();
    }
    private async Task<PersonalClassAcademicPermissionDto> ResultAsync(PersonalClassAcademicGrant grant, CancellationToken ct) =>
        Map(grant, await PersonalClassAcademicAccessQuery.Effective(db, grant.PersonalClassId).AnyAsync(g => g.GrantId == grant.Id, ct));
    private static PersonalClassAcademicPermissionDto Map(PersonalClassAcademicGrant g, bool effective) =>
        new(g.Id, g.PersonalClassId, g.MembershipId, g.Capabilities, g.ParentGrantId, effective, g.CreatedAtUtc, g.RevokedAtUtc, Convert.ToBase64String(g.RowVersion));
}
