namespace MakanApp.Domain.Messaging;

public sealed class ConversationUserState
{
    private ConversationUserState()
    {
    }

    private ConversationUserState(Guid conversationId, Guid userId, DateTime createdAtUtc)
    {
        ConversationId = conversationId;
        UserId = userId;
        MuteMode = ConversationMuteMode.None;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public DateTime? PinnedAtUtc { get; private set; }
    public ConversationMuteMode MuteMode { get; private set; }
    public DateTime? MutedUntilUtc { get; private set; }
    public bool IsManuallyUnread { get; private set; }
    public long HistoryHiddenThroughSequence { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsArchived => ArchivedAtUtc.HasValue;
    public bool IsPinned => PinnedAtUtc.HasValue;

    public static ConversationUserState Create(Guid conversationId, Guid userId, DateTime createdAtUtc)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو و کاربر برای وضعیت شخصی الزامی است.");
        }

        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new ConversationUserState(conversationId, userId, createdAtUtc);
    }

    public void Archive(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        ArchivedAtUtc ??= nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Unarchive(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        ArchivedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void Pin(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        PinnedAtUtc ??= nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Unpin(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        PinnedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MutePermanently(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        MuteMode = ConversationMuteMode.Permanent;
        MutedUntilUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MuteUntil(DateTime mutedUntilUtc, DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        ValidateUtc(mutedUntilUtc, nameof(mutedUntilUtc));
        if (mutedUntilUtc <= nowUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(mutedUntilUtc));
        }

        MuteMode = ConversationMuteMode.Until;
        MutedUntilUtc = mutedUntilUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Unmute(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        MuteMode = ConversationMuteMode.None;
        MutedUntilUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkUnread(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        IsManuallyUnread = true;
        UpdatedAtUtc = nowUtc;
    }

    public bool ClearManualUnread(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        if (!IsManuallyUnread)
        {
            return false;
        }

        IsManuallyUnread = false;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool HideHistoryThrough(long sequence, DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        if (sequence <= HistoryHiddenThroughSequence)
        {
            return false;
        }

        HistoryHiddenThroughSequence = sequence;
        IsManuallyUnread = false;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool IsMutedAt(DateTime nowUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        return MuteMode == ConversationMuteMode.Permanent ||
               MuteMode == ConversationMuteMode.Until && MutedUntilUtc > nowUtc;
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان وضعیت شخصی گفتگو باید UTC باشد.", parameterName);
        }
    }
}
