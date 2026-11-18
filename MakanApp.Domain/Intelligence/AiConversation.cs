namespace MakanApp.Domain.Intelligence;

public sealed record AiConversationSourcePin(Guid SourceId, int Version);

public sealed class AiConversation
{
    private AiConversation() { }
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public byte[] ContextFingerprint { get; private set; } = [];
    public IReadOnlyList<AiConversationSourcePin> SourcePins { get; private set; } = [];
    public long NextSequence { get; private set; } = 1;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AiConversation Create(Guid id, Guid ownerUserId, byte[] contextFingerprint, DateTime nowUtc)
    {
        if (id == Guid.Empty || ownerUserId == Guid.Empty || contextFingerprint.Length != 32 || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("هویت گفت‌وگوی AI معتبر نیست.");
        return new() { Id = id, OwnerUserId = ownerUserId, ContextFingerprint = contextFingerprint.ToArray(),
            CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc };
    }

    public long ReservePair(DateTime nowUtc)
    {
        var sequence = NextSequence;
        NextSequence = checked(NextSequence + 2);
        Touch(nowUtc);
        return sequence;
    }

    public void BindSources(IReadOnlyList<AiConversationSourcePin> sources, DateTime nowUtc)
    {
        if (sources.Count > 32 || sources.Any(p => p.SourceId == Guid.Empty || p.Version < 1) ||
            sources.Select(p => p.SourceId).Distinct().Count() != sources.Count)
            throw new ArgumentException("نسخه‌های منبع معتبر نیستند.");
        var ordered = sources.OrderBy(p => p.SourceId).ToArray();
        if (SourcePins.Count != 0 && !SourcePins.SequenceEqual(ordered))
            throw new InvalidOperationException("تغییر نسخهٔ منبع به گفت‌وگوی تازه نیاز دارد.");
        SourcePins = Array.AsReadOnly(ordered);
        Touch(nowUtc);
    }

    public void Touch(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد.");
        UpdatedAtUtc = nowUtc;
    }
}
