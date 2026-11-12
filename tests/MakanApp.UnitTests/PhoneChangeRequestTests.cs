using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class PhoneChangeRequestTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
    private static PhoneChangeRequest Request() => PhoneChangeRequest.Prepare(Guid.Parse("c50b5053-76f2-49d5-a885-877657c9caa9"), Guid.NewGuid(),
        UserCredential.CreateMobile(Guid.Parse("c50b5053-76f2-49d5-a885-877657c9caa9"), "+989390000001", Now), Guid.NewGuid(), "+989390000002", Now, TimeSpan.FromMinutes(5), 2);
    [Fact]
    public void OnlyRealHashCanCompleteAndArbitraryCodeConsumesAttempts()
    {
        var r = Request(); var hash = Enumerable.Repeat((byte)1, 32).ToArray(); r.Ready(hash, new byte[32], Now);
        Assert.Equal(OtpVerificationResult.Invalid, r.Check(new byte[32], Now));
        Assert.Equal(OtpVerificationResult.Valid, r.Check(hash, Now)); r.Complete(Now);
        Assert.Equal(PhoneChangeState.Completed, r.State); Assert.Equal(Now, r.CompletedAtUtc);
    }
    [Fact]
    public void ExpiredAndSupersededRequestsCannotChangePhone()
    {
        var r = Request(); r.Ready(new byte[32], new byte[32], Now);
        Assert.Equal(OtpVerificationResult.Expired, r.Check(new byte[32], Now.AddMinutes(5)));
        r.Supersede(); Assert.Equal(OtpVerificationResult.Invalid, r.Check(new byte[32], Now));
    }
    [Fact]
    public void AttemptLimitIsFinalEvenIfCorrectCodeIsLaterSupplied()
    {
        var r = Request(); var hash = Enumerable.Repeat((byte)1, 32).ToArray(); r.Ready(hash, new byte[32], Now);
        Assert.Equal(OtpVerificationResult.Invalid, r.Check(new byte[32], Now));
        Assert.Equal(OtpVerificationResult.TooManyAttempts, r.Check(new byte[32], Now));
        Assert.Equal(OtpVerificationResult.TooManyAttempts, r.Check(hash, Now));
    }
}
