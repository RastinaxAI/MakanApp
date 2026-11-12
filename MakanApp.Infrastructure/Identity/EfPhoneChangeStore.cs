using System.Data;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Identity;

public sealed class EfPhoneChangeStore(MakanDbContext db, IIdentitySecurity security, OtpOptions options) : IPhoneChangeStore
{
    public Task<PreparedPhoneChange> PrepareAsync(PhoneChangeActor actor, Guid operation, string phone, DateTime now, CancellationToken ct) =>
        TransactionAsync(async () =>
        {
            await LockActorAsync(actor, now, ct);
            var replay = await db.PhoneChangeRequests.SingleOrDefaultAsync(r => r.UserId == actor.UserId && r.ClientOperationId == operation, ct);
            if (replay is not null)
            {
                if (replay.SessionId != actor.SessionId || replay.TargetPhone != phone) throw Error(PhoneChangeErrorCodes.Conflict);
                return new PreparedPhoneChange(Map(replay), false);
            }
            var recent = db.PhoneChangeRequests.Where(r => r.CreatedAtUtc >= now - options.RateLimitWindow);
            if (await recent.CountAsync(r => r.UserId == actor.UserId, ct) >= options.MaxRequestsPerWindow ||
                await recent.CountAsync(r => r.TargetPhone == phone, ct) >= options.MaxRequestsPerWindow ||
                await db.PhoneChangeRequests.AnyAsync(r => (r.UserId == actor.UserId || r.TargetPhone == phone) && r.CreatedAtUtc > now - options.ResendDelay, ct))
                throw Error(IdentityErrorCodes.OtpRateLimited);
            await EnsureTargetFreeAsync(phone, ct);
            var credential = await db.UserCredentials.SingleAsync(c => c.UserId == actor.UserId && c.Kind == CredentialKind.MobilePhone, ct);
            if (credential.NormalizedIdentifier == phone)
                throw Error(PhoneChangeErrorCodes.Conflict);
            var pending = await db.PhoneChangeRequests.Where(r => r.UserId == actor.UserId &&
                (r.State == PhoneChangeState.Ready || r.State == PhoneChangeState.Preparing)).ToArrayAsync(ct);
            foreach (var prior in pending) prior.Supersede();
            if (pending.Length > 0) await db.SaveChangesAsync(ct);
            var request = PhoneChangeRequest.Prepare(actor.UserId, actor.SessionId, credential, operation, phone, now, options.Lifetime, options.MaxFailedAttempts);
            db.PhoneChangeRequests.Add(request); await db.SaveChangesAsync(ct);
            return new PreparedPhoneChange(Map(request), true);
        }, ct);

    public Task<PhoneChangeResult> ReadyAsync(PhoneChangeActor actor, Guid id, byte[] hash, byte[] salt, DateTime now, CancellationToken ct) =>
        TransactionAsync(async () =>
        {
            await LockActorAsync(actor, now, ct); var request = await RequireAsync(actor, id, ct);
            await RequireCredentialAsync(request, ct);
            if (request.State != PhoneChangeState.Preparing || now >= request.ExpiresAtUtc) throw Error(PhoneChangeErrorCodes.Conflict);
            request.Ready(hash, salt, now); await db.SaveChangesAsync(ct); return Map(request);
        }, ct);

    public Task<PhoneChangeVerification> VerifyAsync(PhoneChangeActor actor, Guid id, string code, string correlationId, DateTime now, CancellationToken ct) =>
        TransactionAsync(async () =>
        {
            await LockActorAsync(actor, now, ct); var request = await RequireAsync(actor, id, ct);
            var verification = request.Check(PhoneChangeService.Hash(security, id, code, request.Salt), now);
            if (verification != OtpVerificationResult.Valid)
            {
                db.AdminAuditLogs.Add(Audit(actor, request, "identity.PhoneChangeVerificationFailed", now, correlationId, AdminAuditResult.Denied));
                await db.SaveChangesAsync(ct); return new PhoneChangeVerification(Map(request), verification);
            }
            if (request.State == PhoneChangeState.Completed) return new PhoneChangeVerification(Map(request), verification);
            await EnsureTargetFreeAsync(request.TargetPhone, ct);
            var credential = await RequireCredentialAsync(request, ct);
            // OTP ورودِ پیش از تغییر شماره نباید بعداً برای حساب قدیمی/جدید مصرف شود.
            var oldPhone = credential.NormalizedIdentifier;
            var challenges = await db.OtpChallenges.Where(c => (c.NormalizedPhoneNumber == oldPhone || c.NormalizedPhoneNumber == request.TargetPhone) &&
                c.ConsumedAtUtc == null && c.SupersededAtUtc == null).ToArrayAsync(ct);
            foreach (var challenge in challenges) challenge.Supersede(now);
            credential.ChangeVerifiedPhone(request.TargetPhone, now);
            var others = await db.UserSessions.Where(s => s.UserId == actor.UserId && s.Id != actor.SessionId).ToArrayAsync(ct);
            var sessionIds = others.Select(s => s.Id).ToArray();
            foreach (var session in others) session.Revoke(now);
            var refresh = await db.RefreshTokens.Where(t => sessionIds.Contains(t.SessionId) && t.RevokedAtUtc == null).ToArrayAsync(ct);
            foreach (var token in refresh) token.Revoke(now);
            request.Complete(now);
            var pending = await db.PhoneChangeRequests.Where(r => r.UserId == actor.UserId && r.Id != id &&
                (r.State == PhoneChangeState.Preparing || r.State == PhoneChangeState.Ready)).ToArrayAsync(ct);
            foreach (var prior in pending) prior.Supersede();
            db.AdminAuditLogs.Add(Audit(actor, request, "identity.PhoneChanged", now, correlationId, AdminAuditResult.Succeeded));
            await db.SaveChangesAsync(ct); return new PhoneChangeVerification(Map(request), OtpVerificationResult.Valid);
        }, ct);

    private async Task LockActorAsync(PhoneChangeActor actor, DateTime now, CancellationToken ct)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor.UserId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (user?.AccountStatus != UserAccountStatus.Active || !await db.UserSessions.AsNoTracking().AnyAsync(s => s.Id == actor.SessionId &&
            s.UserId == actor.UserId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now, ct))
            throw Error(IdentityErrorCodes.AuthRequired);
    }
    private async Task<PhoneChangeRequest> RequireAsync(PhoneChangeActor actor, Guid id, CancellationToken ct) =>
        await db.PhoneChangeRequests.SingleOrDefaultAsync(r => r.Id == id && r.UserId == actor.UserId && r.SessionId == actor.SessionId, ct)
        ?? throw Error(PhoneChangeErrorCodes.NotFound);
    private async Task<UserCredential> RequireCredentialAsync(PhoneChangeRequest request, CancellationToken ct)
    {
        var credential = await db.UserCredentials.SingleOrDefaultAsync(c => c.Id == request.CredentialId && c.UserId == request.UserId && c.Kind == CredentialKind.MobilePhone, ct);
        if (credential is null || !credential.RowVersion.SequenceEqual(request.CredentialVersion)) throw Error(PhoneChangeErrorCodes.Conflict);
        return credential;
    }
    private async Task EnsureTargetFreeAsync(string phone, CancellationToken ct)
    {
        // قفل range روی unique index، رقابت دو حساب یا ورود هم‌زمان به شمارهٔ مقصد را مهار می‌کند.
        if (await db.UserCredentials.FromSqlInterpolated($"SELECT * FROM [identity].[UserCredentials] WITH (UPDLOCK,HOLDLOCK) WHERE [Kind]={(int)CredentialKind.MobilePhone} AND [NormalizedIdentifier]={phone}")
            .AsNoTracking().AnyAsync(ct)) throw Error(PhoneChangeErrorCodes.Conflict);
    }
    private PhoneChangeResult Map(PhoneChangeRequest request) => new(request.Id, request.State.ToString(), request.ExpiresAtUtc,
        request.CreatedAtUtc + options.ResendDelay, request.CompletedAtUtc);
    private static AdminAuditLog Audit(PhoneChangeActor actor, PhoneChangeRequest request, string action, DateTime now, string correlation, AdminAuditResult result) =>
        AdminAuditLog.Create(actor.UserId, null, actor.SessionId, action, "PhoneChangeRequest", request.Id.ToString(), null,
            "ثبت تغییر شماره با تأیید مستقل؛ بدون ثبت شماره یا کد", now, correlation, result, null, null);
    private async Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action(); await tx.CommitAsync(ct); return result;
        }
        catch (DbUpdateConcurrencyException) { throw Error(PhoneChangeErrorCodes.Conflict); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 1205 }) { throw Error(PhoneChangeErrorCodes.Conflict); }
        catch (SqlException e) when (e.Number == 1205) { throw Error(PhoneChangeErrorCodes.Conflict); }
        catch (InvalidOperationException e) when (e.InnerException is SqlException { Number: 1205 })
        { throw Error(PhoneChangeErrorCodes.Conflict); }
    }
    private static IdentityException Error(string code) => new(code, "درخواست تغییر شماره قابل انجام نیست.");
}
