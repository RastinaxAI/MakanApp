using System.Data;
using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Notification;

public sealed partial class EfNotificationPreferenceStore
{
    public async Task<PushDeviceRegistration> UpsertAsync(Guid userId, Guid sessionId, RegisterPushDeviceCommand command,
        byte[] tokenHash, DateTime nowUtc, CancellationToken cancellationToken)
    {
        for (var retry = 0; ; retry++)
        {
            try
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                if (!await IsActiveSessionAsync(sessionId, userId, nowUtc, cancellationToken))
                    throw new NotificationPreferenceException(NotificationPreferenceErrorCodes.DeviceNotFound, "نشست فعال ثبت دستگاه پیدا نشد.");
                var registration = await dbContext.PushDeviceRegistrations
                    .FromSqlInterpolated($"SELECT * FROM [notification].[PushDeviceRegistrations] WITH (UPDLOCK, HOLDLOCK) WHERE [TokenHash] = {tokenHash} AND [RevokedAtUtc] IS NULL")
                    .SingleOrDefaultAsync(cancellationToken);
                if (registration is not null && registration.UserId != userId)
                    throw DeviceConflict();
                if (registration is null)
                {
                    if (command.ExpectedVersion is not null) throw DeviceConflict();
                    registration = PushDeviceRegistration.Create(userId, sessionId, command.Platform, command.PushToken, tokenHash, nowUtc);
                    dbContext.PushDeviceRegistrations.Add(registration);
                }
                else
                {
                    var changingWebKeys = (registration.Platform == PushDevicePlatform.Web || command.Platform == PushDevicePlatform.Web) &&
                        (registration.Platform != command.Platform || registration.WebPushP256dh != command.P256dh || registration.WebPushAuth != command.Auth);
                    if (changingWebKeys && command.ExpectedVersion != Convert.ToBase64String(registration.RowVersion))
                        throw new NotificationPreferenceException(NotificationPreferenceErrorCodes.ConcurrencyConflict,
                            "برای تغییر کلید subscription نسخه جاری دستگاه را دریافت کنید.");
                    registration.Refresh(sessionId, command.Platform, command.PushToken, tokenHash, nowUtc);
                }
                registration.SetWebPushKeys(command.P256dh, command.Auth);
                await SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return registration;
            }
            catch (Exception exception) when (retry < 3 &&
                (exception is SqlException { Number: 1205 } || exception.InnerException is SqlException { Number: 1205 }))
            {
                dbContext.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(20 * (retry + 1)), cancellationToken);
            }
        }
    }

    public async Task<PushDevicePageResult> GetPageAsync(Guid userId, PushDeviceListQuery query, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var registrations = dbContext.PushDeviceRegistrations.AsNoTracking().Where(r => r.UserId == userId &&
            (query.IncludeRevoked || r.RevokedAtUtc == null));
        var total = await registrations.CountAsync(cancellationToken);
        var rows = await registrations.OrderByDescending(r => r.LastSeenAtUtc).ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(r => new
            {
                Registration = r,
                IsActive = r.RevokedAtUtc == null && dbContext.UserSessions.Any(s => s.Id == r.UserSessionId && s.UserId == userId &&
                    s.RevokedAtUtc == null && s.ExpiresAtUtc > nowUtc && s.AbsoluteExpiresAtUtc > nowUtc) &&
                    dbContext.Users.Any(u => u.Id == userId && u.AccountStatus == UserAccountStatus.Active)
            }).ToArrayAsync(cancellationToken);
        return new(rows.Select(row => new PushDeviceListItem(row.Registration.Id, row.Registration.Platform,
            DateTime.SpecifyKind(row.Registration.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(row.Registration.LastSeenAtUtc, DateTimeKind.Utc),
            row.Registration.RevokedAtUtc.HasValue ? DateTime.SpecifyKind(row.Registration.RevokedAtUtc.Value, DateTimeKind.Utc) : null,
            row.IsActive, Convert.ToBase64String(row.Registration.RowVersion))).ToArray(), query.Page, query.PageSize, total);
    }

    private static NotificationPreferenceException DeviceConflict() =>
        new(NotificationPreferenceErrorCodes.DeviceConflict, "این دستگاه در حال حاضر قابل ثبت نیست.");
}
