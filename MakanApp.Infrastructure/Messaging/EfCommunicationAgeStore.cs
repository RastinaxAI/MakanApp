using System.Data;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace MakanApp.Infrastructure.Messaging;

public sealed class EfCommunicationAgeStore(MakanDbContext db, IPlatformIamStore iam,
    TimeProvider clock) : ICommunicationAgeStore
{
    public async Task<CommunicationAgeResult> GetPlatformAsync(Guid actorId, Guid sessionId, Guid userId, string correlationId, CancellationToken ct)
    {
        await AuthorizePlatformAsync(actorId, sessionId, false, ct);
        var subject = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw NotFound();
        var actorPersonId = await db.Users.Where(u => u.Id == actorId).Select(u => u.PersonId).SingleAsync(ct);
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actorId, actorPersonId, sessionId,
            "messaging.age.read", "User", userId.ToString("D"), null, "مشاهده وضعیت تأیید ارتباطی بدون تاریخ تولد",
            clock.GetUtcNow().UtcDateTime, correlationId, AdminAuditResult.Succeeded, null,
            $"{subject.CommunicationAgeCategory};{subject.CommunicationAgeVerificationStatus}"));
        await db.SaveChangesAsync(ct);
        return Map(subject);
    }

    public async Task<MessagingStatusSnapshot> GetSelfStatusAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var row = await (from user in db.Users.AsNoTracking()
            join session in db.UserSessions.AsNoTracking() on user.Id equals session.UserId
            where user.Id == userId && session.Id == sessionId && user.AccountStatus == UserAccountStatus.Active &&
                session.RevokedAtUtc == null && session.ExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now
            select new { user.CommunicationAgeCategory, user.CommunicationAgeVerificationStatus,
                Complete = user.PersonId != null && user.Username != null && user.ProfileCompletedAtUtc != null,
                Personal = session.SelectedMembershipId == null, user.MessagingRestrictedUntilUtc, user.RowVersion })
            .SingleOrDefaultAsync(ct) ?? throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        return new(row.CommunicationAgeVerificationStatus, row.CommunicationAgeCategory, row.Complete,
            row.Personal, row.MessagingRestrictedUntilUtc, Convert.ToBase64String(row.RowVersion));
    }

    public async Task<CommunicationAgeResult> VerifyInitialAsync(Guid actorId, Guid sessionId, Guid userId,
        VerifyCommunicationAgeCommand command, string correlationId, CancellationToken ct)
    {
        try { return await VerifyCoreAsync(actorId, sessionId, null, userId, command, correlationId, ct, true); }
        catch (SqlException exception) when (exception.Number == 1205)
        { throw new MessagingException("AGE_VERIFICATION_CONFLICT", "عملیات هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید."); }
    }

    public async Task<CommunicationAgeResult> GetSchoolAsync(Guid actorId, Guid sessionId, AccessContext access, Guid personId, CancellationToken ct)
    {
        await CheckSchoolAsync(actorId, sessionId, access, ct);
        return Map(await SchoolSubjectAsync(access.OrganizationId!.Value, personId, ct));
    }

    public async Task<CommunicationAgeResult> VerifyAsync(Guid actorId, Guid sessionId, AccessContext? access, Guid targetId,
        VerifyCommunicationAgeCommand command, string correlationId, CancellationToken ct)
    {
        try { return await VerifyCoreAsync(actorId, sessionId, access, targetId, command, correlationId, ct); }
        catch (SqlException exception) when (exception.Number == 1205)
        { throw new MessagingException("AGE_VERIFICATION_CONFLICT", "عملیات هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید."); }
    }

    private async Task<CommunicationAgeResult> VerifyCoreAsync(Guid actorId, Guid sessionId, AccessContext? access, Guid targetId,
        VerifyCommunicationAgeCommand command, string correlationId, CancellationToken ct, bool initialPlatformVerification = false)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        User subject;
        if (access is not null)
        {
            await CheckSchoolAsync(actorId, sessionId, access, ct);
            subject = await SchoolSubjectAsync(access.OrganizationId!.Value, targetId, ct, true);
        }
        else
        {
            await AuthorizePlatformAsync(actorId, sessionId, !initialPlatformVerification, ct);
            subject = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={targetId}")
                .SingleOrDefaultAsync(ct) ?? throw NotFound();
        }
        // User از ابتدا با قفل به‌روزرسانی خوانده می‌شود؛ ارتقای دو قفل خواندن مستقل لازم نیست.
        SchoolOnboardingValidation.Version(subject.RowVersion, command.ExpectedVersion);
        if (access is null && subject.CommunicationAgeVerificationStatus != (initialPlatformVerification
                ? CommunicationAgeVerificationStatus.Unverified : CommunicationAgeVerificationStatus.Conflict))
            throw new MessagingException("AGE_VERIFICATION_CONFLICT", "وضعیت جاری برای این عملیات تأیید مجاز نیست.");
        if (initialPlatformVerification && subject.AccountStatus != UserAccountStatus.Active) throw NotFound();
        var previous = subject.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified
            ? subject.CommunicationAgeCategory : CommunicationAgeCategory.Unknown;
        var latest = await db.CommunicationAgeVerifications.Where(v => v.UserId == subject.Id && v.Status == CommunicationAgeVerificationStatus.Verified)
            .OrderByDescending(v => v.CreatedAtUtc).ThenByDescending(v => v.Id).FirstOrDefaultAsync(ct);
        var conflict = access is not null && (subject.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Conflict ||
            latest is not null && latest.VerifiedBirthDate != command.VerifiedBirthDate);
        var evidence = CommunicationAgeVerification.Create(subject.Id, command.VerifiedBirthDate, previous, conflict,
            access is not null ? CommunicationAgeVerificationSource.School : initialPlatformVerification
                ? CommunicationAgeVerificationSource.PlatformVerification : CommunicationAgeVerificationSource.PlatformResolution,
            actorId, access?.OrganizationId, command.Reason, now);
        db.CommunicationAgeVerifications.Add(evidence);
        if (conflict) subject.MarkCommunicationAgeConflict(now);
        else subject.SetCommunicationAgeCategory(evidence.ProposedCategory, now);
        var actorPersonId = await db.Users.Where(u => u.Id == actorId).Select(u => u.PersonId).SingleAsync(ct);
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actorId, actorPersonId, sessionId,
            conflict ? "messaging.age.conflict" : initialPlatformVerification ? "messaging.age.initial.verified"
                : access is null ? "messaging.age.conflict.resolved" : "messaging.age.verified",
            "User", subject.Id.ToString("D"), access?.OrganizationId, command.Reason.Trim(), now, correlationId,
            AdminAuditResult.Succeeded, previous.ToString(), $"{subject.CommunicationAgeCategory};{subject.CommunicationAgeVerificationStatus};{evidence.Source}"));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new MessagingException("AGE_VERIFICATION_CONFLICT", "وضعیت هم‌زمان تغییر کرده است."); }
        await transaction.CommitAsync(ct);
        if (conflict) throw new MessagingException("AGE_VERIFICATION_CONFLICT", "شاهد متعارض ثبت شد؛ حل مجاز پلتفرم لازم است.");
        return Map(subject);
    }

    private async Task AuthorizePlatformAsync(Guid actorId, Guid sessionId, bool requireOwner, CancellationToken ct)
    {
        var actor = await iam.GetAuthorizationSnapshotAsync(actorId, ct);
        if (actor is null || !actor.Permissions.Contains(PlatformPermissionKeys.UsersManage) ||
            requireOwner && !actor.Roles.Contains(PlatformRoleKeys.PlatformOwner))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "عملیات مجاز نیست.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await db.UserSessions.AnyAsync(session => session.Id == sessionId && session.UserId == actorId &&
                session.RevokedAtUtc == null && session.ExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "نشست جاری معتبر لازم است.");
    }

    private async Task CheckSchoolAsync(Guid actorId, Guid sessionId, AccessContext access, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (access.ActiveRole != OrganizationRole.Manager || access.OrganizationId is null ||
            !await db.UserSessions.AnyAsync(s => s.Id == sessionId && s.UserId == actorId && s.RevokedAtUtc == null &&
                s.ExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now && s.SelectedMembershipId == access.MembershipId && s.SelectedRole == nameof(OrganizationRole.Manager), ct) ||
            !await (from m in db.Memberships join r in db.RoleAssignments on m.Id equals r.MembershipId
                join o in db.Organizations on m.OrganizationId equals o.Id
                where m.Id == access.MembershipId && m.UserId == actorId && m.OrganizationId == access.OrganizationId &&
                    m.Status == MembershipStatus.Active && m.EndedAtUtc == null && r.Role == OrganizationRole.Manager &&
                    r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null && o.Status == OrganizationStatus.Active select m.Id).AnyAsync(ct))
            throw new OrganizationException(OrganizationErrorCodes.WorkspaceNotAllowed, "مدیر جاری سازمان لازم است.");
    }

    private async Task<User> SchoolSubjectAsync(Guid organizationId, Guid personId, CancellationToken ct, bool forUpdate = false)
    {
        var users = forUpdate ? db.Users.FromSqlRaw("SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK)") : db.Users.AsQueryable();
        return await (from p in db.OrganizationPersons join u in users on p.PersonId equals u.PersonId
            where p.Id == personId && p.OrganizationId == organizationId && p.Status == OrganizationPersonStatus.Active &&
                p.EndedAtUtc == null && u.AccountStatus == UserAccountStatus.Active select u).SingleOrDefaultAsync(ct) ?? throw NotFound();
    }
    private static CommunicationAgeResult Map(User user) => new(user.Id,
        user.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified ? user.CommunicationAgeCategory : CommunicationAgeCategory.Unknown,
        user.CommunicationAgeVerificationStatus, Convert.ToBase64String(user.RowVersion));
    private static MessagingException NotFound() => new("AGE_SUBJECT_NOT_FOUND", "پرونده مجاز یافت نشد.");
}
