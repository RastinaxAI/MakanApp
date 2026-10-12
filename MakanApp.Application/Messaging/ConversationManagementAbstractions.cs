using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record ConversationParticipantStoreRecord(
    ConversationParticipant Participant,
    SafeMessagingIdentityRecord Identity);

public sealed record ManagedConversationStoreResult(
    Conversation Conversation,
    IReadOnlyCollection<ConversationParticipantStoreRecord> Participants,
    bool AlreadyExisted);

public sealed record OwnershipTransferStoreResult(
    ConversationOwnershipTransfer Transfer,
    bool AlreadyCompleted);

public sealed record DiscoverableConversationStoreRecord(
    Conversation Conversation,
    string? OrganizationName,
    bool IsJoined);

public sealed record DiscoverableConversationStorePage(
    IReadOnlyCollection<DiscoverableConversationStoreRecord> Items,
    bool HasMore);

public interface IConversationManagementStore
{
    Task<ManagedConversationStoreResult> CreateAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> GetDetailsAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken);

    Task<(IReadOnlyCollection<ConversationParticipantStoreRecord> Items, bool HasMore)> GetMembersAsync(
        Guid actorUserId,
        Guid conversationId,
        int page,
        int pageSize,
        AccessContext accessContext,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> AddMemberAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> RemoveMemberAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> LeaveAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> ChangeMemberRoleAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        ConversationParticipantRole role,
        ConversationAdminPermission adminPermissions,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> UpdateMemberPermissionsAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        ConversationAdminPermission permissions,
        byte[] expectedVersion,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> UpdateSettingsAsync(
        Guid actorUserId,
        Guid conversationId,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        byte[] expectedVersion,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<DiscoverableConversationStorePage> DiscoverAsync(
        Guid actorUserId,
        ConversationType? type,
        string? query,
        int page,
        int pageSize,
        AccessContext accessContext,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> JoinPublicAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<OwnershipTransferStoreResult> StartOwnershipTransferAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<OwnershipTransferStoreResult> AcceptOwnershipTransferAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid transferId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<OwnershipTransferStoreResult> DeclineOwnershipTransferAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid transferId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ManagedConversationStoreResult> ArchiveAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

public interface IConversationManagementService
{
    Task<ManagedConversationResult> CreateGroupAsync(
        Guid userId,
        Guid sessionId,
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> CreateChannelAsync(
        Guid userId,
        Guid sessionId,
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> GetDetailsAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationParticipantPageResult> GetMembersAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> AddMemberAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        AddConversationMemberCommand command,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> RemoveMemberAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid targetUserId,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> LeaveAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> ChangeMemberRoleAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid targetUserId,
        ChangeConversationMemberRoleCommand command,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> UpdateMemberPermissionsAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid targetUserId,
        UpdateConversationPermissionsCommand command,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> UpdateSettingsAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        UpdateConversationSettingsCommand command,
        CancellationToken cancellationToken);

    Task<DiscoverableConversationPageResult> DiscoverAsync(
        Guid userId,
        Guid sessionId,
        DiscoverConversationsQuery query,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> JoinPublicAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<OwnershipTransferResult> StartOwnershipTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        StartOwnershipTransferCommand command,
        CancellationToken cancellationToken);

    Task<OwnershipTransferResult> AcceptOwnershipTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken);

    Task<OwnershipTransferResult> DeclineOwnershipTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken);

    Task<ManagedConversationResult> ArchiveAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);
}
