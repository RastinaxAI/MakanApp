using MakanApp.Application.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class AiAccountDeletionLifecycle(MakanDbContext db) : IAiAccountDeletionLifecycle
{
    public async Task PurgePrivateContextAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        await db.AiConversationTurns.Where(s => s.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.AiConversations.Where(s => s.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.AssistantContextSelections.Where(s => s.UserId == context.UserId).ExecuteDeleteAsync(ct);
    }
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.AssistantContextSelections.AnyAsync(s => s.UserId == context.UserId, ct) ||
            await db.AiConversations.AnyAsync(s => s.OwnerUserId == context.UserId, ct) ||
            await db.AiConversationTurns.AnyAsync(s => s.OwnerUserId == context.UserId, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }
}
