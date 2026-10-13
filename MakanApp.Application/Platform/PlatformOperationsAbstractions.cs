namespace MakanApp.Application.Platform;

public interface IPlatformOperationsStore
{
    Task<PlatformOrganizationPage> GetOrganizationsAsync(
        PlatformOrganizationQuery query,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationDetail?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationImpactPreview?> GetOrganizationImpactAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformProvisioningPage> GetProvisioningAsync(
        PlatformProvisioningQuery query,
        CancellationToken cancellationToken);

    Task<PlatformProvisioningResult> ProvisionAsync(
        PlatformActorContext actor,
        ProvisionOrganizationCommand command,
        CancellationToken cancellationToken);

    Task<PlatformOrganizationMutationResult> SetOrganizationActiveAsync(
        PlatformActorContext actor,
        Guid organizationId,
        bool active,
        string reason,
        CancellationToken cancellationToken);

    Task<PlatformMembershipPage> GetMembershipsAsync(
        Guid organizationId,
        PlatformMembershipQuery query,
        CancellationToken cancellationToken);

    Task<PlatformInvitationResult> CreateInvitationAsync(
        PlatformActorContext actor,
        Guid organizationId,
        CreatePlatformInvitationCommand command,
        CancellationToken cancellationToken);

    Task<PlatformMembershipMutationResult> ChangeMembershipRoleAsync(
        PlatformActorContext actor,
        Guid organizationId,
        Guid membershipId,
        ChangePlatformMembershipRoleCommand command,
        CancellationToken cancellationToken);

    Task<PlatformMembershipMutationResult> EndMembershipAsync(
        PlatformActorContext actor,
        Guid organizationId,
        Guid membershipId,
        string reason,
        CancellationToken cancellationToken);

    Task<PlatformUserPage> GetUsersAsync(
        PlatformUserQuery query,
        CancellationToken cancellationToken);

    Task<PlatformUserDetail?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<PlatformUserMutationResult> SetUserActiveAsync(
        PlatformActorContext actor,
        Guid userId,
        bool active,
        string reason,
        CancellationToken cancellationToken);

    Task<PlatformSessionRevocationResult> RevokeSessionsAsync(
        PlatformActorContext actor,
        Guid userId,
        Guid? sessionId,
        string reason,
        CancellationToken cancellationToken);
}

public interface IPlatformOperationsService
{
    Task<PlatformOrganizationPage> GetOrganizationsAsync(PlatformOrganizationQuery query, CancellationToken cancellationToken);
    Task<PlatformOrganizationDetail> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<PlatformOrganizationImpactPreview> GetOrganizationImpactAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<PlatformProvisioningPage> GetProvisioningAsync(PlatformProvisioningQuery query, CancellationToken cancellationToken);
    Task<PlatformProvisioningResult> CreateOrganizationAsync(Guid actorUserId, Guid actorSessionId, CreatePlatformOrganizationCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformProvisioningResult> ProvisionAsync(Guid actorUserId, Guid actorSessionId, ProvisionOrganizationCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformOrganizationMutationResult> SuspendOrganizationAsync(Guid actorUserId, Guid actorSessionId, Guid organizationId, PlatformOrganizationStateCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformOrganizationMutationResult> ReactivateOrganizationAsync(Guid actorUserId, Guid actorSessionId, Guid organizationId, PlatformOrganizationStateCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformMembershipPage> GetMembershipsAsync(Guid organizationId, PlatformMembershipQuery query, CancellationToken cancellationToken);
    Task<PlatformInvitationResult> CreateInvitationAsync(Guid actorUserId, Guid actorSessionId, Guid organizationId, CreatePlatformInvitationCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformMembershipMutationResult> ChangeMembershipRoleAsync(Guid actorUserId, Guid actorSessionId, Guid organizationId, Guid membershipId, ChangePlatformMembershipRoleCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformMembershipMutationResult> EndMembershipAsync(Guid actorUserId, Guid actorSessionId, Guid organizationId, Guid membershipId, EndPlatformMembershipCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformUserPage> GetUsersAsync(PlatformUserQuery query, CancellationToken cancellationToken);
    Task<PlatformUserDetail> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<PlatformUserMutationResult> SuspendUserAsync(Guid actorUserId, Guid actorSessionId, Guid userId, PlatformUserStateCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformUserMutationResult> ReactivateUserAsync(Guid actorUserId, Guid actorSessionId, Guid userId, PlatformUserStateCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformSessionRevocationResult> RevokeSessionsAsync(Guid actorUserId, Guid actorSessionId, Guid userId, Guid? sessionId, RevokePlatformSessionsCommand command, string correlationId, CancellationToken cancellationToken);
}
