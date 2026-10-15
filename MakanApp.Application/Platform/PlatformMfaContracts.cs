using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Platform;

public sealed record PlatformMfaEnrollment(Guid EnrollmentId, string SetupUri, DateTime ExpiresAtUtc);
public sealed record PlatformMfaResult(bool Enrolled, bool SessionMfaVerified, DateTime? StepUpValidUntilUtc,
    PlatformMfaEnrollment? Enrollment = null, IReadOnlyCollection<string>? RecoveryCodes = null, bool EnrollmentPending = false);
public sealed record ConfirmPlatformMfaCommand(Guid EnrollmentId, string Code);
public sealed record VerifyPlatformMfaCommand(string Code);
public sealed record ResetPlatformMfaCommand(string Reason);
public sealed class BackofficeSecurityOptions
{
    public int StepUpLifetimeMinutes { get; init; } = 10;
    public int EnrollmentLifetimeMinutes { get; init; } = 10;
    public int MaximumFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 5;
    public const int RecoveryCodeCount = 10;
    public void Validate()
    {
        if (StepUpLifetimeMinutes is < 1 or > 30 || EnrollmentLifetimeMinutes is < 1 or > 30 ||
            MaximumFailedAttempts is < 1 or > 10 || LockoutMinutes is < 1 or > 60)
            throw new InvalidOperationException("محدوده تنظیمات امنیت Backoffice معتبر نیست.");
    }
}
public interface IPlatformMfaAssurance
{
    Task<bool> IsSatisfiedAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
public interface IPlatformMfaCryptography
{
    string GenerateSecret();
    string Protect(Guid userId, string secret);
    bool Verify(Guid userId, string protectedSecret, string code, DateTime nowUtc, out long timeStep);
    string GenerateRecoveryCode();
    string HashRecoveryCode(Guid userId, string code);
    bool MatchesRecoveryCode(Guid userId, string code, string hash);
}

// این context فقط واحد تراکنشی قابلیت MFA است؛ EF یا repository عمومی در قرارداد وجود ندارد.
public sealed class PlatformMfaState(
    PlatformAuthorizationSnapshot actor, UserSession session, PlatformMfaCredential credential,
    IReadOnlyCollection<UserSession> subjectSessions, PlatformAuthorizationSnapshot? target)
{
    public PlatformAuthorizationSnapshot Actor { get; } = actor;
    public UserSession Session { get; } = session;
    public PlatformMfaCredential Credential { get; } = credential;
    public IReadOnlyCollection<UserSession> SubjectSessions { get; } = subjectSessions;
    public PlatformAuthorizationSnapshot? Target { get; } = target;
    public List<AdminAuditLog> AuditEntries { get; } = [];
    public string? ErrorCode { get; set; }
}
public interface IPlatformMfaStore
{
    Task<PlatformMfaResult> ExecuteAsync(Guid actorUserId, Guid actorSessionId, Guid? targetUserId,
        Func<PlatformMfaState, PlatformMfaResult> operation, CancellationToken cancellationToken);
}
