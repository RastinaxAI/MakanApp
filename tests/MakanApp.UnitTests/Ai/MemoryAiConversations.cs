using MakanApp.Application.Ai;
using MakanApp.Domain.Intelligence;

namespace MakanApp.UnitTests.Ai;

// فقط double مربوط به Unit است؛ اثبات قفل/constraint در تست SQL مستقل انجام می‌شود.
internal sealed class MemoryAiConversations : IAiConversationStore
{
    private readonly Dictionary<Guid, AiConversation> _conversations = [];
    private readonly List<AiConversationTurn> _turns = [];
    public async Task<AiConversationState> OpenAsync(Guid user, Guid? conversation, Guid operation, byte[] binding, byte[] intentHash,
        Func<AiConversationState, CancellationToken, Task> authorize, CancellationToken ct)
    {
        var old = _turns.SingleOrDefault(x => x.OwnerUserId == user && x.OperationId == operation && x.Role == AiConversationTurnRole.User);
        var id = old?.ConversationId ?? conversation ?? Guid.NewGuid();
        if (old is not null && (!old.IntentHash.SequenceEqual(intentHash) || conversation.HasValue && conversation != id))
            throw new AiServiceException(AiConversationErrorCodes.OperationConflict, "تعارض عملیات تست");
        if (!_conversations.TryGetValue(id, out var entity)) { entity = AiConversation.Create(id, user, binding, DateTime.UtcNow); _conversations.Add(id, entity); }
        Ensure(entity, user, binding);
        await authorize(State(entity), ct);
        if (old is null) _turns.Add(AiConversationTurn.ReserveUser(user, id, operation, entity.ReservePair(DateTime.UtcNow), intentHash, DateTime.UtcNow));
        return State(entity);
    }
    public async Task<(AiConversationState State, IReadOnlyList<AiHistoryTurn> History)> ReadHistoryAsync(Guid user, Guid id, Guid? exclude,
        byte[] binding, Func<AiConversationState, CancellationToken, Task> authorize, CancellationToken ct)
    {
        if (!_conversations.TryGetValue(id, out var entity)) throw new AiServiceException(AiConversationErrorCodes.NotFound, "پیدا نشد");
        Ensure(entity, user, binding); await authorize(State(entity), ct);
        var pairs = _turns.Where(x => x.ConversationId == id && x.Role == AiConversationTurnRole.User && x.IsCompleted && x.OperationId != exclude)
            .OrderByDescending(x => x.Sequence).Select(x => new AiCompletedHistoryPair(x.Sequence, x.Text!,
                _turns.Single(a => a.ConversationId == id && a.OperationId == x.OperationId && a.Role == AiConversationTurnRole.Assistant).Text!));
        return (State(entity), AiHistoryWindow.Select(pairs));
    }
    public async Task CompleteAsync(Guid user, Guid id, Guid operation, byte[] binding, string prompt, string answer,
        IReadOnlyList<AiConversationSourcePin> pins, Func<AiConversationState, CancellationToken, Task> authorize, CancellationToken ct)
    {
        var entity = _conversations[id]; Ensure(entity, user, binding); await authorize(State(entity), ct);
        if (pins.Count != 0) entity.BindSources(pins, DateTime.UtcNow);
        _turns.Add(_turns.Single(x => x.OwnerUserId == user && x.OperationId == operation && x.Role == AiConversationTurnRole.User).Complete(prompt, answer, DateTime.UtcNow));
    }
    private static AiConversationState State(AiConversation entity) => new(entity.Id, entity.SourcePins);
    private static void Ensure(AiConversation entity, Guid user, byte[] binding)
    {
        if (entity.OwnerUserId != user || !entity.ContextFingerprint.SequenceEqual(binding)) throw new AiServiceException(AiConversationErrorCodes.NotFound, "پیدا نشد");
    }
}
