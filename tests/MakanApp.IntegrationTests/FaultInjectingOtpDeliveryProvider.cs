using MakanApp.Application.Identity;
using MakanApp.Infrastructure.Identity;

namespace MakanApp.IntegrationTests;

public sealed class OtpProviderTestControl
{
    private int _invocationCount;
    private readonly Dictionary<string, string> _codes = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public bool Fail { get; set; }
    public string? OverrideVerificationCode { get; set; }
    public int InvocationCount => Volatile.Read(ref _invocationCount);

    internal void RecordInvocation() => Interlocked.Increment(ref _invocationCount);

    internal void RecordCode(string normalizedPhoneNumber, string code)
    {
        lock (_sync)
        {
            _codes[normalizedPhoneNumber] = code;
        }
    }

    public string GetLatestCode(string normalizedPhoneNumber)
    {
        lock (_sync)
        {
            return _codes.TryGetValue(normalizedPhoneNumber, out var code)
                ? code
                : throw new InvalidOperationException("Development OTP was not captured.");
        }
    }
}

public sealed class FaultInjectingOtpDeliveryProvider(
    DevelopmentOtpProvider inner,
    OtpProviderTestControl control) : IOtpDeliveryProvider
{
    public async Task<OtpDeliveryResult> RequestOtpAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken)
    {
        control.RecordInvocation();
        if (control.Fail)
        {
            throw new IdentityException(
                IdentityErrorCodes.SmsProviderUnavailable,
                "سرویس ارسال پیامک موقتاً در دسترس نیست.");
        }

        var result = control.OverrideVerificationCode is { } code
            ? new OtpDeliveryResult(code, "TestOverride")
            : await inner.RequestOtpAsync(normalizedPhoneNumber, cancellationToken);
        control.RecordCode(normalizedPhoneNumber, result.VerificationCode);
        return result;
    }
}
