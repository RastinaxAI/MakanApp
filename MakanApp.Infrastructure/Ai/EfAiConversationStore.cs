using System.Data;
using MakanApp.Application.Ai;
using MakanApp.Domain.Intelligence;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class EfAiConversationStore(MakanDbContext db, TimeProvider clock) : IAiConversationStore
{
    public async Task<AiConversationState> OpenAsync(Guid user, Guid? conversation, Guid operation, byte[] binding, byte[] intentHash,
        Func<AiConversationState, CancellationToken, Task> reauthorize, CancellationToken ct)
    {
        // قفل صریح مالک و Conversation جایگزین range lock سراسریِ lookup خالی است.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockOwner(user, ct);
        var old = await db.AiConversationTurns.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OwnerUserId == user && x.OperationId == operation && x.Role == AiConversationTurnRole.User, ct);
        if (old is not null)
        {
            var saved = await Owned(user, old.ConversationId, binding, ct);
            await reauthorize(State(saved), ct);
            if (!old.IntentHash.SequenceEqual(intentHash) || conversation.HasValue && conversation != old.ConversationId)
                throw Error(AiConversationErrorCodes.OperationConflict);
            await transaction.CommitAsync(ct);
            return State(saved);
        }
        var id = conversation ?? Guid.NewGuid();
        await LockConversation(id, ct);
        var entity = await db.AiConversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            entity = AiConversation.Create(id, user, binding, Now);
            db.AiConversations.Add(entity);
        }
        else
        {
            EnsureOwned(entity, user, binding);
            db.AiConversations.Attach(entity);
        }
        await reauthorize(State(entity), ct);
        var turn = AiConversationTurn.ReserveUser(user, id, operation, entity.ReservePair(Now), intentHash, Now);
        db.AiConversationTurns.Add(turn);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.Entry(entity).State = EntityState.Detached;
        db.Entry(turn).State = EntityState.Detached;
        return State(entity);
    }

    public async Task<(AiConversationState State, IReadOnlyList<AiHistoryTurn> History)> ReadHistoryAsync(Guid user, Guid conversation,
        Guid? excludeOperation, byte[] binding, Func<AiConversationState, CancellationToken, Task> reauthorize, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockOwner(user, ct);
        await LockConversation(conversation, ct);
        var entity = await Owned(user, conversation, binding, ct);
        await reauthorize(State(entity), ct);
        var candidates = new List<AiCompletedHistoryPair>();
        var before = long.MaxValue; var stop = false;
        while (!stop)
        {
            var batch = await (from prompt in db.AiConversationTurns.AsNoTracking()
                join answer in db.AiConversationTurns.AsNoTracking()
                    on new { prompt.OwnerUserId, prompt.ConversationId, prompt.OperationId } equals new { answer.OwnerUserId, answer.ConversationId, answer.OperationId }
                where prompt.OwnerUserId == user && prompt.ConversationId == conversation && prompt.Role == AiConversationTurnRole.User &&
                    answer.Role == AiConversationTurnRole.Assistant && prompt.IsCompleted && answer.IsCompleted && prompt.Sequence < before &&
                    (!excludeOperation.HasValue || prompt.OperationId != excludeOperation.Value)
                orderby prompt.Sequence descending
                select new AiCompletedHistoryPair(prompt.Sequence, prompt.Text!, answer.Text!)).Take(16).ToListAsync(ct);
            if (batch.Count == 0) break;
            foreach (var pair in batch)
            {
                if (AiConversationTurn.CharacterCount(pair.UserText) > AiHistoryWindow.MaximumTurnCharacters ||
                    AiConversationTurn.CharacterCount(pair.AssistantText) > AiHistoryWindow.MaximumTurnCharacters) continue;
                candidates.Add(pair);
                var selected = AiHistoryWindow.Select(candidates);
                if (selected.Count == AiHistoryWindow.MaximumTurns || selected.Count < candidates.Count * 2) { stop = true; break; }
            }
            before = batch[^1].Sequence;
        }
        var history = AiHistoryWindow.Select(candidates);
        await transaction.CommitAsync(ct);
        return (State(entity), history);
    }

    public async Task CompleteAsync(Guid user, Guid conversation, Guid operation, byte[] binding, string userText, string assistantText,
        IReadOnlyList<AiConversationSourcePin> sourcePins, Func<AiConversationState, CancellationToken, Task> reauthorize, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockOwner(user, ct);
        await LockConversation(conversation, ct);
        var entity = await Owned(user, conversation, binding, ct);
        // callback فقط قواعد محلی و SQL را می‌خواند؛ هیچ HTTP در transaction اجرا نمی‌شود.
        await reauthorize(State(entity), ct);
        var prompt = await db.AiConversationTurns.AsNoTracking().SingleAsync(x => x.OwnerUserId == user &&
            x.OperationId == operation && x.ConversationId == conversation && x.Role == AiConversationTurnRole.User, ct);
        if (prompt.IsCompleted) { await transaction.CommitAsync(ct); return; }
        db.AiConversations.Attach(entity); db.AiConversationTurns.Attach(prompt);
        if (sourcePins.Count != 0)
        {
            if (entity.SourcePins.Count != 0 && !entity.SourcePins.SequenceEqual(sourcePins.OrderBy(p => p.SourceId)))
                throw Error(AiConversationErrorCodes.SourceBindingConflict);
            entity.BindSources(sourcePins, Now);
        }
        var answer = prompt.Complete(userText, assistantText, Now);
        entity.Touch(Now); db.AiConversationTurns.Add(answer);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.Entry(entity).State = EntityState.Detached; db.Entry(prompt).State = EntityState.Detached; db.Entry(answer).State = EntityState.Detached;
    }

    private async Task<AiConversation> Owned(Guid user, Guid id, byte[] binding, CancellationToken ct)
    {
        var entity = await db.AiConversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) throw Error(AiConversationErrorCodes.NotFound);
        EnsureOwned(entity, user, binding); return entity;
    }
    private static void EnsureOwned(AiConversation entity, Guid user, byte[] binding)
    {
        if (entity.OwnerUserId != user || !entity.ContextFingerprint.SequenceEqual(binding)) throw Error(AiConversationErrorCodes.NotFound);
    }
    private Task LockOwner(Guid user, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT [Id] FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={user}", ct);
    private Task LockConversation(Guid conversation, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"ai-conversation:" + conversation.ToString("N")},
            @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
        IF @result < 0 THROW 51000, 'AI conversation lock unavailable.', 1;
        """, ct);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static AiConversationState State(AiConversation entity) => new(entity.Id, entity.SourcePins.ToArray());
    private static AiServiceException Error(string code) => new(code, "گفت‌وگوی مجاز و جاری AI در دسترس نیست.");
}
