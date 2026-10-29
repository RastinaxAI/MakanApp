using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;

namespace MakanApp.Application.Commerce;

public sealed class CommercialActorAccess(IIdentityStore identities, TimeProvider clock)
{
    public async Task EnsureAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await identities.GetSessionAsync(sessionId, userId, ct);
        var user = await identities.GetUserAsync(userId, ct);
        if (session is null || !session.IsActive(clock.GetUtcNow().UtcDateTime) || user is null)
            throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        if (user.AccountStatus != UserAccountStatus.Active)
            throw new IdentityException(IdentityErrorCodes.AccountSuspended, "حساب کاربری فعال نیست.");
    }
}
