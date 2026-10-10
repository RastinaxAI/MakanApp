using MakanApp.Application.Identity;
using MakanApp.Infrastructure.Identity;

namespace MakanApp.IntegrationTests;

public sealed class SmsFailureSwitch
{
    private int _invocationCount;

    public bool Fail { get; set; }
    public int InvocationCount => Volatile.Read(ref _invocationCount);

    internal void RecordInvocation() => Interlocked.Increment(ref _invocationCount);
}

public sealed class FaultInjectingSmsSender(
    DevelopmentSmsSender inner,
    SmsFailureSwitch failureSwitch) : ISmsSender
{
    public Task SendOtpAsync(
        string normalizedPhoneNumber,
        Guid challengeId,
        string code,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        failureSwitch.RecordInvocation();
        if (failureSwitch.Fail)
        {
            throw new IdentityException(
                IdentityErrorCodes.SmsProviderUnavailable,
                "سرویس ارسال پیامک موقتاً در دسترس نیست.");
        }

        return inner.SendOtpAsync(
            normalizedPhoneNumber,
            challengeId,
            code,
            expiresAtUtc,
            cancellationToken);
    }
}
