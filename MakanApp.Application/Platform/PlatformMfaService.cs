using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Platform;

public sealed class PlatformMfaService(IPlatformMfaStore store, IPlatformMfaCryptography cryptography,
    BackofficeSecurityOptions options, TimeProvider timeProvider)
{
    public Task<PlatformMfaResult> GetStatusAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        store.ExecuteAsync(userId, sessionId, null, state => Result(state, Now()), ct);

    public Task<PlatformMfaResult> StartEnrollmentAsync(Guid userId, Guid sessionId, string correlationId, CancellationToken ct) =>
        store.ExecuteAsync(userId, sessionId, null, state =>
        {
            var now = Now();
            if (state.Credential.IsActive && !Fresh(state.Session, now))
                return Deny(state, PlatformErrorCodes.StepUpRequired, "platform.mfa.enrollment.denied", correlationId, now);
            if (state.Credential.IsLocked(now)) return Locked(state, correlationId, now);
            var secret = cryptography.GenerateSecret();
            var expires = now.AddMinutes(options.EnrollmentLifetimeMinutes);
            var id = state.Credential.BeginEnrollment(cryptography.Protect(userId, secret), sessionId, expires);
            Audit(state, "platform.mfa.enrollment.started", correlationId, now);
            var label = Uri.EscapeDataString("MakanApp:" + userId.ToString("N"));
            var uri = $"otpauth://totp/{label}?secret={secret}&issuer=MakanApp&algorithm=SHA1&digits=6&period=30";
            return Result(state, now) with { Enrollment = new(id, uri, expires) };
        }, ct);

    public Task<PlatformMfaResult> ConfirmAsync(Guid userId, Guid sessionId, ConfirmPlatformMfaCommand command,
        string correlationId, CancellationToken ct) => store.ExecuteAsync(userId, sessionId, null, state =>
        {
            var now = Now();
            if (state.Credential.IsLocked(now)) return Locked(state, correlationId, now);
            if (!state.Credential.CanConfirm(command.EnrollmentId, sessionId, now) ||
                !cryptography.Verify(userId, state.Credential.PendingProtectedSecret!, command.Code, now, out var step))
                return Invalid(state, correlationId, now);
            // تأیید مجدد MFA فعال نیز فقط با assurance تازه همان نشست مجاز است.
            if (state.Credential.IsActive && !Fresh(state.Session, now))
                return Deny(state, PlatformErrorCodes.StepUpRequired, "platform.mfa.enrollment.denied", correlationId, now);
            state.Credential.Confirm(command.EnrollmentId, sessionId, step, now);
            foreach (var session in state.SubjectSessions) session.ClearMfaAssurance();
            state.Session.RecordStepUp(AuthenticationAssuranceLevel.MultiFactor, now);
            var codes = ReplaceCodes(state, now);
            Audit(state, "platform.mfa.enrolled", correlationId, now);
            return Result(state, now) with { RecoveryCodes = codes };
        }, ct);

    public Task<PlatformMfaResult> VerifyAsync(Guid userId, Guid sessionId, VerifyPlatformMfaCommand command,
        bool recovery, string correlationId, CancellationToken ct) => store.ExecuteAsync(userId, sessionId, null, state =>
        {
            var now = Now();
            if (state.Credential.IsLocked(now)) return Locked(state, correlationId, now);
            if (!state.Credential.IsActive) return Invalid(state, correlationId, now);
            if (recovery)
            {
                var code = state.Credential.RecoveryCodes.FirstOrDefault(candidate => !candidate.ConsumedAtUtc.HasValue &&
                    cryptography.MatchesRecoveryCode(userId, command.Code, candidate.CodeHash));
                if (code is null) return Invalid(state, correlationId, now);
                code.Consume(now);
                state.Credential.ClearFailures();
            }
            else if (!cryptography.Verify(userId, state.Credential.ProtectedSecret!, command.Code, now, out var step) ||
                     !state.Credential.AcceptTimeStep(step)) return Invalid(state, correlationId, now);
            state.Session.RecordStepUp(AuthenticationAssuranceLevel.MultiFactor, now);
            Audit(state, recovery ? "platform.mfa.recovery.used" : "platform.mfa.verified", correlationId, now);
            return Result(state, now);
        }, ct);

    public Task<PlatformMfaResult> RegenerateRecoveryCodesAsync(Guid userId, Guid sessionId, string correlationId, CancellationToken ct) =>
        store.ExecuteAsync(userId, sessionId, null, state =>
        {
            var now = Now();
            if (!state.Credential.IsActive || !Fresh(state.Session, now))
                return Deny(state, PlatformErrorCodes.StepUpRequired, "platform.mfa.recovery.regenerate.denied", correlationId, now);
            var codes = ReplaceCodes(state, now);
            Audit(state, "platform.mfa.recovery.regenerated", correlationId, now);
            return Result(state, now) with { RecoveryCodes = codes };
        }, ct);

    public Task<PlatformMfaResult> ResetAsync(Guid userId, Guid sessionId, Guid targetUserId,
        ResetPlatformMfaCommand command, string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > AdminAuditLog.MaximumReasonLength)
            throw new PlatformException(PlatformErrorCodes.ReasonRequired, "دلیل معتبر الزامی است.");
        return store.ExecuteAsync(userId, sessionId, targetUserId, state =>
        {
            var now = Now();
            if (userId == targetUserId || !state.Actor.Roles.Contains(PlatformRoleKeys.PlatformOwner) ||
                !state.Actor.Permissions.Contains(PlatformPermissionKeys.RolesManage) || state.Target is null)
                return Deny(state, PlatformErrorCodes.AccessDenied, "platform.mfa.reset.denied", correlationId, now);
            if (!Fresh(state.Session, now))
                return Deny(state, PlatformErrorCodes.StepUpRequired, "platform.mfa.reset.denied", correlationId, now);
            state.Credential.Reset();
            foreach (var session in state.SubjectSessions) session.ClearMfaAssurance();
            Audit(state, "platform.mfa.reset", correlationId, now, command.Reason.Trim());
            // وضعیت subject برگردانده می‌شود، نه assurance یا داده امنیتی نشست‌های او.
            return new(false, false, null);
        }, ct);
    }

    private string[] ReplaceCodes(PlatformMfaState state, DateTime now)
    {
        var codes = Enumerable.Range(0, BackofficeSecurityOptions.RecoveryCodeCount).Select(_ => cryptography.GenerateRecoveryCode()).ToArray();
        state.Credential.ReplaceRecoveryCodes(codes.Select(code => cryptography.HashRecoveryCode(state.Credential.UserId, code)), now);
        return codes;
    }
    private bool Fresh(UserSession session, DateTime now) => session.IsActive(now) &&
        session.AssuranceLevel == AuthenticationAssuranceLevel.MultiFactor && session.StepUpAtUtc <= now &&
        session.StepUpAtUtc?.AddMinutes(options.StepUpLifetimeMinutes) > now;
    private PlatformMfaResult Result(PlatformMfaState state, DateTime now)
    {
        var verified = state.Credential.IsActive && state.Session.AssuranceLevel == AuthenticationAssuranceLevel.MultiFactor &&
                       state.Session.StepUpAtUtc <= now && state.Session.IsActive(now);
        return new(state.Credential.IsActive, verified, verified && Fresh(state.Session, now)
            ? state.Session.StepUpAtUtc!.Value.AddMinutes(options.StepUpLifetimeMinutes) : null,
            EnrollmentPending: state.Credential.PendingProtectedSecret is not null && state.Credential.EnrollmentExpiresAtUtc > now);
    }
    private PlatformMfaResult Invalid(PlatformMfaState state, string correlationId, DateTime now)
    {
        state.Credential.RecordFailure(now, options.MaximumFailedAttempts, TimeSpan.FromMinutes(options.LockoutMinutes));
        return Deny(state, state.Credential.IsLocked(now) ? PlatformErrorCodes.MfaRateLimited : PlatformErrorCodes.MfaInvalid,
            "platform.mfa.verification.denied", correlationId, now);
    }
    private PlatformMfaResult Locked(PlatformMfaState state, string correlationId, DateTime now) =>
        Deny(state, PlatformErrorCodes.MfaRateLimited, "platform.mfa.verification.denied", correlationId, now);
    private PlatformMfaResult Deny(PlatformMfaState state, string code, string action, string correlationId, DateTime now)
    {
        state.ErrorCode = code;
        Audit(state, action, correlationId, now, result: AdminAuditResult.Denied);
        return Result(state, now);
    }
    private static void Audit(PlatformMfaState state, string action, string correlationId, DateTime now,
        string reason = "مدیریت امنیت MFA پلتفرم", AdminAuditResult result = AdminAuditResult.Succeeded) =>
        state.AuditEntries.Add(AdminAuditLog.Create(state.Actor.UserId, state.Actor.PersonId, state.Session.Id, action,
            "PlatformMfa", state.Credential.UserId.ToString(), null, reason, now, correlationId, result, null, null));
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
