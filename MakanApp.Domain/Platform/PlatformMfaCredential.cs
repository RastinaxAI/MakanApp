namespace MakanApp.Domain.Platform;

public sealed class PlatformMfaCredential
{
    private readonly List<PlatformMfaRecoveryCode> _recoveryCodes = [];
    private PlatformMfaCredential() { }
    public Guid UserId { get; private set; }
    public string? ProtectedSecret { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public string? PendingProtectedSecret { get; private set; }
    public Guid? EnrollmentId { get; private set; }
    public Guid? EnrollmentSessionId { get; private set; }
    public DateTime? EnrollmentExpiresAtUtc { get; private set; }
    public long? LastAcceptedTimeStep { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PlatformMfaRecoveryCode> RecoveryCodes => _recoveryCodes;
    public bool IsActive => ProtectedSecret is not null && ActivatedAtUtc.HasValue;

    public static PlatformMfaCredential Create(Guid userId) => userId == Guid.Empty
        ? throw new ArgumentException("کاربر MFA معتبر نیست.")
        : new() { UserId = userId };

    public Guid BeginEnrollment(string protectedSecret, Guid sessionId, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret) || sessionId == Guid.Empty || expiresAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("مشخصات ثبت MFA معتبر نیست.");
        PendingProtectedSecret = protectedSecret;
        EnrollmentId = Guid.NewGuid();
        EnrollmentSessionId = sessionId;
        EnrollmentExpiresAtUtc = expiresAtUtc;
        return EnrollmentId.Value;
    }

    public bool CanConfirm(Guid enrollmentId, Guid sessionId, DateTime nowUtc) =>
        PendingProtectedSecret is not null && EnrollmentId == enrollmentId && EnrollmentSessionId == sessionId &&
        EnrollmentExpiresAtUtc > nowUtc;

    public void Confirm(Guid enrollmentId, Guid sessionId, long timeStep, DateTime nowUtc)
    {
        if (!CanConfirm(enrollmentId, sessionId, nowUtc) || timeStep < 0 || nowUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("ثبت MFA قابل تأیید نیست.");
        ProtectedSecret = PendingProtectedSecret;
        ActivatedAtUtc = nowUtc;
        LastAcceptedTimeStep = timeStep;
        ClearPending();
        ClearFailures();
    }

    public bool AcceptTimeStep(long timeStep)
    {
        if (!IsActive || timeStep < 0 || timeStep <= LastAcceptedTimeStep) return false;
        LastAcceptedTimeStep = timeStep;
        ClearFailures();
        return true;
    }

    public bool IsLocked(DateTime nowUtc) => LockedUntilUtc > nowUtc;
    public void RecordFailure(DateTime nowUtc, int maximumAttempts, TimeSpan lockout)
    {
        if (maximumAttempts <= 0 || lockout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (LockedUntilUtc.HasValue && LockedUntilUtc <= nowUtc) ClearFailures();
        FailedAttempts++;
        if (FailedAttempts >= maximumAttempts) LockedUntilUtc = nowUtc.Add(lockout);
    }
    public void ClearFailures() { FailedAttempts = 0; LockedUntilUtc = null; }
    public void ReplaceRecoveryCodes(IEnumerable<string> hashes, DateTime nowUtc)
    {
        if (!IsActive) throw new InvalidOperationException("MFA فعال نیست.");
        var codes = hashes.Select(hash => PlatformMfaRecoveryCode.Create(UserId, hash, nowUtc)).ToArray();
        if (codes.Length == 0) throw new ArgumentException("کد بازیابی لازم است.");
        _recoveryCodes.Clear();
        _recoveryCodes.AddRange(codes);
    }
    public void Reset()
    {
        ProtectedSecret = null;
        ActivatedAtUtc = null;
        LastAcceptedTimeStep = null;
        ClearPending();
        ClearFailures();
        _recoveryCodes.Clear();
    }
    private void ClearPending()
    {
        PendingProtectedSecret = null;
        EnrollmentId = null;
        EnrollmentSessionId = null;
        EnrollmentExpiresAtUtc = null;
    }
}

public sealed class PlatformMfaRecoveryCode
{
    private PlatformMfaRecoveryCode() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static PlatformMfaRecoveryCode Create(Guid userId, string hash, DateTime nowUtc)
    {
        if (userId == Guid.Empty || hash.Length != 64 || !hash.All(Uri.IsHexDigit) || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("مشخصات کد بازیابی معتبر نیست.");
        return new() { Id = Guid.NewGuid(), UserId = userId, CodeHash = hash, CreatedAtUtc = nowUtc };
    }
    public void Consume(DateTime nowUtc)
    {
        if (ConsumedAtUtc.HasValue || nowUtc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("کد بازیابی قابل مصرف نیست.");
        ConsumedAtUtc = nowUtc;
    }
}
