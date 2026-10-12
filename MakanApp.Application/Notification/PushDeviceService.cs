using System.Security.Cryptography;
using System.Text;
using MakanApp.Domain.Notification;

namespace MakanApp.Application.Notification;

public sealed class PushDeviceService(
    IPushDeviceStore store,
    TimeProvider timeProvider) : IPushDeviceService
{
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

        var normalizedToken = command.PushToken?.Trim() ?? string.Empty;
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedToken));
        var existing = await store.GetActiveByTokenHashAsync(tokenHash, cancellationToken);
        try
        {
            if (existing is not null)
            {
                if (existing.UserId != userId)
                {
                    throw new NotificationPreferenceException(
                        NotificationPreferenceErrorCodes.DeviceConflict,
                        "این دستگاه در حال حاضر قابل ثبت نیست.");
                }

                existing.Refresh(sessionId, command.Platform, normalizedToken, tokenHash, nowUtc);
                await store.SaveChangesAsync(cancellationToken);
                return ToResult(existing);
            }

            var registration = PushDeviceRegistration.Create(
                userId, sessionId, command.Platform, normalizedToken, tokenHash, nowUtc);
            store.Add(registration);
            await store.SaveChangesAsync(cancellationToken);
            return ToResult(registration);
        }
        catch (NotificationPreferenceException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.Invalid,
                exception.Message);
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
        new(registration.Id, registration.Platform, registration.CreatedAtUtc, registration.LastSeenAtUtc);
}
