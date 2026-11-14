using MakanApp.Application.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class AiAccountDeletionLifecycle(MakanDbContext db) : IAiAccountDeletionLifecycle
{
    public Task PurgePrivateContextAsync(AccountDeletionExecutionContext context, CancellationToken ct) =>
        db.AssistantContextSelections.Where(s => s.UserId == context.UserId).ExecuteDeleteAsync(ct);
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.AssistantContextSelections.AnyAsync(s => s.UserId == context.UserId, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }
}
