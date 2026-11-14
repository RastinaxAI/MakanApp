using MakanApp.Application.Identity;
using MakanApp.Domain.Guardian;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Guardian;

public sealed class GuardianAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock) : IGuardianAccountDeletionLifecycle
{
    private IQueryable<GuardianRelation> Related(Guid user) => db.GuardianRelations.Where(g =>
        g.GuardianUserId == user || db.OrganizationPersons.Any(p => p.Id == g.LearnerOrganizationPersonId &&
            db.Users.Any(u => u.Id == user && u.PersonId == p.PersonId)));
    public async Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var relations = await Related(context.UserId).Where(g => g.Status == GuardianRelationStatus.Active || g.Status == GuardianRelationStatus.Pending).ToArrayAsync(ct);
        foreach (var relation in relations) relation.End(clock.GetUtcNow().UtcDateTime);
    }
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await Related(context.UserId).AnyAsync(g => g.Status == GuardianRelationStatus.Active || g.Status == GuardianRelationStatus.Pending, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }
}
