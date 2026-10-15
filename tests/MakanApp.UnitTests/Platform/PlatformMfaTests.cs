using System.Text;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;
using Xunit;

namespace MakanApp.UnitTests.Platform;

public sealed class PlatformMfaTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void PendingEnrollmentCannotActivateFromAnotherSessionOrAfterExpiry()
    {
        var credential = PlatformMfaCredential.Create(Guid.NewGuid());
        var session = Guid.NewGuid();
        var id = credential.BeginEnrollment("protected-test-material", session, Now.AddMinutes(10));
        Assert.False(credential.IsActive);
        Assert.Throws<InvalidOperationException>(() => credential.Confirm(id, Guid.NewGuid(), 123, Now));
        Assert.Throws<InvalidOperationException>(() => credential.Confirm(id, session, 123, Now.AddMinutes(10)));
        credential.Confirm(id, session, 123, Now);
        Assert.True(credential.IsActive);
        Assert.Null(credential.PendingProtectedSecret);
        Assert.False(credential.AcceptTimeStep(123));
        Assert.False(credential.AcceptTimeStep(122));
        Assert.True(credential.AcceptTimeStep(124));
    }
    [Fact]
    public void ResetInvalidatesActiveSecretPendingEnrollmentAndRecoveryCodes()
    {
        var credential = ActiveCredential();
        credential.ReplaceRecoveryCodes([new string('A', 64)], Now);
        credential.Reset();
        Assert.False(credential.IsActive);
        Assert.Null(credential.ProtectedSecret);
        Assert.Null(credential.EnrollmentId);
        Assert.Empty(credential.RecoveryCodes);
    }
    [Fact]
    public void RecoveryCodeCanOnlyBeConsumedOnceAndRegenerationReplacesOldHashes()
    {
        var credential = ActiveCredential();
        credential.ReplaceRecoveryCodes([new string('A', 64)], Now);
        var code = Assert.Single(credential.RecoveryCodes);
        code.Consume(Now);
        Assert.Throws<InvalidOperationException>(() => code.Consume(Now));
        credential.ReplaceRecoveryCodes([new string('B', 64)], Now);
        Assert.NotEqual(code.Id, Assert.Single(credential.RecoveryCodes).Id);
    }
    [Fact]
    public void LockoutSurvivesNewEnrollmentAndExpiresOnServerTime()
    {
        var credential = PlatformMfaCredential.Create(Guid.NewGuid());
        for (var i = 0; i < 5; i++) credential.RecordFailure(Now, 5, TimeSpan.FromMinutes(5));
        Assert.True(credential.IsLocked(Now));
        credential.BeginEnrollment("protected-test-material", Guid.NewGuid(), Now.AddMinutes(10));
        Assert.True(credential.IsLocked(Now));
        Assert.False(credential.IsLocked(Now.AddMinutes(5)));
    }
    [Fact]
    public void ProtectedTotpInteroperatesWithRfcVectorAndRejectsOutsideSkewWindow()
    {
        var provider = new EphemeralDataProtectionProvider();
        var crypto = new TotpMfaCryptography(provider);
        var user = Guid.NewGuid();
        // این secret بردار عمومی RFC است، نه credential واقعی محیط.
        var secret = Base32Encoding.ToString(Encoding.ASCII.GetBytes("12345678901234567890"));
        var protectedSecret = crypto.Protect(user, secret);
        Assert.True(crypto.Verify(user, protectedSecret, "287082", DateTime.UnixEpoch.AddSeconds(59), out var step));
        Assert.Equal(1, step);
        Assert.False(crypto.Verify(user, protectedSecret, "287082", DateTime.UnixEpoch.AddSeconds(150), out _));
        Assert.Throws<PlatformException>(() => crypto.Verify(Guid.NewGuid(), protectedSecret, "287082", DateTime.UnixEpoch.AddSeconds(59), out _));
    }
    [Fact]
    public void RecoveryCodesHaveHighEntropyAndUserBoundHashAndMissingProtectorFailsClosed()
    {
        var crypto = new TotpMfaCryptography(null);
        var user = Guid.NewGuid();
        var code = crypto.GenerateRecoveryCode();
        Assert.Equal(40, code.Length);
        var hash = crypto.HashRecoveryCode(user, code);
        Assert.NotEqual(code, hash);
        Assert.True(crypto.MatchesRecoveryCode(user, code, hash));
        Assert.False(crypto.MatchesRecoveryCode(Guid.NewGuid(), code, hash));
        Assert.Throws<PlatformException>(() => crypto.Protect(user, crypto.GenerateSecret()));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void UnsafeStepUpLifetimeIsRejected(int minutes) =>
        Assert.Throws<InvalidOperationException>(() => new BackofficeSecurityOptions { StepUpLifetimeMinutes = minutes }.Validate());

    private static PlatformMfaCredential ActiveCredential()
    {
        var credential = PlatformMfaCredential.Create(Guid.NewGuid());
        var session = Guid.NewGuid();
        var id = credential.BeginEnrollment("protected-test-material", session, Now.AddMinutes(10));
        credential.Confirm(id, session, 1, Now);
        return credential;
    }
}
