using MakanApp.Domain.Organization;

namespace MakanApp.Domain.Messaging;

public sealed class ConversationInviteLink
{
    private ConversationInviteLink() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? CreatorMembershipId { get; private set; }
    public OrganizationRole? CreatorRole { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public int MaximumUses { get; private set; }
    public int UsedCount { get; private set; }
    public bool RequiresApproval { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsOpen(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
    public static ConversationInviteLink Create(Guid id, Guid conversationId, Guid creatorId, string tokenHash,
        DateTime expiresAtUtc, int maximumUses, bool requiresApproval, DateTime nowUtc, Guid? creatorMembershipId = null, OrganizationRole? creatorRole = null)
    {
        if (id == Guid.Empty || conversationId == Guid.Empty || creatorId == Guid.Empty ||
            creatorMembershipId.HasValue != creatorRole.HasValue || creatorMembershipId == Guid.Empty || creatorRole.HasValue && !Enum.IsDefined(creatorRole.Value) ||
            tokenHash is null || tokenHash.Length != 64 || !tokenHash.All(Uri.IsHexDigit) || maximumUses is < 1 or > 10000 ||
            nowUtc.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= nowUtc || expiresAtUtc > nowUtc.AddDays(30))
            throw new ArgumentException("لینک، انقضای حداکثر ۳۰ روزه و سقف مصرف معتبر لازم است.");
        return new() { Id = id, ConversationId = conversationId, CreatedByUserId = creatorId, TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc, MaximumUses = maximumUses, RequiresApproval = requiresApproval, CreatedAtUtc = nowUtc, CreatorMembershipId = creatorMembershipId, CreatorRole = creatorRole };
    }
    public void Consume(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || !IsOpen(nowUtc) || UsedCount >= MaximumUses) throw new InvalidOperationException("لینک قابل مصرف نیست.");
        UsedCount++;
    }
    public void Revoke(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان UTC لازم است.");
        RevokedAtUtc ??= nowUtc;
    }
}

public enum ConversationJoinRequestStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4, Expired = 5 }

public sealed class ConversationJoinRequest
{
    private ConversationJoinRequest() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid InviteLinkId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? RequesterMembershipId { get; private set; }
    public OrganizationRole? RequesterRole { get; private set; }
    public ConversationJoinRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static ConversationJoinRequest Create(Guid conversationId, Guid inviteLinkId, Guid userId, DateTime expiresAtUtc, DateTime nowUtc,
        Guid? requesterMembershipId = null, OrganizationRole? requesterRole = null)
    {
        if (conversationId == Guid.Empty || inviteLinkId == Guid.Empty || userId == Guid.Empty ||
            expiresAtUtc.Kind != DateTimeKind.Utc || nowUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= nowUtc ||
            requesterMembershipId.HasValue != requesterRole.HasValue || requesterMembershipId == Guid.Empty || requesterRole.HasValue && !Enum.IsDefined(requesterRole.Value))
            throw new ArgumentException("درخواست عضویت معتبر لازم است.");
        return new() { Id = Guid.NewGuid(), ConversationId = conversationId, InviteLinkId = inviteLinkId, UserId = userId,
            Status = ConversationJoinRequestStatus.Pending, CreatedAtUtc = nowUtc, ExpiresAtUtc = expiresAtUtc,
            RequesterMembershipId = requesterMembershipId, RequesterRole = requesterRole };
    }
    public void Complete(ConversationJoinRequestStatus status, DateTime nowUtc)
    {
        if (Status == status) return;
        if (Status != ConversationJoinRequestStatus.Pending || !Enum.IsDefined(status) || status == ConversationJoinRequestStatus.Pending ||
            nowUtc.Kind != DateTimeKind.Utc || status == ConversationJoinRequestStatus.Approved && nowUtc >= ExpiresAtUtc)
            throw new InvalidOperationException("انتقال وضعیت درخواست مجاز نیست.");
        Status = status; CompletedAtUtc = nowUtc;
    }
}
