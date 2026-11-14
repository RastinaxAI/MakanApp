using MakanApp.Application.Identity;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Knowledge;

public sealed class AccountDeletionKnowledgePurger(MakanDbContext db, IAiKnowledgeClient client, EfCommercialStore commerce, TimeProvider clock)
    : IAccountDeletionKnowledgePurger
{
    public async Task RevokeSourcesAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        await ProveRequestAsync(context, ct);
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(context.UserId), ct, requireActive: false);
        var sources = await db.EducationalSources.Where(s => s.Scope == EducationalSourceScope.Personal && s.OwnerUserId == context.UserId).ToArrayAsync(ct);
        foreach (var source in sources)
        {
            if (source.Status == EducationalSourceStatus.Active) source.Archive(clock.GetUtcNow().UtcDateTime);
            var versions = await db.EducationalSourceVersions.Where(v => v.SourceId == source.Id).ToArrayAsync(ct);
            foreach (var version in versions)
                if (!await db.AccountDeletionKnowledgePurges.AnyAsync(p => p.RequestId == context.RequestId && p.SourceId == source.Id && p.Version == version.Number, ct))
                    db.AccountDeletionKnowledgePurges.Add(AccountDeletionKnowledgePurge.Create(context.RequestId, source, version, context.UserId));
        }
    }

    public async Task<AccountDeletionKnowledgeWork?> PrepareNextAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        await ProveRequestAsync(context, ct);
        var item = await db.AccountDeletionKnowledgePurges.Where(p => p.RequestId == context.RequestId && p.CompletedAtUtc == null)
            .OrderBy(p => p.SourceId).ThenBy(p => p.Version).FirstOrDefaultAsync(ct);
        if (item is null) return null;
        var work = new AccountDeletionKnowledgeWork(context, item.SourceId, item.Version, item.CommandRequestId);
        await ProveWorkAsync(work, ct); item.Attempt(clock.GetUtcNow().UtcDateTime);
        return work;
    }

    public async Task<string> DeleteAsync(AccountDeletionKnowledgeWork work, CancellationToken ct)
    {
        // کنترل claim و مالکیت جای نشست جعلی را می‌گیرد؛ تماس شبکه بیرون transaction انجام می‌شود.
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("تماس Knowledge نباید داخل transaction باشد.");
        await ProveWorkAsync(work, ct);
        try
        {
            var result = await client.DeleteAsync(new(work.SourceId, work.Version), new(work.CommandRequestId, work.Context.CorrelationId), ct);
            if (result.SourceId != work.SourceId || result.Version != work.Version || result.State != "Deleted")
                throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.KnowledgeUnavailable, true);
            return "Deleted";
        }
        catch (AiKnowledgeException exception) when (exception.Code == KnowledgeErrorCodes.NotIngested && exception.StatusCode == 409)
        { return "NotIngested"; }
        catch (AiKnowledgeException)
        { throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.KnowledgeUnavailable, true); }
        catch (MakanApp.Application.Ai.AiServiceException)
        { throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.KnowledgeUnavailable, true); }
    }

    public async Task CompleteAsync(AccountDeletionKnowledgeWork work, string outcome, CancellationToken ct)
    {
        await ProveWorkAsync(work, ct);
        var item = await db.AccountDeletionKnowledgePurges.SingleAsync(p => p.RequestId == work.Context.RequestId && p.SourceId == work.SourceId && p.Version == work.Version, ct);
        item.Complete(outcome, clock.GetUtcNow().UtcDateTime);
    }

    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        await ProveRequestAsync(context, ct);
        if (await db.EducationalSources.AnyAsync(s => s.Scope == EducationalSourceScope.Personal && s.OwnerUserId == context.UserId && s.Status == EducationalSourceStatus.Active, ct) ||
            await db.AccountDeletionKnowledgePurges.AnyAsync(p => p.RequestId == context.RequestId && p.CompletedAtUtc == null, ct) ||
            await db.EducationalSourceVersions.AnyAsync(v => db.EducationalSources.Any(s => s.Id == v.SourceId && s.Scope == EducationalSourceScope.Personal && s.OwnerUserId == context.UserId) &&
                !db.AccountDeletionKnowledgePurges.Any(p => p.RequestId == context.RequestId && p.SourceId == v.SourceId && p.Version == v.Number && p.CompletedAtUtc != null), ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }

    private async Task ProveRequestAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == context.RequestId && r.UserId == context.UserId, ct)
            ?? throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.OwnershipConflict);
        request.EnsureClaim(context.ClaimToken, clock.GetUtcNow().UtcDateTime);
        if (request.PolicyVersion != context.PolicyVersion || !await db.Users.AnyAsync(u => u.Id == context.UserId && u.DeletionStartedAtUtc != null && u.AccountStatus == UserAccountStatus.Suspended, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.OwnershipConflict);
    }

    private async Task ProveWorkAsync(AccountDeletionKnowledgeWork work, CancellationToken ct)
    {
        await ProveRequestAsync(work.Context, ct);
        if (!await db.AccountDeletionSteps.AnyAsync(s => s.RequestId == work.Context.RequestId && s.Step == AccountDeletionStepKind.DeletePersonalKnowledge && s.Status == AccountDeletionStepStatus.Processing, ct) ||
            !await db.AccountDeletionKnowledgePurges.AnyAsync(p => p.RequestId == work.Context.RequestId && p.SourceId == work.SourceId && p.Version == work.Version && p.OwnerUserId == work.Context.UserId && p.CommandRequestId == work.CommandRequestId, ct) ||
            !await db.EducationalSourceVersions.AnyAsync(v => v.SourceId == work.SourceId && v.Number == work.Version && db.EducationalSources.Any(s => s.Id == v.SourceId && s.Scope == EducationalSourceScope.Personal && s.OwnerUserId == work.Context.UserId && s.Status != EducationalSourceStatus.Active), ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.OwnershipConflict);
    }
}
