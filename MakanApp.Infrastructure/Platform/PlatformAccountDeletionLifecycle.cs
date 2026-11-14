using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed class PlatformAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock) : IPlatformAccountDeletionLifecycle
{
    public async Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        // همان ردیف Role با عملیات IAM قفل می‌شود؛ دو حذف هم‌زمان نمی‌توانند آخرین Owner را حذف کنند.
        _ = await db.PlatformRoles.FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoles] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={PlatformCatalogIds.PlatformOwnerRole}")
            .AsNoTracking().SingleAsync(ct);
        if (await db.PlatformRoleAssignments.AnyAsync(a => a.UserId == context.UserId && a.RoleId == PlatformCatalogIds.PlatformOwnerRole &&
                a.Status == PlatformRoleAssignmentStatus.Active && a.EndedAtUtc == null, ct) &&
            !await (from a in db.PlatformRoleAssignments join u in db.Users on a.UserId equals u.Id
                where a.UserId != context.UserId && a.RoleId == PlatformCatalogIds.PlatformOwnerRole &&
                    a.Status == PlatformRoleAssignmentStatus.Active && a.EndedAtUtc == null && u.AccountStatus == UserAccountStatus.Active
                select a.Id).AnyAsync(ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.LastPlatformOwner);
    }
    public async Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var roles = await db.PlatformRoleAssignments.Where(a => a.UserId == context.UserId && a.EndedAtUtc == null).ToArrayAsync(ct);
        foreach (var role in roles) role.End(context.UserId, clock.GetUtcNow().UtcDateTime);
    }
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.PlatformRoleAssignments.AnyAsync(a => a.UserId == context.UserId && a.Status == PlatformRoleAssignmentStatus.Active && a.EndedAtUtc == null, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }
}
