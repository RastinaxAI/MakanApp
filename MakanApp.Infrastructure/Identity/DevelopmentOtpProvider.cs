using System.Collections.Concurrent;
using System.Security.Cryptography;
using MakanApp.Application.Identity;

namespace MakanApp.Infrastructure.Identity;

public sealed record DevelopmentOtpMessage(
    string PhoneNumber,
    string Code);

public sealed class DevelopmentOtpStore
{
    private readonly ConcurrentDictionary<string, DevelopmentOtpMessage> _messages =
        new(StringComparer.Ordinal);

    public bool TryGet(string normalizedPhoneNumber, out DevelopmentOtpMessage? message) =>
        _messages.TryGetValue(normalizedPhoneNumber, out message);

    internal void Set(string normalizedPhoneNumber, DevelopmentOtpMessage message) =>
        _messages[normalizedPhoneNumber] = message;
}

public sealed class DevelopmentOtpProvider(DevelopmentOtpStore store) : IOtpDeliveryProvider
{
    public Task<OtpDeliveryResult> RequestOtpAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        store.Set(
            normalizedPhoneNumber,
            new DevelopmentOtpMessage(normalizedPhoneNumber, code));
        return Task.FromResult(new OtpDeliveryResult(code, "Development"));
    }
}

public sealed class UnavailableOtpDeliveryProvider : IOtpDeliveryProvider
{
    public Task<OtpDeliveryResult> RequestOtpAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken) =>
        throw new IdentityException(
            IdentityErrorCodes.SmsProviderUnavailable,
            "ارسال پیامک در این محیط پیکربندی نشده است.");
}
