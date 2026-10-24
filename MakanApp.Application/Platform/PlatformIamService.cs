using MakanApp.Domain.Platform;

namespace MakanApp.Application.Platform;

public sealed class PlatformIamService(
    IPlatformIamStore store,
    TimeProvider timeProvider) : IPlatformIamService, IPlatformPermissionResolver
{
    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || !PlatformPermissionKeys.All.Contains(permission, StringComparer.Ordinal))
        {
            return false;
        }

        var snapshot = await store.GetAuthorizationSnapshotAsync(userId, cancellationToken);
        return snapshot?.Permissions.Contains(permission, StringComparer.Ordinal) == true;
    }

    public async Task<PlatformMeResult> GetMeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.GetAuthorizationSnapshotAsync(userId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.AccessDenied, "دسترسی به Backoffice مجاز نیست.");
        return new PlatformMeResult(
            snapshot.UserId,
            snapshot.PersonId,
            snapshot.DisplayName,
            snapshot.Roles,
            snapshot.Permissions);
    }

    public Task<IReadOnlyCollection<PlatformRoleCatalogItem>> GetRoleCatalogAsync(
        CancellationToken cancellationToken) =>
        store.GetRoleCatalogAsync(cancellationToken);

    public Task<PlatformRoleAssignmentPage> GetAssignmentsAsync(
        PlatformRoleAssignmentQuery query,
        CancellationToken cancellationToken)
    {
        if (query.UserId == Guid.Empty ||
            query.Page is <= 0 or > 100_000 || query.PageSize is <= 0 or > 100 ||
            query.RoleKey?.Trim().Length > 100)
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "پارامترهای جست‌وجوی انتساب نقش معتبر نیستند.");
        }

        return store.GetAssignmentsAsync(
            query with { RoleKey = NormalizeOptional(query.RoleKey) },
            cancellationToken);
    }

    public Task<AdminAuditLogPage> GetAuditLogsAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ActorUserId == Guid.Empty || query.Page is <= 0 or > 100_000 ||
            query.PageSize is <= 0 or > 100 || query.FromUtc > query.ToUtc ||
            !TryParseSort(query.SortBy, out var sortField) ||
            !TryParseDirection(query.SortDirection, out var descending) ||
            query.Action?.Trim().Length > AdminAuditLog.MaximumActionLength ||
            query.TargetType?.Trim().Length > AdminAuditLog.MaximumTargetTypeLength ||
            query.TargetId?.Trim().Length > AdminAuditLog.MaximumTargetIdLength ||
            query.Result.HasValue && !Enum.IsDefined(typeof(AdminAuditResult), query.Result.Value))
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "پارامترهای جست‌وجوی ممیزی معتبر نیستند.");
        }

        return store.GetAuditLogsAsync(
            new AdminAuditQuerySpec(
                query.ActorUserId,
                NormalizeOptional(query.Action),
                NormalizeOptional(query.TargetType),
                NormalizeOptional(query.TargetId),
                query.Result,
                EnsureUtc(query.FromUtc),
                EnsureUtc(query.ToUtc),
                query.Page,
                query.PageSize,
                sortField,
                descending),
            cancellationToken);
    }

    public async Task<PlatformRoleMutationResult> GrantRoleAsync(
        Guid actorUserId,
        Guid actorSessionId,
        GrantPlatformRoleCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateMutation(actorUserId, actorSessionId, command.UserId, command.Reason, correlationId);
        if (string.IsNullOrWhiteSpace(command.RoleKey) || command.RoleKey.Trim().Length > 100)
        {
            throw Error(PlatformErrorCodes.RoleNotFound, "نقش پلتفرم یافت نشد.");
        }

        return await store.GrantRoleAsync(
            actorUserId,
            actorSessionId,
            command.UserId,
            command.RoleKey.Trim(),
            command.Reason.Trim(),
            UtcNow(),
            correlationId.Trim(),
            cancellationToken);
    }

    public async Task<PlatformRoleMutationResult> EndRoleAssignmentAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid assignmentId,
        EndPlatformRoleAssignmentCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateMutation(actorUserId, actorSessionId, assignmentId, command.Reason, correlationId);
        return await store.EndRoleAssignmentAsync(
            actorUserId,
            actorSessionId,
            assignmentId,
            command.Reason.Trim(),
            UtcNow(),
            correlationId.Trim(),
            cancellationToken);
    }

    private static void ValidateMutation(
        Guid actorUserId,
        Guid actorSessionId,
        Guid targetId,
        string reason,
        string correlationId)
    {
        if (actorUserId == Guid.Empty || actorSessionId == Guid.Empty || targetId == Guid.Empty)
        {
            throw Error(PlatformErrorCodes.AccessDenied, "شناسه عملیات مدیریتی معتبر نیست.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > AdminAuditLog.MaximumReasonLength)
        {
            throw Error(PlatformErrorCodes.ReasonRequired, "دلیل معتبر برای عملیات حساس الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(correlationId) ||
            correlationId.Trim().Length > AdminAuditLog.MaximumCorrelationIdLength)
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "شناسه همبستگی عملیات معتبر نیست.");
        }
    }

    private static bool TryParseSort(string value, out AdminAuditSortField field)
    {
        field = value.Trim().ToLowerInvariant() switch
        {
            "occurredatutc" => AdminAuditSortField.OccurredAtUtc,
            "action" => AdminAuditSortField.Action,
            "result" => AdminAuditSortField.Result,
            _ => 0
        };
        return field != 0;
    }

    private static bool TryParseDirection(string value, out bool descending)
    {
        descending = string.Equals(value?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return descending || string.Equals(value?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? EnsureUtc(DateTime? value) =>
        value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static PlatformException Error(string code, string message) => new(code, message);
}

public sealed class PlatformBootstrapService(
    IPlatformIamStore store,
    TimeProvider timeProvider) : IPlatformBootstrapService
{
    public Task<PlatformBootstrapResult> BootstrapOwnerAsync(
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new PlatformException(
                PlatformErrorCodes.UserNotFound,
                "کاربر bootstrap یافت نشد.");
        }

        var normalizedCorrelationId = correlationId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCorrelationId) ||
            normalizedCorrelationId.Length > AdminAuditLog.MaximumCorrelationIdLength)
        {
            throw new PlatformException(
                PlatformErrorCodes.QueryInvalid,
                "شناسه همبستگی bootstrap معتبر نیست.");
        }

        return store.BootstrapOwnerAsync(
            userId,
            timeProvider.GetUtcNow().UtcDateTime,
            normalizedCorrelationId,
            cancellationToken);
    }
}
