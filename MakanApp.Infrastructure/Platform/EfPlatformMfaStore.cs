using System.Data;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed class EfPlatformMfaStore(MakanDbContext dbContext, IPlatformIamStore iamStore, TimeProvider clock) : IPlatformMfaStore
{
    public async Task<PlatformMfaResult> ExecuteAsync(Guid actorUserId, Guid actorSessionId, Guid? targetUserId,
        Func<PlatformMfaState, PlatformMfaResult> operation, CancellationToken cancellationToken)
    {
        PlatformMfaResult result;
        string? error;
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            // ترتیب ثابت actor/subject و قفل تراکنشی، مصرف recovery و reset هم‌زمان را serialize می‌کند.
            foreach (var id in new[] { actorUserId, targetUserId ?? actorUserId }.Distinct().Order())
            {
                var resource = "makan-platform-mfa:" + id.ToString("N");
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
                        @LockOwner='Transaction', @LockTimeout=10000;
                    IF @result < 0 THROW 51004, 'MFA_LOCK_FAILED', 1;
                    """, cancellationToken);
            }
            var now = clock.GetUtcNow().UtcDateTime;
            var actor = await iamStore.GetAuthorizationSnapshotAsync(actorUserId, cancellationToken);
            var session = await dbContext.UserSessions.SingleOrDefaultAsync(
                s => s.UserId == actorUserId && s.Id == actorSessionId, cancellationToken);
            if (actor is null || !actor.Permissions.Contains(PlatformPermissionKeys.Access) || session?.IsActive(now) != true)
                throw new PlatformException(PlatformErrorCodes.AccessDenied, "دسترسی به مدیریت MFA مجاز نیست.");
            var subject = targetUserId ?? actorUserId;
            PlatformAuthorizationSnapshot? target = null;
            if (targetUserId.HasValue)
            {
                if (subject == actorUserId || !actor.Roles.Contains(PlatformRoleKeys.PlatformOwner) ||
                    !actor.Permissions.Contains(PlatformPermissionKeys.RolesManage))
                    throw new PlatformException(PlatformErrorCodes.AccessDenied, "عملیات مجاز نیست.");
                if (!await dbContext.PlatformMfaCredentials.AnyAsync(c => c.UserId == actorUserId &&
                        c.ProtectedSecret != null && c.ActivatedAtUtc != null, cancellationToken))
                    throw new PlatformException(PlatformErrorCodes.MfaRequired, "تأیید MFA الزامی است.");
                target = await iamStore.GetAuthorizationSnapshotAsync(subject, cancellationToken);
                if (target is null || !target.Permissions.Contains(PlatformPermissionKeys.Access))
                    throw new PlatformException(PlatformErrorCodes.AccessDenied, "عملیات مجاز نیست.");
            }
            var credential = await dbContext.PlatformMfaCredentials.Include(c => c.RecoveryCodes)
                .SingleOrDefaultAsync(c => c.UserId == subject, cancellationToken);
            var isNew = credential is null;
            credential ??= PlatformMfaCredential.Create(subject);
            var sessions = await dbContext.UserSessions.Where(s => s.UserId == subject).ToArrayAsync(cancellationToken);
            var state = new PlatformMfaState(actor, session!, credential, sessions, target);
            result = operation(state);
            if (isNew && (credential.PendingProtectedSecret is not null || credential.IsActive || credential.FailedAttempts > 0))
                dbContext.PlatformMfaCredentials.Add(credential);
            // کلید Guid کدها در Domain ساخته می‌شود؛ EF نباید آن‌ها را row موجود فرض کند.
            dbContext.PlatformMfaRecoveryCodes.AddRange(credential.RecoveryCodes.Where(code => code.RowVersion.Length == 0));
            dbContext.AdminAuditLogs.AddRange(state.AuditEntries);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            error = state.ErrorCode;
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException || exception is SqlException { Number: 1205 or 51004 })
        {
            throw new PlatformException(PlatformErrorCodes.AssignmentConflict, "عملیات هم‌زمان تغییر کرد؛ مجدداً تلاش کنید.");
        }
        // تلاش ناموفق و audit باید پیش از پاسخ خطا commit شده باشند.
        if (error is not null) throw new PlatformException(error, "تأیید امنیتی نامعتبر، منقضی یا در این نشست مجاز نیست.");
        return result;
    }
}
