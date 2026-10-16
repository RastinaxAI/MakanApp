using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record CreateInviteLinkCommand(Guid ClientLinkId, DateTime ExpiresAtUtc, int MaximumUses, bool RequiresApproval);
public sealed record RedeemInviteLinkCommand(string Token);
public sealed record RevokeInviteLinkCommand(string ExpectedVersion);
public sealed record InviteLinkResult(Guid Id, Guid ConversationId, Guid CreatedByUserId, DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc, int MaximumUses, int UsedCount, bool RequiresApproval, DateTime? RevokedAtUtc, string Version);
public sealed record CreatedInviteLinkResult(InviteLinkResult Link, string? Token, bool AlreadyExisted);
public sealed record InviteLinkPageResult(IReadOnlyCollection<InviteLinkResult> Items, bool HasMore);
public sealed record ConversationJoinRequestResult(Guid Id, Guid ConversationId, Guid UserId,
    ConversationJoinRequestStatus Status, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, string Version);
public sealed record ConversationJoinRequestPageResult(IReadOnlyCollection<ConversationJoinRequestResult> Items, bool HasMore);
public sealed record InviteLinkRedemptionResult(Guid ConversationId, bool AlreadyMember, ConversationJoinRequestResult? JoinRequest);
public sealed record CompleteJoinRequestCommand(string ExpectedVersion);
public sealed record SetMemberRestrictionCommand(bool IsBanned, GroupMemberCapability DeniedCapabilities, DateTime? UntilUtc, string Reason, string? ExpectedVersion);
public sealed record MemberRestrictionResult(Guid ConversationId, Guid UserId, bool IsBanned, GroupMemberCapability DeniedCapabilities,
    DateTime? UntilUtc, bool IsEffective, string Version);
public sealed record MemberRestrictionPageResult(IReadOnlyCollection<MemberRestrictionResult> Items, bool HasMore);
public sealed record UpdateConversationInfoCommand(string Title, string? Description, string ExpectedVersion);
public sealed record ConversationInfoResult(Guid ConversationId, string Title, string? Description, string Version);

public interface IConversationAdvancedManagementStore
{
    Task<CreatedInviteLinkResult> CreateLinkAsync(Guid actorId, Guid sessionId, Guid conversationId, CreateInviteLinkCommand command,
        AccessContext context, string correlationId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<InviteLinkPageResult> GetLinksAsync(Guid actorId, Guid conversationId, AccessContext context, int skip, int take, CancellationToken cancellationToken);
    Task<InviteLinkResult> RevokeLinkAsync(Guid actorId, Guid sessionId, Guid conversationId, Guid linkId, byte[] expectedVersion,
        AccessContext context, string correlationId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<InviteLinkRedemptionResult> RedeemLinkAsync(Guid actorId, Guid sessionId, string token, AccessContext context,
        string correlationId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<ConversationJoinRequestPageResult> GetJoinRequestsAsync(Guid actorId, Guid? conversationId, AccessContext context,
        int skip, int take, CancellationToken cancellationToken);
    Task<ConversationJoinRequestResult> CompleteJoinRequestAsync(Guid actorId, Guid sessionId, Guid requestId, ConversationJoinRequestStatus status,
        byte[] expectedVersion, AccessContext context, string correlationId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<MemberRestrictionPageResult> GetRestrictionsAsync(Guid actorId, Guid conversationId, AccessContext context,
        int skip, int take, DateTime nowUtc, CancellationToken cancellationToken);
    Task<MemberRestrictionResult> SetRestrictionAsync(Guid actorId, Guid sessionId, Guid conversationId, Guid targetUserId,
        SetMemberRestrictionCommand command, byte[]? expectedVersion, AccessContext context, string correlationId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<ConversationInfoResult> UpdateInfoAsync(Guid actorId, Guid sessionId, Guid conversationId, UpdateConversationInfoCommand command,
        byte[] expectedVersion, AccessContext context, string correlationId, DateTime nowUtc, CancellationToken cancellationToken);
}
