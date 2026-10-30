using MakanApp.Application.Ai;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class EfAiSourceSelectionStore(MakanDbContext db, TimeProvider clock) : IAiSourceSelectionStore
{
    public async Task<IReadOnlyList<AiSourceVersionIdentity>?> FindPinnedAsync(Guid userId, Guid operationId, byte[] payloadHash, CancellationToken ct)
    {
        var existing = await db.Set<AiSourceSelectionOperation>().AsNoTracking().Include(x => x.Pins)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.OperationId == operationId, ct);
        if (existing is null) return null;
        if (!existing.PayloadHash.SequenceEqual(payloadHash)) throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
        return existing.Pins.OrderBy(x => x.SourceId).Select(x => new AiSourceVersionIdentity(x.SourceId, x.Number)).ToArray();
    }
    public async Task EnsureUnselectedAsync(Guid userId, Guid operationId, CancellationToken ct)
    {
        if (await db.Set<AiSourceSelectionOperation>().AnyAsync(x => x.UserId == userId && x.OperationId == operationId, ct))
            throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
    }
    public async Task<(IReadOnlyList<AiSourceVersionIdentity> Pins, bool Replayed)> PinAsync(Guid userId, Guid operationId,
        byte[] payloadHash, IReadOnlyList<AiSourceVersionIdentity> candidates, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={userId}", ct);
        var existing = await db.Set<AiSourceSelectionOperation>().AsNoTracking().Include(x => x.Pins)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.OperationId == operationId, ct);
        if (existing is not null)
        {
            if (!existing.PayloadHash.SequenceEqual(payloadHash)) throw AiSourceSelection.Error(AiSourceErrorCodes.OperationConflict);
            await transaction.CommitAsync(ct);
            return (existing.Pins.OrderBy(x => x.SourceId).Select(x => new AiSourceVersionIdentity(x.SourceId, x.Number)).ToArray(), true);
        }
        var operation = AiSourceSelectionOperation.Create(userId, operationId, payloadHash,
            candidates.Select(x => (x.SourceId, x.Number)).ToArray(), clock.GetUtcNow().UtcDateTime);
        db.Set<AiSourceSelectionOperation>().Add(operation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (candidates, false);
    }
}
