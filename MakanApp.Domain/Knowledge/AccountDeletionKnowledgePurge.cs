namespace MakanApp.Domain.Knowledge;

public sealed class AccountDeletionKnowledgePurge
{
    private AccountDeletionKnowledgePurge() { }
    public Guid RequestId { get; private set; }
    public Guid SourceId { get; private set; }
    public int Version { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string CommandRequestId { get; private set; } = "";
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Outcome { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AccountDeletionKnowledgePurge Create(Guid request, EducationalSource source, EducationalSourceVersion version, Guid user)
    {
        if (request == Guid.Empty || source.Scope != EducationalSourceScope.Personal || source.OwnerUserId != user || version.SourceId != source.Id)
            throw new InvalidOperationException("فقط منبع شخصیِ صاحب درخواست قابل پاک‌سازی است.");
        return new() { RequestId = request, SourceId = source.Id, Version = version.Number, OwnerUserId = user,
            CommandRequestId = Guid.NewGuid().ToString("N") };
    }
    public void Attempt(DateTime now)
    {
        if (CompletedAtUtc.HasValue || now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("پاک‌سازی قابل تکرار نیست.");
        AttemptCount = checked(AttemptCount + 1); LastAttemptAtUtc = now;
    }
    public void Complete(string outcome, DateTime now)
    {
        if (outcome is not ("Deleted" or "NotIngested") || now.Kind != DateTimeKind.Utc || AttemptCount < 1)
            throw new InvalidOperationException("تأیید قطعی پاک‌سازی لازم است.");
        if (CompletedAtUtc.HasValue) return;
        Outcome = outcome; CompletedAtUtc = now;
    }
}
