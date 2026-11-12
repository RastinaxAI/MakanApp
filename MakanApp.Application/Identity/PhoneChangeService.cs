using MakanApp.Domain.Identity;

namespace MakanApp.Application.Identity;

public static class PhoneChangeErrorCodes
{
    public const string Invalid = "PHONE_CHANGE_INVALID";
    public const string Conflict = "PHONE_CHANGE_CONFLICT";
    public const string DeliveryPending = "PHONE_CHANGE_DELIVERY_UNCONFIRMED";
    public const string NotFound = "PHONE_CHANGE_NOT_FOUND";
}
public sealed record PhoneChangeActor(Guid UserId, Guid SessionId);
public sealed record RequestPhoneChangeCommand(Guid ClientOperationId, string NewPhoneNumber);
public sealed record VerifyPhoneChangeCommand(string Code);
public sealed record PhoneChangeResult(Guid RequestId, string State, DateTime ExpiresAtUtc, DateTime ResendAvailableAtUtc, DateTime? CompletedAtUtc);
public sealed record PreparedPhoneChange(PhoneChangeResult Result, bool Deliver);
public sealed record PhoneChangeVerification(PhoneChangeResult Result, OtpVerificationResult Verification);
public interface IPhoneChangeStore
{
    Task<PreparedPhoneChange> PrepareAsync(PhoneChangeActor actor, Guid operation, string phone, DateTime now, CancellationToken ct);
    Task<PhoneChangeResult> ReadyAsync(PhoneChangeActor actor, Guid id, byte[] hash, byte[] salt, DateTime now, CancellationToken ct);
    Task<PhoneChangeVerification> VerifyAsync(PhoneChangeActor actor, Guid id, string code, string correlationId, DateTime now, CancellationToken ct);
}

public sealed class PhoneChangeService(IPhoneChangeStore store, IOtpDeliveryProvider realDelivery,
    IIdentitySecurity security, OtpOptions options, TimeProvider clock)
{
    public const string RealDeliveryKey = "identity.phone-change.real";
    public async Task<PhoneChangeResult> RequestAsync(PhoneChangeActor actor, RequestPhoneChangeCommand command, CancellationToken ct)
    {
        if (command.ClientOperationId == Guid.Empty || !PhoneNumberNormalizer.TryNormalize(command.NewPhoneNumber, out var phone))
            throw Error(PhoneChangeErrorCodes.Invalid);
        var prepared = await store.PrepareAsync(actor, command.ClientOperationId, phone, clock.GetUtcNow().UtcDateTime, ct);
        if (!prepared.Deliver)
        {
            if (prepared.Result.State == nameof(PhoneChangeState.Preparing)) throw Error(PhoneChangeErrorCodes.DeliveryPending);
            return prepared.Result;
        }
        // این port فقط adapter واقعی دارد؛ Enabled/Mode مربوط به ورود در این مسیر خوانده نمی‌شود.
        // تماس شبکه خارج از transaction است؛ retry همان عملیات هرگز دوباره SMS نمی‌فرستد.
        var delivery = await realDelivery.RequestOtpAsync(phone, ct);
        var code = Normalize(delivery.VerificationCode);
        if (code is null) throw Error(IdentityErrorCodes.SmsProviderUnavailable);
        var salt = security.GenerateSalt();
        return await store.ReadyAsync(actor, prepared.Result.RequestId, Hash(security, prepared.Result.RequestId, code, salt), salt,
            clock.GetUtcNow().UtcDateTime, ct);
    }
    public async Task<PhoneChangeResult> VerifyAsync(PhoneChangeActor actor, Guid id, VerifyPhoneChangeCommand command, string correlationId, CancellationToken ct)
    {
        var code = Normalize(command.Code);
        if (id == Guid.Empty || code is null) throw Error(IdentityErrorCodes.OtpInvalid);
        var result = await store.VerifyAsync(actor, id, code, correlationId, clock.GetUtcNow().UtcDateTime, ct);
        if (result.Verification != OtpVerificationResult.Valid)
            throw Error(result.Verification switch { OtpVerificationResult.Expired => IdentityErrorCodes.OtpExpired,
                OtpVerificationResult.TooManyAttempts => IdentityErrorCodes.OtpTooManyAttempts, _ => IdentityErrorCodes.OtpInvalid });
        return result.Result;
    }
    public static byte[] Hash(IIdentitySecurity security, Guid request, string code, byte[] salt) =>
        security.HashOtp("\u001fMakanApp.PhoneChange.v1\u001f" + request.ToString("N") + "\u001f" + code, salt);
    private string? Normalize(string? value)
    {
        var code = value?.Trim();
        if (code is null || code.Length < options.MinimumCodeLength || code.Length > options.MaximumCodeLength) return null;
        var normalized = code.Select(PhoneNumberNormalizer.ToAsciiDigit).ToArray();
        return normalized.Any(char.IsControl) ? null : new string(normalized);
    }
    internal static IdentityException Error(string code) => new(code, "تغییر شماره انجام نشد؛ وضعیت درخواست و کد تأیید شمارهٔ جدید را بررسی کنید.");
}
