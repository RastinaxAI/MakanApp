using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record CreateManagedConversationCommand(
    Guid ClientOperationId,
    ConversationScope Scope,
    string Title,
    string? Description,
    IReadOnlyCollection<Guid>? InitialParticipantUserIds,
    ConversationVisibility Visibility = ConversationVisibility.Private,
    GroupHistoryPolicy GroupHistoryPolicy = GroupHistoryPolicy.FullHistory,
    GroupMemberCapability? MemberCapabilities = null);

public sealed record AddConversationMemberCommand(Guid UserId);

public sealed record ChangeConversationMemberRoleCommand(
    ConversationParticipantRole Role,
    ConversationAdminPermission AdminPermissions = ConversationAdminPermission.None);

public sealed record UpdateConversationPermissionsCommand(
    ConversationAdminPermission Permissions,
    string ExpectedVersion);

public sealed record UpdateConversationSettingsCommand(
    ConversationVisibility Visibility,
    GroupHistoryPolicy GroupHistoryPolicy,
    GroupMemberCapability MemberCapabilities,
    string ExpectedVersion);

public sealed record DiscoverConversationsQuery(
    ConversationType? Type,
    string? Query,
    int Page = 1,
    int PageSize = 30);

public sealed record StartOwnershipTransferCommand(Guid TargetUserId);

public sealed record ConversationParticipantResult(
    Guid ParticipantId,
    SafeMessagingIdentityResult User,
    ConversationParticipantRole Role,
    ConversationParticipantStatus Status,
    DateTime JoinedAtUtc,
    DateTime? EndedAtUtc,
    long HistoryVisibleFromSequence,
    ConversationAdminPermission AdminPermissions,
    string Version);

public sealed record ManagedConversationResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    Guid? OrganizationId,
    string Title,
    string? Description,
    ConversationManagementPolicy ManagementPolicy,
    ConversationVisibility Visibility,
    GroupHistoryPolicy GroupHistoryPolicy,
    GroupMemberCapability MemberCapabilities,
    ConversationStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    string Version,
    IReadOnlyCollection<ConversationParticipantResult> Participants,
    bool AlreadyExisted);

public sealed record DiscoverableConversationResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    string Title,
    string? Description,
    ConversationOrganizationBadgeResult? Organization,
    bool IsJoined,
    DateTime CreatedAtUtc);

public sealed record DiscoverableConversationPageResult(
    IReadOnlyCollection<DiscoverableConversationResult> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record ConversationParticipantPageResult(
    IReadOnlyCollection<ConversationParticipantResult> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record OwnershipTransferResult(
    Guid TransferId,
    Guid ConversationId,
    Guid FromUserId,
    Guid ToUserId,
    OwnershipTransferStatus Status,
    DateTime CreatedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? DeclinedAtUtc,
    bool AlreadyCompleted);
