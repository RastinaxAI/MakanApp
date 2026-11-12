using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Identity;

public sealed class EfAccountDeletionStore(MakanDbContext db, CommercialActorAccess actors, TimeProvider clock) : IAccountDeletionStore
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
            db.AdminAuditLogs.Add(AdminAuditLog.Create(userId, null, sessionId, "privacy.DeletionRequested", "AccountDeletionRequest", request.Id.ToString(), null,
                "درخواست ثبت شد؛ اجرا به سیاست مصوب نگه‌داری داده نیاز دارد", now, correlationId, AdminAuditResult.Succeeded, null, null));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return Map(request);
    }
    public async Task<AccountDeletionStatusResult?> StatusAsync(Guid userId, CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId, ct);
        return request is null ? null : Map(request);
    }
    private static AccountDeletionStatusResult Map(AccountDeletionRequest r) => new(r.Id, r.RequestedAtUtc, r.PolicyStatus, false, null);
}
