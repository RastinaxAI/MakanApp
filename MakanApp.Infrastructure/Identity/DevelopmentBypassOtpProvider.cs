using MakanApp.Application.Identity;

namespace MakanApp.Infrastructure.Identity;

public sealed class DevelopmentBypassOtpProvider(DevelopmentOtpBypassOptions options) : IOtpDeliveryProvider
{
    public Task<OtpDeliveryResult> RequestOtpAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Mode != OtpMode.DevelopmentBypass || !options.TestUsers.ContainsKey(normalizedPhoneNumber))
            throw new IdentityException(IdentityErrorCodes.OtpInvalid, "کد یک‌بارمصرف معتبر نیست.");
        // هیچ HTTP، SMS، log یا ذخیرهٔ plaintext انجام نمی‌شود؛ hash توسط مسیر عادی ساخته می‌شود.
        return Task.FromResult(new OtpDeliveryResult(options.DevelopmentCode, "DevelopmentBypass"));
    }
}
