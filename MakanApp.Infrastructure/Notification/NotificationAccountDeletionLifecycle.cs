using MakanApp.Application.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Notification;

public sealed class NotificationAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock) : INotificationAccountDeletionLifecycle
{
    public async Task RevokeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var devices = await db.PushDeviceRegistrations.Where(d => d.UserId == context.UserId && d.RevokedAtUtc == null).ToArrayAsync(ct);
        foreach (var device in devices) device.Revoke(clock.GetUtcNow().UtcDateTime);
    }
    public Task PurgePreferencesAsync(AccountDeletionExecutionContext context, CancellationToken ct) =>
        db.NotificationPreferences.Where(p => p.UserId == context.UserId).ExecuteDeleteAsync(ct);
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.PushDeviceRegistrations.AnyAsync(d => d.UserId == context.UserId && d.RevokedAtUtc == null, ct) ||
            await db.NotificationPreferences.AnyAsync(p => p.UserId == context.UserId, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }
}
