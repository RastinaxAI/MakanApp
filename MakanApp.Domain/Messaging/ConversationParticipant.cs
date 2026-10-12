namespace MakanApp.Domain.Messaging;

public sealed class ConversationParticipant
{
    private ConversationParticipant()
    {
    }

    private ConversationParticipant(
        Guid id,
        Guid conversationId,
        Guid userId,
        ConversationParticipantRole role,
        long historyVisibleFromSequence,
        ConversationAdminPermission adminPermissions,
        DateTime joinedAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        HistoryVisibleFromSequence = historyVisibleFromSequence;
        AdminPermissions = role == ConversationParticipantRole.Admin
            ? adminPermissions
            : ConversationAdminPermission.None;
        Status = ConversationParticipantStatus.Active;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationParticipantRole Role { get; private set; }
    public ConversationParticipantStatus Status { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public Guid? EndedByUserId { get; private set; }
    public long LastDeliveredMessageSequence { get; private set; }
    public long LastReadMessageSequence { get; private set; }
    public DateTime? CursorUpdatedAtUtc { get; private set; }
    public long HistoryVisibleFromSequence { get; private set; }
    public ConversationAdminPermission AdminPermissions { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == ConversationParticipantStatus.Active && !EndedAtUtc.HasValue;

    public static ConversationParticipant CreateActive(
        Guid conversationId,
        Guid userId,
        DateTime joinedAtUtc,
        ConversationParticipantRole role = ConversationParticipantRole.Member,
        long historyVisibleFromSequence = 1,
        ConversationAdminPermission adminPermissions = ConversationAdminPermission.None)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو و کاربر الزامی است.");
        }

        if (joinedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان عضویت باید UTC باشد.", nameof(joinedAtUtc));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (historyVisibleFromSequence <= 0 ||
            (adminPermissions & ~ConversationAdminPermission.All) != 0 ||
            role != ConversationParticipantRole.Admin && adminPermissions != ConversationAdminPermission.None)
        {
            throw new ArgumentOutOfRangeException(nameof(historyVisibleFromSequence));
        }

        return new ConversationParticipant(
            Guid.NewGuid(),
            conversationId,
            userId,
            role,
            historyVisibleFromSequence,
            adminPermissions,
            joinedAtUtc);
    }

    public void Remove(Guid endedByUserId, DateTime endedAtUtc)
    {
        if (endedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه اقدام‌کننده الزامی است.", nameof(endedByUserId));
        }

        End(ConversationParticipantStatus.Removed, endedByUserId, endedAtUtc);
    }

    public void Leave(DateTime endedAtUtc) =>
        End(ConversationParticipantStatus.Left, UserId, endedAtUtc);

    public void ChangeRole(
        ConversationParticipantRole role,
        ConversationAdminPermission adminPermissions = ConversationAdminPermission.None)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("نقش عضویت پایان‌یافته قابل تغییر نیست.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if ((adminPermissions & ~ConversationAdminPermission.All) != 0 ||
            role != ConversationParticipantRole.Admin && adminPermissions != ConversationAdminPermission.None)
        {
            throw new ArgumentOutOfRangeException(nameof(adminPermissions));
        }

        Role = role;
        AdminPermissions = role == ConversationParticipantRole.Admin
            ? adminPermissions
            : ConversationAdminPermission.None;
    }

    public void UpdateAdminPermissions(ConversationAdminPermission permissions)
    {
        if (!IsActive || Role != ConversationParticipantRole.Admin)
        {
            throw new InvalidOperationException("فقط permission عضو Admin فعال قابل تغییر است.");
        }

        if ((permissions & ~ConversationAdminPermission.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permissions));
        }

        AdminPermissions = permissions;
    }

    public bool AdvanceDelivery(long upToMessageSequence, DateTime updatedAtUtc)
    {
        ValidateCursorInput(upToMessageSequence, updatedAtUtc);
        if (upToMessageSequence <= LastDeliveredMessageSequence)
        {
            return false;
        }

        LastDeliveredMessageSequence = upToMessageSequence;
        CursorUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    public bool AdvanceRead(long upToMessageSequence, DateTime updatedAtUtc)
    {
        ValidateCursorInput(upToMessageSequence, updatedAtUtc);
        if (upToMessageSequence <= LastReadMessageSequence)
        {
            return false;
        }

        LastReadMessageSequence = upToMessageSequence;
        LastDeliveredMessageSequence = Math.Max(LastDeliveredMessageSequence, upToMessageSequence);
        CursorUpdatedAtUtc = updatedAtUtc;
        return true;
    }

    private void End(
        ConversationParticipantStatus status,
        Guid endedByUserId,
        DateTime endedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        if (status is not ConversationParticipantStatus.Removed and not ConversationParticipantStatus.Left)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (endedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پایان عضویت باید UTC باشد.", nameof(endedAtUtc));
        }

        Status = status;
        EndedAtUtc = endedAtUtc;
        EndedByUserId = endedByUserId;
    }

    private static void ValidateCursorInput(long upToMessageSequence, DateTime updatedAtUtc)
    {
        if (upToMessageSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(upToMessageSequence));
        }

        if (updatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان به‌روزرسانی نشانگر باید UTC باشد.", nameof(updatedAtUtc));
        }
    }
}
