namespace MakanApp.Application.Platform;

public sealed record PlatformAuthorizationSnapshot(
    Guid UserId,
    Guid? PersonId,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record PlatformMeResult(
    Guid UserId,
    Guid? PersonId,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record PlatformRoleCatalogItem(
    Guid RoleId,
    string Key,
    string DisplayName,
    IReadOnlyCollection<string> Permissions);

public sealed record PlatformRoleAssignmentItem(
    Guid AssignmentId,
    Guid UserId,
    Guid? PersonId,
    string DisplayName,
    Guid RoleId,
    string RoleKey,
    string RoleDisplayName,
    bool IsActive,
    DateTime GrantedAtUtc,
    DateTime? EndedAtUtc,
    Guid? GrantedByUserId,
    Guid? EndedByUserId,
    string Version);

public sealed record PlatformRoleAssignmentPage(
    IReadOnlyCollection<PlatformRoleAssignmentItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PlatformRoleAssignmentQuery(
    Guid? UserId,
    string? RoleKey,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 30);

public sealed record GrantPlatformRoleCommand(
    Guid UserId,
    string RoleKey,
    string Reason);

public sealed record EndPlatformRoleAssignmentCommand(string Reason);

public sealed record PlatformRoleMutationResult(
    PlatformRoleAssignmentItem Assignment,
    bool Replayed);

public sealed record PlatformBootstrapResult(
    PlatformRoleAssignmentItem Assignment,
    bool Replayed);

public enum AdminAuditSortField
{
    OccurredAtUtc = 1,
    Action = 2,
    Result = 3
}

public sealed record AdminAuditLogQuery(
    Guid? ActorUserId,
    string? Action,
    string? TargetType,
    string? TargetId,
    int? Result,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 50,
    string SortBy = "occurredAtUtc",
    string SortDirection = "desc");

public sealed record AdminAuditQuerySpec(
    Guid? ActorUserId,
    string? Action,
    string? TargetType,
    string? TargetId,
    int? Result,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize,
    AdminAuditSortField SortField,
    bool Descending);

public sealed record AdminAuditLogItem(
    Guid Id,
    Guid? ActorUserId,
    Guid? ActorPersonId,
    Guid? ActorSessionId,
    string Action,
    string TargetType,
    string TargetId,
    Guid? OrganizationId,
    string Reason,
    DateTime OccurredAtUtc,
    string CorrelationId,
    int Result,
    string? BeforeSummary,
    string? AfterSummary);

public sealed record AdminAuditLogPage(
    IReadOnlyCollection<AdminAuditLogItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
