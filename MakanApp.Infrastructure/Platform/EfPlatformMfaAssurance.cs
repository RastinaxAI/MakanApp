using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed class EfPlatformMfaAssurance(MakanDbContext dbContext, BackofficeSecurityOptions options, TimeProvider clock)
    : IPlatformMfaAssurance, IPlatformStepUpAuthorization
{
    public Task<bool> IsSatisfiedAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        CheckAsync(userId, sessionId, false, cancellationToken);
    Task<bool> IPlatformStepUpAuthorization.IsSatisfiedAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        CheckAsync(userId, sessionId, true, cancellationToken);
    private Task<bool> CheckAsync(Guid userId, Guid sessionId, bool fresh, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var threshold = now.AddMinutes(-options.StepUpLifetimeMinutes);
        return (from session in dbContext.UserSessions.AsNoTracking()
                join credential in dbContext.PlatformMfaCredentials.AsNoTracking() on session.UserId equals credential.UserId
                join user in dbContext.Users.AsNoTracking() on session.UserId equals user.Id
                where session.Id == sessionId && session.UserId == userId && session.RevokedAtUtc == null &&
                      session.ExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now &&
                      user.AccountStatus == UserAccountStatus.Active && credential.ActivatedAtUtc != null && credential.ProtectedSecret != null &&
                      session.AssuranceLevel == AuthenticationAssuranceLevel.MultiFactor && session.StepUpAtUtc <= now &&
                      session.StepUpAtUtc != null && (!fresh || session.StepUpAtUtc > threshold)
                select session.Id).AnyAsync(ct);
    }
}
