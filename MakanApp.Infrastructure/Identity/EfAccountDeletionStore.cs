using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Identity;

public sealed class EfAccountDeletionStore(MakanDbContext db, CommercialActorAccess actors, TimeProvider clock,
    AccountDeletionOptions options, IPlatformPermissionResolver permissions) : IAccountDeletionStore
{
    public async Task<AccountDeletionPage> PageAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.AccountDeletionRequests.AsNoTracking(); var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.RequestedAtUtc).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(r => new AccountDeletionQueueItem(r.UserId, Map(r))).ToArray(), total, page, pageSize);
    }
    public async Task<AccountDeletionStatusResult> RequestAsync(Guid userId, Guid sessionId, Guid operation, string correlationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={userId}").AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        await actors.EnsureAsync(userId, sessionId, ct);
        var request = await db.AccountDeletionRequests.SingleOrDefaultAsync(r => r.UserId == userId, ct);
        if (request is null)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            request = AccountDeletionRequest.Create(userId, sessionId, operation, now); db.AccountDeletionRequests.Add(request);
            foreach (var step in Enum.GetValues<AccountDeletionStepKind>()) db.AccountDeletionSteps.Add(AccountDeletionStep.Create(request.Id, step));
            db.AdminAuditLogs.Add(AdminAuditLog.Create(userId, null, sessionId, "privacy.DeletionRequested", "AccountDeletionRequest", request.Id.ToString(), null,
                "درخواست صریح حذف حساب ثبت شد؛ اجرا فقط با تنظیم مصوب است", now, correlationId, AdminAuditResult.Succeeded, null, null));
            if (options.ExecutionEnabled)
            {
                request.Schedule(options.ScheduleAt(now));
                db.AdminAuditLogs.Add(AdminAuditLog.Create(userId, null, sessionId, "privacy.DeletionScheduled", "AccountDeletionRequest", request.Id.ToString(), null,
                    AccountDeletionRequest.CurrentPolicyVersion, now, "account-deletion-" + request.Id.ToString("N"), AdminAuditResult.Succeeded, null, null));
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return Map(request);
    }
    public async Task<AccountDeletionStatusResult?> StatusAsync(Guid userId, CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId, ct);
        return request is null ? null : Map(request);
    }
    public async Task<AccountDeletionDetail?> DetailAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null) return null;
        var steps = await db.AccountDeletionSteps.AsNoTracking().Where(s => s.RequestId == requestId).OrderBy(s => s.Step).ToArrayAsync(ct);
        return new(new(request.UserId, Map(request)), steps.Select(s => new AccountDeletionStepResult(s.Step.ToString(), s.Status.ToString(),
            s.AttemptCount, s.StartedAtUtc, s.CompletedAtUtc, s.FailureCode)).ToArray());
    }

    public async Task<AccountDeletionStatusResult> RetryAsync(Guid userId, Guid sessionId, Guid requestId, RetryAccountDeletionCommand command, CancellationToken ct)
    {
        if (!options.ExecutionEnabled) throw new IdentityException(AccountDeletionErrorCodes.ExecutionDisabled, "اجرای حذف حساب فعال نیست.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.AccountDeletionRequests.FromSqlInterpolated($"SELECT * FROM [identity].[AccountDeletionRequests] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={requestId}").SingleOrDefaultAsync(ct)
            ?? throw new IdentityException(AccountDeletionErrorCodes.NotFound, "درخواست قابل دسترس نیست.");
        await actors.EnsureAsync(userId, sessionId, ct);
        if (!await permissions.HasPermissionAsync(userId, PlatformPermissionKeys.PrivacyRequestsManage, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری مدیریت حریم خصوصی لازم است.");
        var correlation = "account-deletion-retry-" + command.ClientOperationId.ToString("N"); var target = request.Id.ToString("D");
        // همان Audit اتمیک، receipt عملیات ادامه است؛ retry قدیمی نیز deadline یا مراحل موفق را عوض نمی‌کند.
        if (await db.AdminAuditLogs.AnyAsync(a => a.Action == "privacy.DeletionResumed" && a.TargetId == target && a.CorrelationId == correlation, ct))
        { await tx.CommitAsync(ct); return Map(request); }
        byte[] version;
        try { version = Convert.FromBase64String(command.ExpectedVersion); }
        catch (FormatException) { throw new IdentityException(AccountDeletionErrorCodes.Conflict, "نسخهٔ درخواست معتبر نیست."); }
        if (request.Status != AccountDeletionStatus.NeedsAttention || !request.RowVersion.SequenceEqual(version))
            throw new IdentityException(AccountDeletionErrorCodes.Conflict, "وضعیت یا نسخهٔ درخواست تغییر کرده است.");
        request.Resume(clock.GetUtcNow().UtcDateTime);
        db.AdminAuditLogs.Add(AdminAuditLog.Create(userId, null, sessionId, "privacy.DeletionResumed", "AccountDeletionRequest", target, null,
            AccountDeletionRequest.CurrentPolicyVersion, clock.GetUtcNow().UtcDateTime, correlation, AdminAuditResult.Succeeded, null, null));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(request);
    }

    private AccountDeletionStatusResult Map(AccountDeletionRequest r) => new(r.Id, r.RequestedAtUtc, r.Status.ToString(), options.ExecutionEnabled, r.ScheduledExecutionAtUtc)
    {
        PolicyVersion = r.PolicyVersion, StartedAtUtc = r.StartedAtUtc, CompletedAtUtc = r.CompletedAtUtc, AttemptCount = r.AttemptCount,
        SafeFailureCode = r.FailureCode, FailureStep = r.FailureStep?.ToString(), Version = Convert.ToBase64String(r.RowVersion)
    };
}
