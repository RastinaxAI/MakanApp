using System.Security.Cryptography;
using System.Text;
using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public sealed class PushDeviceService(
    IPushDeviceStore store,
    TimeProvider timeProvider,
    PushDeviceOptions options) : IPushDeviceService
{
    public Task<PushDeviceRegistrationResult> RegisterWebPushAsync(Guid userId, Guid sessionId,
        RegisterWebPushCommand command, CancellationToken cancellationToken) => RegisterAsync(userId, sessionId,
            new RegisterPushDeviceCommand(PushDevicePlatform.Web, command.Endpoint, command.P256dh, command.Auth, command.ExpectedVersion), cancellationToken);

    public async Task<PushDevicePageResult> GetPageAsync(Guid userId, Guid sessionId, PushDeviceListQuery query, CancellationToken cancellationToken)
    {
        if (query.Page is < 1 or > 100_000 || query.PageSize is < 1 or > 100)
            throw new NotificationPreferenceException(NotificationPreferenceErrorCodes.Invalid, "صفحه‌بندی دستگاه‌ها معتبر نیست.");
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!await store.IsActiveSessionAsync(sessionId, userId, nowUtc, cancellationToken))
            throw new NotificationPreferenceException(NotificationPreferenceErrorCodes.DeviceNotFound, "نشست فعال پیدا نشد.");
        return await store.GetPageAsync(userId, query, nowUtc, cancellationToken);
    }

    public async Task<PushDeviceRegistrationResult> RegisterAsync(
        Guid userId,
        Guid sessionId,
        RegisterPushDeviceCommand command,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!await store.IsActiveSessionAsync(sessionId, userId, nowUtc, cancellationToken))
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.DeviceNotFound,
                "نشست فعال برای ثبت دستگاه پیدا نشد.");
        }

        try
        {
            if (command.ExpectedVersion is not null)
            {
                var version = new byte[8];
                if (!Convert.TryFromBase64String(command.ExpectedVersion, version, out var length) || length != 8)
                    throw new ArgumentException("نسخه دستگاه معتبر نیست.");
            }
            var normalized = command with { PushToken = command.PushToken?.Trim() ?? string.Empty };
            if (command.Platform == PushDevicePlatform.Web)
            {
                if (options.AllowedWebPushHosts.Count == 0)
                    throw new NotificationPreferenceException(NotificationPreferenceErrorCodes.PushUnavailable, "مقصدهای مجاز Web Push هنوز پیکربندی نشده‌اند.");
                var endpoint = WebPushSubscriptionValidation.ValidateEndpoint(normalized.PushToken, options.AllowedWebPushHosts);
                var keys = WebPushSubscriptionValidation.ValidateKeys(command.P256dh, command.Auth);
                normalized = normalized with { PushToken = endpoint.AbsoluteUri, P256dh = keys.P256dh, Auth = keys.Auth };
            }
            else if (command.P256dh is not null || command.Auth is not null)
                throw new ArgumentException("کلید Web Push برای دستگاه موبایل مجاز نیست.");
            var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.PushToken));
            var registration = await store.UpsertAsync(userId, sessionId, normalized, tokenHash, nowUtc, cancellationToken);
            return ToResult(registration);
        }
        catch (NotificationPreferenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.Invalid,
                "اطلاعات ثبت دستگاه معتبر نیست.");
        }
    }

    public async Task RevokeAsync(
        Guid userId,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        var registration = await store.GetForUpdateAsync(
            registrationId, userId, cancellationToken)
            ?? throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.DeviceNotFound,
                "دستگاه Push پیدا نشد.");
        registration.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static PushDeviceRegistrationResult ToResult(PushDeviceRegistration registration) =>
        new(registration.Id, registration.Platform, DateTime.SpecifyKind(registration.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(registration.LastSeenAtUtc, DateTimeKind.Utc),
            Convert.ToBase64String(registration.RowVersion));
}
