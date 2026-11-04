using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed class EfSchoolMembershipManagementStore(MakanDbContext db, TimeProvider clock,
    RealtimeInvalidationWriter realtime, OfficialSpaceEventWriter officialSpaces) : ISchoolMembershipManagementStore
{
    public async Task<SchoolMembershipMutationResult> ExecuteAsync(AccessContext actor, Guid membershipId,
        SchoolMembershipMutation command, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // همان قفل سازمان که mutationهای Platform می‌گیرند؛ حذف رقابتی مدیران زیر یک مرز سریال می‌شود.
        var organization = await db.Organizations.FromSqlInterpolated(
            $"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor.OrganizationId}").SingleOrDefaultAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        await EnsureManagerAsync(actor, organization?.IsActive == true, now, ct);
        var membership = await db.Memberships.FromSqlInterpolated(
            $"SELECT * FROM [organization].[Memberships] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={actor.OrganizationId} AND [Id]={membershipId}")
            .SingleOrDefaultAsync(ct) ?? throw Error(SchoolMembershipErrorCodes.NotFound, "عضویت مجاز پیدا نشد.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            command.Kind, MembershipId = membershipId, command.RoleAssignmentId, command.Role,
            command.ExpectedMembershipVersion, command.ExpectedRoleVersion, command.Reason
        })));
        var previous = await db.OrganizationMembershipOperations.AsNoTracking().SingleOrDefaultAsync(o =>
            o.OrganizationId == actor.OrganizationId && o.ActorUserId == actor.UserId && o.ClientOperationId == command.ClientOperationId, ct);
        if (previous is not null)
        {
            if (previous.RequestHash != hash)
                throw Error(SchoolMembershipErrorCodes.OperationConflict, "شناسهٔ عملیات قبلاً برای قصد متفاوت استفاده شده است.");
            var replay = await ResultAsync(membership, previous, true, ct);
            await transaction.CommitAsync(ct);
            return replay;
        }

        SchoolOnboardingValidation.Version(membership.RowVersion, command.ExpectedMembershipVersion);
        if (!membership.IsActive) throw Conflict("عضویت فعال نیست؛ عضویت تازه نیازمند دعوت و پذیرش است.");
        var roles = await db.RoleAssignments.Where(r => r.MembershipId == membershipId && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null)
            .OrderBy(r => r.Id).ToArrayAsync(ct);
        RoleAssignment? selected = null;
        if (command.RoleAssignmentId.HasValue)
        {
            selected = await db.RoleAssignments.SingleOrDefaultAsync(r => r.Id == command.RoleAssignmentId && r.MembershipId == membershipId, ct)
                ?? throw Error(SchoolMembershipErrorCodes.RoleNotFound, "دورهٔ نقش مجاز پیدا نشد.");
            SchoolOnboardingValidation.Version(selected.RowVersion, command.ExpectedRoleVersion!);
            if (!selected.IsActive) throw Conflict("دورهٔ نقش پایان یافته است.");
            if (command.Kind == OrganizationMembershipOperationKind.ChangeRole && selected.Role == command.Role)
                throw SchoolOnboardingValidation.Invalid("نقش مقصد باید با نقش مبدأ متفاوت باشد.");
        }
        if (selected?.Role == OrganizationRole.Manager || command.Kind == OrganizationMembershipOperationKind.EndMembership && roles.Any(r => r.Role == OrganizationRole.Manager))
            await EnsureReplacementManagerAsync(actor.OrganizationId!.Value, membershipId, ct);

        var before = JsonSerializer.Serialize(new { Status = membership.Status.ToString(), Roles = roles.Select(r => r.Role.ToString()).Order().ToArray() });
        Guid? resultAssignmentId = null;
        switch (command.Kind)
        {
            case OrganizationMembershipOperationKind.GrantRole:
            case OrganizationMembershipOperationKind.ChangeRole:
                if (!await db.Users.AnyAsync(u => u.Id == membership.UserId && u.AccountStatus == UserAccountStatus.Active, ct))
                    throw Conflict("حساب مقصد برای اعطای نقش فعال نیست.");
                selected?.End(now);
                var destination = roles.SingleOrDefault(r => r.Role == command.Role);
                if (destination is null)
                {
                    destination = RoleAssignment.CreateActive(membershipId, command.Role!.Value, now);
                    db.RoleAssignments.Add(destination);
                }
                resultAssignmentId = destination.Id;
                break;
            case OrganizationMembershipOperationKind.EndRole:
                selected!.End(now); resultAssignmentId = selected.Id;
                break;
            case OrganizationMembershipOperationKind.EndMembership:
                foreach (var role in roles) role.End(now);
                membership.End(now);
                break;
            default:
                throw SchoolOnboardingValidation.Invalid("نوع عملیات معتبر نیست.");
        }
        var receipt = OrganizationMembershipOperation.Record(actor.OrganizationId!.Value, actor.UserId, membershipId,
            command.ClientOperationId, command.Kind, hash, resultAssignmentId, now);
        db.OrganizationMembershipOperations.Add(receipt);
        var actorPerson = await db.Users.Where(u => u.Id == actor.UserId).Select(u => u.PersonId).SingleAsync(ct);
        var action = command.Kind switch
        {
            OrganizationMembershipOperationKind.GrantRole => "organization.membership.role.grant",
            OrganizationMembershipOperationKind.ChangeRole => "organization.membership.role.change",
            OrganizationMembershipOperationKind.EndRole => "organization.membership.role.end",
            _ => "organization.membership.end"
        };
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, actorPerson, actor.SessionId, action, "Membership",
            membershipId.ToString("D"), actor.OrganizationId, command.Reason, now, command.CorrelationId,
            AdminAuditResult.Succeeded, before, JsonSerializer.Serialize(new
            { command.ClientOperationId, ResultRoleAssignmentId = resultAssignmentId, Status = membership.Status.ToString(), Role = command.Role?.ToString() })));
        await realtime.CaptureSchoolAsync(ct);
        await officialSpaces.CaptureSchoolAsync(ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Error(OrganizationErrorCodes.ConcurrencyConflict, "نسخهٔ عضویت یا نقش تغییر کرده است."); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        { throw Conflict("قصد یا نقش فعال یکتا هم‌زمان ثبت شده است؛ وضعیت را دوباره بخوانید."); }
        var result = await ResultAsync(membership, receipt, false, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task EnsureManagerAsync(AccessContext actor, bool organizationActive, DateTime now, CancellationToken ct)
    {
        if (!organizationActive || actor.WorkspaceType != WorkspaceType.Organization || actor.ActiveRole != OrganizationRole.Manager ||
            !await db.Memberships.AnyAsync(m => m.Id == actor.MembershipId && m.UserId == actor.UserId && m.OrganizationId == actor.OrganizationId &&
                m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
                db.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Role == OrganizationRole.Manager && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null) &&
                db.Users.Any(u => u.Id == m.UserId && u.AccountStatus == UserAccountStatus.Active) &&
                db.UserSessions.Any(s => s.Id == actor.SessionId && s.UserId == m.UserId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now &&
                    s.AbsoluteExpiresAtUtc > now && s.SelectedMembershipId == m.Id && s.SelectedRole == "Manager"), ct))
            throw Error(OrganizationErrorCodes.WorkspaceNotAllowed, "مجوز جاری مدیر همان سازمان لازم است.");
    }

    private async Task EnsureReplacementManagerAsync(Guid organizationId, Guid membershipId, CancellationToken ct)
    {
        // حساب معلق یا دورهٔ عضویت/نقش پایان‌یافته، مدیر جایگزین محسوب نمی‌شود.
        if (!await (from member in db.Memberships
            join role in db.RoleAssignments on member.Id equals role.MembershipId
            join user in db.Users on member.UserId equals user.Id
            where member.OrganizationId == organizationId && member.Id != membershipId && member.Status == MembershipStatus.Active && member.EndedAtUtc == null &&
                role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
            select member.Id).AnyAsync(ct))
            throw Error(SchoolMembershipErrorCodes.LastManagerRequired, "آخرین مدیر فعال سازمان قابل حذف یا تغییر نقش نیست.");
    }

    private async Task<SchoolMembershipMutationResult> ResultAsync(Membership membership, OrganizationMembershipOperation receipt, bool replayed, CancellationToken ct)
    {
        var roles = await db.RoleAssignments.AsNoTracking().Where(r => r.MembershipId == membership.Id &&
            r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null).OrderBy(r => r.Role)
            .Select(r => new SchoolRoleAssignmentResult(r.Id, r.Role, r.Status, r.AssignedAtUtc, r.EndedAtUtc, Convert.ToBase64String(r.RowVersion))).ToArrayAsync(ct);
        return new(membership.Id, membership.Status, Convert.ToBase64String(membership.RowVersion), roles,
            receipt.ResultRoleAssignmentId, receipt.ClientOperationId, receipt.CompletedAtUtc, replayed);
    }

    private static OrganizationException Error(string code, string message) => new(code, message);
    private static OrganizationException Conflict(string message) => Error(SchoolMembershipErrorCodes.Conflict, message);
}
