using System.Security.Cryptography;

namespace MakanApp.Domain.Identity;

public enum PhoneChangeState { Preparing = 1, Ready = 2, Completed = 3, Superseded = 4 }

public sealed class PhoneChangeRequest
{
    private PhoneChangeRequest() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid CredentialId { get; private set; }
    public byte[] CredentialVersion { get; private set; } = [];
    public Guid ClientOperationId { get; private set; }
    public string TargetPhone { get; private set; } = string.Empty;
    public PhoneChangeState State { get; private set; }
    public byte[] CodeHash { get; private set; } = [];
    public byte[] Salt { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }
    public int MaxFailedAttempts { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PhoneChangeRequest Prepare(Guid user, Guid session, UserCredential credential, Guid operation,
        string target, DateTime now, TimeSpan lifetime, int attempts)
    {
        if (user == Guid.Empty || session == Guid.Empty || operation == Guid.Empty || credential.UserId != user ||
            credential.Kind != CredentialKind.MobilePhone || string.IsNullOrWhiteSpace(target) || target == credential.NormalizedIdentifier ||
            now.Kind != DateTimeKind.Utc || lifetime <= TimeSpan.Zero || attempts < 1) throw new ArgumentException("درخواست تغییر شماره معتبر نیست.");
        return new() { Id = Guid.NewGuid(), UserId = user, SessionId = session, CredentialId = credential.Id,
            CredentialVersion = credential.RowVersion.ToArray(), ClientOperationId = operation, TargetPhone = target,
            State = PhoneChangeState.Preparing, CreatedAtUtc = now, ExpiresAtUtc = now + lifetime, MaxFailedAttempts = attempts };
    }
    public void Ready(byte[] hash, byte[] salt, DateTime now)
    {
        if (State != PhoneChangeState.Preparing || now >= ExpiresAtUtc || hash.Length != 32 || salt.Length < 16)
            throw new InvalidOperationException("درخواست قابل آماده‌سازی نیست.");
        CodeHash = hash.ToArray(); Salt = salt.ToArray(); State = PhoneChangeState.Ready;
    }
    public OtpVerificationResult Check(byte[] hash, DateTime now)
    {
        if (State is PhoneChangeState.Preparing or PhoneChangeState.Superseded) return OtpVerificationResult.Invalid;
        if (FailedAttempts >= MaxFailedAttempts) return OtpVerificationResult.TooManyAttempts;
        if (State != PhoneChangeState.Completed && now >= ExpiresAtUtc) return OtpVerificationResult.Expired;
        if (!CryptographicOperations.FixedTimeEquals(CodeHash, hash))
        { FailedAttempts++; return FailedAttempts >= MaxFailedAttempts ? OtpVerificationResult.TooManyAttempts : OtpVerificationResult.Invalid; }
        return OtpVerificationResult.Valid;
    }
    public void Complete(DateTime now)
    {
        if (State != PhoneChangeState.Ready || now >= ExpiresAtUtc) throw new InvalidOperationException("درخواست قابل تأیید نیست.");
        State = PhoneChangeState.Completed; CompletedAtUtc = now;
    }
    public void Supersede() { if (State is PhoneChangeState.Preparing or PhoneChangeState.Ready) State = PhoneChangeState.Superseded; }
}
