namespace MakanApp.Domain.Messaging;

public sealed class ConversationMemberRestriction
{
    private ConversationMemberRestriction() { }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsBanned { get; private set; }
    public GroupMemberCapability DeniedCapabilities { get; private set; }
    public DateTime? UntilUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsEffective(DateTime nowUtc) => (IsBanned || DeniedCapabilities != GroupMemberCapability.None) && (UntilUtc is null || UntilUtc > nowUtc);
    public bool Denies(GroupMemberCapability capability, DateTime nowUtc) => IsEffective(nowUtc) && (IsBanned || (DeniedCapabilities & capability) != 0);
    public static ConversationMemberRestriction Create(Guid conversationId, Guid userId)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("شناسهٔ گفتگو و عضو لازم است.");
        return new() { ConversationId = conversationId, UserId = userId };
    }
    public void Update(bool isBanned, GroupMemberCapability denied, DateTime? untilUtc, DateTime nowUtc)
    {
        if ((denied & ~GroupMemberCapability.All) != 0 || nowUtc.Kind != DateTimeKind.Utc ||
            untilUtc.HasValue && (untilUtc.Value.Kind != DateTimeKind.Utc || untilUtc <= nowUtc) ||
            !isBanned && denied == GroupMemberCapability.None && untilUtc.HasValue)
            throw new ArgumentException("محدودیت یا زمان آن معتبر نیست.");
        IsBanned = isBanned; DeniedCapabilities = denied; UntilUtc = untilUtc; Revision = checked(Revision + 1);
    }
}
