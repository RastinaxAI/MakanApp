namespace MakanApp.Application.Platform;

public interface IPlatformPermissionResolver
{
    Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken);
}

public interface IPlatformStepUpAuthorization
{
    Task<bool> IsSatisfiedAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IPlatformIamStore
{
    Task<PlatformAuthorizationSnapshot?> GetAuthorizationSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PlatformRoleCatalogItem>> GetRoleCatalogAsync(
        CancellationToken cancellationToken);

    Task<PlatformRoleAssignmentPage> GetAssignmentsAsync(
        PlatformRoleAssignmentQuery query,
        CancellationToken cancellationToken);

    Task<AdminAuditLogPage> GetAuditLogsAsync(
        AdminAuditQuerySpec query,
        CancellationToken cancellationToken);

    Task<PlatformBootstrapResult> BootstrapOwnerAsync(
        Guid userId,
        DateTime occurredAtUtc,
        string correlationId,
        CancellationToken cancellationToken);

    Task<PlatformRoleMutationResult> GrantRoleAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid targetUserId,
        string roleKey,
        string reason,
        DateTime occurredAtUtc,
        string correlationId,
        CancellationToken cancellationToken);

    Task<PlatformRoleMutationResult> EndRoleAssignmentAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid assignmentId,
        string reason,
        DateTime occurredAtUtc,
        string correlationId,
        CancellationToken cancellationToken);
}

public interface IPlatformIamService
{
    Task<PlatformMeResult> GetMeAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PlatformRoleCatalogItem>> GetRoleCatalogAsync(
        CancellationToken cancellationToken);

    Task<PlatformRoleAssignmentPage> GetAssignmentsAsync(
        PlatformRoleAssignmentQuery query,
        CancellationToken cancellationToken);

    Task<AdminAuditLogPage> GetAuditLogsAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken);

    Task<PlatformRoleMutationResult> GrantRoleAsync(
        Guid actorUserId,
        Guid actorSessionId,
        GrantPlatformRoleCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    Task<PlatformRoleMutationResult> EndRoleAssignmentAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid assignmentId,
        EndPlatformRoleAssignmentCommand command,
        string correlationId,
        CancellationToken cancellationToken);
}

public interface IPlatformBootstrapService
{
    Task<PlatformBootstrapResult> BootstrapOwnerAsync(
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken);
}
