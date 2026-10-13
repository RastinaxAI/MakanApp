using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Platform;

public sealed record PlatformActorContext(
    Guid UserId,
    Guid SessionId,
    DateTime OccurredAtUtc,
    string CorrelationId);

public sealed record PlatformOrganizationQuery(
    string? Search,
    OrganizationStatus? Status,
    int Page = 1,
    int PageSize = 30,
    string SortBy = "createdAtUtc",
    string SortDirection = "desc");

public sealed record PlatformManagerSummary(Guid UserId, string DisplayName);

public sealed record PlatformSubscriptionSummary(
    Guid SubscriptionId,
    Guid PlanVersionId,
    string PlanName,
    SubscriptionStatus Status,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc);

public sealed record PlatformOrganizationItem(
    Guid Id,
    string Name,
    OrganizationStatus Status,
    int ActiveMemberCount,
    int ActiveClassCount,
    int ActiveManagerCount,
    IReadOnlyCollection<PlatformManagerSummary> Managers,
    PlatformSubscriptionSummary? Subscription,
    DateTime CreatedAtUtc);

public sealed record PlatformOrganizationPage(
    IReadOnlyCollection<PlatformOrganizationItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformOrganizationRoleCount(OrganizationRole Role, int Count);

public sealed record PlatformClassSummary(
    int TotalCount,
    int ActiveCount,
    int TotalCapacity);

public sealed record PlatformOrganizationActivitySummary(
    DateTime? LastMembershipCreatedAtUtc,
    DateTime? LastClassCreatedAtUtc,
    DateTime? LastAdministrativeActionAtUtc);

public sealed record PlatformOrganizationDetail(
    Guid Id,
    string Name,
    OrganizationStatus Status,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<PlatformManagerSummary> Managers,
    int ActiveMemberCount,
    IReadOnlyCollection<PlatformOrganizationRoleCount> ActiveRoles,
    PlatformClassSummary Classes,
    PlatformSubscriptionSummary? Subscription,
    PlatformOrganizationActivitySummary Activity,
    string Version);

public sealed record PlatformOrganizationImpactPreview(
    Guid OrganizationId,
    int ActiveMembershipCount,
    int ActiveClassCount,
    int ActiveSubscriptionCount,
    int PendingInvitationCount,
    int PendingProvisioningCount);

public sealed record CreatePlatformOrganizationCommand(
    string ClientOperationId,
    string Name,
    string Reason);

public sealed record ProvisionOrganizationCommand(
    string ClientOperationId,
    string OrganizationName,
    Guid? PlanVersionId,
    Guid? InitialManagerUserId,
    DateTime? SubscriptionEndsAtUtc,
    DateTime? InvitationExpiresAtUtc,
    string Reason);

public sealed record PlatformProvisioningQuery(
    PlatformProvisioningStatus? Status,
    int Page = 1,
    int PageSize = 30);

public sealed record PlatformProvisioningItem(
    Guid Id,
    string ClientOperationId,
    Guid RequestedByUserId,
    string OrganizationName,
    Guid? PlanVersionId,
    Guid? InitialManagerUserId,
    PlatformProvisioningStatus Status,
    Guid? OrganizationId,
    Guid? SubscriptionId,
    Guid? ManagerInvitationId,
    string? FailureCode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Version);

public sealed record PlatformProvisioningPage(
    IReadOnlyCollection<PlatformProvisioningItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformProvisioningResult(
    PlatformProvisioningItem Request,
    bool Replayed);

public sealed record PlatformOrganizationStateCommand(string Reason);

public sealed record PlatformOrganizationMutationResult(
    PlatformOrganizationDetail Organization,
    PlatformOrganizationImpactPreview Impact,
    bool Replayed);

public sealed record PlatformMembershipQuery(
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 50);

public sealed record PlatformMembershipItem(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    MembershipStatus Status,
    IReadOnlyCollection<OrganizationRole> ActiveRoles,
    DateTime CreatedAtUtc,
    DateTime? EndedAtUtc,
    string Version);

public sealed record PlatformMembershipPage(
    IReadOnlyCollection<PlatformMembershipItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreatePlatformInvitationCommand(
    Guid UserId,
    OrganizationRole Role,
    DateTime ExpiresAtUtc,
    string Reason);

public sealed record PlatformInvitationResult(
    Guid InvitationId,
    Guid OrganizationId,
    Guid DestinationUserId,
    OrganizationRole Role,
    InvitationStatus Status,
    DateTime ExpiresAtUtc,
    bool Replayed);

public sealed record ChangePlatformMembershipRoleCommand(
    OrganizationRole FromRole,
    OrganizationRole ToRole,
    string Reason);

public sealed record EndPlatformMembershipCommand(string Reason);

public sealed record PlatformMembershipMutationResult(
    PlatformMembershipItem Membership,
    bool Replayed);

public sealed record PlatformUserQuery(
    string? Search,
    UserAccountStatus? Status,
    int Page = 1,
    int PageSize = 30,
    string SortBy = "createdAtUtc",
    string SortDirection = "desc");

public sealed record PlatformUserItem(
    Guid UserId,
    Guid? PersonId,
    string DisplayName,
    string? MaskedPhone,
    UserAccountStatus AccountStatus,
    DateTime CreatedAtUtc,
    DateTime? LastSessionActivityAtUtc,
    IReadOnlyCollection<string> PlatformRoles,
    int ActiveOrganizationMembershipCount);

public sealed record PlatformUserPage(
    IReadOnlyCollection<PlatformUserItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformUserMembershipSummary(
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    MembershipStatus Status,
    IReadOnlyCollection<OrganizationRole> ActiveRoles);

public sealed record PlatformUserSessionSummary(
    Guid SessionId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    bool IsActive);

public sealed record PlatformGuardianSummary(int ActiveAsGuardian, int ActiveAsLearner);

public sealed record PlatformAdminHistoryReference(
    Guid AuditId,
    string Action,
    DateTime OccurredAtUtc,
    int Result);

public sealed record PlatformUserDetail(
    Guid UserId,
    Guid? PersonId,
    string DisplayName,
    string? Username,
    string? MaskedPhone,
    bool MobileVerified,
    UserAccountStatus AccountStatus,
    DateTime? SuspendedAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<PlatformUserMembershipSummary> Memberships,
    PlatformGuardianSummary GuardianRelations,
    IReadOnlyCollection<PlatformUserSessionSummary> Sessions,
    IReadOnlyCollection<string> PlatformRoles,
    IReadOnlyCollection<PlatformAdminHistoryReference> AdministrativeHistory,
    string Version);

public sealed record PlatformUserStateCommand(string Reason);

public sealed record RevokePlatformSessionsCommand(string Reason);

public sealed record PlatformUserMutationResult(
    Guid UserId,
    UserAccountStatus AccountStatus,
    DateTime? SuspendedAtUtc,
    int RevokedSessionCount,
    bool Replayed,
    string Version);

public sealed record PlatformSessionRevocationResult(
    Guid UserId,
    Guid? SessionId,
    int RevokedSessionCount,
    bool Replayed);
